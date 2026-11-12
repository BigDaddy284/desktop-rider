# Architecture

Desktop Rider is a native Windows application written in C# on .NET 10 with WPF, following DESIGN.md section 8.

## Projects

| Project | Target | Purpose |
|---|---|---|
| `src/DesktopRider.Core` | `net10.0` | Everything that does not need Windows: settings, file locations, logging, shared service registration. Fully unit tested. |
| `src/DesktopRider.Desktop` | `net10.0-windows` | Windows desktop integration: the monitor list, moving the character's window in physical pixels, and the icon beside the clock. |
| `src/DesktopRider.App` | `net10.0-windows` | The WPF application: start-up, shutdown, windows and view models. Produces `DesktopRider.exe`. |
| `src/DesktopRider.Animation` | `net10.0` | The sprite list and the animation player: which frame of which animation shows at any moment. No window, no timer, fully unit tested. |
| `src/DesktopRider.AI` | `net10.0` | Talking to AI models: the provider abstraction, the Ollama provider, the chat service and the AI lines of the icon's menu. No window, fully unit tested. |
| `tests/DesktopRider.AI.Tests` | `net10.0` | xUnit tests for AI. |
| `tests/DesktopRider.Core.Tests` | `net10.0` | xUnit tests for Core. |
| `tests/DesktopRider.Animation.Tests` | `net10.0` | xUnit tests for Animation. |

The rule for where code goes: if it can work without a window, it lives outside the App project so that it can be tested automatically on any machine.

## Start-up and shutdown

1. `App.OnStartup` takes the single-instance guard. A second copy explains that the rider is already running and exits.
2. The local data folder is created and services are registered through `AddDesktopRiderCore`.
3. Error handlers are attached so an unexpected problem is logged and does not close the rider.
4. Settings are loaded without blocking the interface, then the character's window is shown.
5. `App.OnExit` logs the exit, detaches handlers and disposes the services and the guard.

## The character's window

`OverlayWindow` is a borderless WPF window with a fully transparent background, no taskbar button, and a size that fits its content. Because nothing is drawn in the empty area, Windows passes clicks there straight through to whatever is behind; only the visible character receives the mouse.

- `OverlayViewModel` (Core) supplies what the window shows and the right-click menu actions. It has no knowledge of windows, so it is unit tested.
- `PlacementCalculator` (Core) decides where the character may sit: the default corner, restoring a saved spot, and keeping it fully on one screen. Unit tested with single, multiple and negative-coordinate monitor layouts.
- `WindowPlacementController` (Desktop) carries those decisions out: it restores the position when the window loads, handles dragging, re-checks the position when Windows reports a display change, and saves the spot after each drag.

## The icon beside the clock

Desktop Rider has no taskbar button, so its icon in the notification area is the way to reach it when the rider is hidden.

- `TrayViewModel` (Core) decides everything: the menu for the current state, what a click does, the hover text and when to show the "rider is hidden" reminder. Unit tested.
- `TrayIconController` (Desktop) carries it out with the standard Windows notification icon. The menu is rebuilt each time it opens, so nothing updates in the background.
- `TrayIconFactory` (Desktop) makes the icon picture at start-up by shrinking the drawn part of the rider's resting frame. `IconLayout` (Core) does the arithmetic and is unit tested. If the sprites are missing, the standard Windows application icon is used.
- Hiding sets `Appearance.CharacterVisible` to false. `OverlayWindow` follows that value by hiding or showing itself, and hiding stops the animation timer, so a hidden rider uses no processor time.
- `App` creates the icon after the window and removes it in `OnExit`. Exit is also handled by `App` directly so that it works when the rider started hidden and its window was never opened.

Positions are measured and saved in physical screen pixels through the Windows API, not in WPF units, so they mean the same thing on monitors with different scaling.

Until the artwork is approved, the window shows a neutral grey stand-in card that says so. It is not character artwork.

## Animation

`AnimationPlayer` (Animation project) is told how much time has passed and answers with the current animation and frame. It knows the rules in SPRITE_SPECIFICATION.md: loops, one-shots, lead-in and lead-out animations, priorities and speed. It never touches the AI, so the rider keeps moving whatever the AI is doing.

`RiderSprite` (App project) is the thin Windows layer around it. It owns one timer that ticks at the sprites' frame rate, cuts the current frame out of its sheet and hands it to the window. The timer stops while the character is hidden. If the sprite files are missing or damaged the window shows a plain stand-in card instead of failing, and the reason is logged.

`IdleVariationPicker` asks for a small idle animation every 8 to 20 seconds while the rider is resting, unless idle animations are switched off in settings.

## AI

The AI project depends on Core only. Nothing in Animation or Desktop refers to it, so the rider animates and behaves normally whether or not an AI is available.

- `IAiProvider` is the abstraction every provider implements: list the models (which doubles as the connection test) and stream a reply piece by piece. Both can be cancelled. Problems are always reported as an `AiProviderException` with a kind (`NotRunning`, `Timeout`, `NoModelSelected`, `ModelNotFound`, `ConnectionLost`, `InvalidResponse`, `ProviderError`, `UnknownProvider`) and a plain-English message that can be shown to the user as it is.
- `OllamaProvider` implements it over Ollama's HTTP interface (`GET /api/tags`, `POST /api/chat` with streaming). It reads the address and waiting time from settings for every request, so a change applies immediately.
- `AiProviderRegistry` finds the provider named in settings. Adding LM Studio or another local provider later means writing one class and registering it.
- `AiChatService` is what the rest of the application uses. It applies the user's settings (provider, model, personality, temperature) so callers only pass the conversation. Closing the application cancels anything still in progress.
- `AiMenuViewModel` adds the AI lines to the icon's menu through `ITrayMenuContributor` (Core): check the connection, choose a model, ask for a hello, stop a reply. It announces progress and results as events; `App` shows them in `MessageWindow`, one reused window whose words are replaced as a reply grows. The hello line is a stand-in for trying the connection until the conversation window exists (Phase 6).

Rules the code follows:

- **No request without the user asking.** Creating the services contacts nothing. There is no polling and no background check.
- **No model name is assumed.** The only model names the code ever uses come from Ollama's own list, after the user picks one.
- **Time limits are on silence, not on the whole reply.** Listing models allows 10 seconds. A reply allows `Ai.RequestTimeoutSeconds` (120 by default) before the first piece and again between pieces, so a long reply that keeps arriving is never cut off, but a stalled one is.
- **Stopped is not failed.** Cancellation by the user or by shutdown surfaces as `OperationCanceledException`, never as an error message.
- **Privacy.** Conversation text is never logged. Addresses are logged and shown without any sign-in details they may contain.

## Local data

Everything is stored under `%LOCALAPPDATA%\DesktopRider` on the user's own computer:

| Item | Location |
|---|---|
| Settings | `settings.json` |
| Logs | `Logs\desktop-rider-YYYYMMDD.log`, one per day, 14 days kept |

Nothing is sent anywhere. There is no telemetry.

## Settings

`AppSettings` has one section per settings category in DESIGN.md section 7: Appearance, Behaviour, AI, Memory and Application.

- `JsonSettingsStore` reads and writes the file. Saves go to a temporary file first and are then swapped in, so an interrupted save cannot leave a broken file.
- A missing file means defaults. A damaged file is renamed to `settings.damaged-<date>.json`, defaults are used, and the application starts normally.
- `SettingsNormalizer` runs on every load and save. It fills in missing sections and pulls out-of-range values back into range, so the rest of the code never has to check.
- No AI model name is ever assumed; `Ai.Model` stays empty until the user picks one. Proactive comments and update checks are off by default.

## Logging

`FileLoggerProvider` plugs into the standard `Microsoft.Extensions.Logging` interfaces and writes plain text. A failure to write a log entry is swallowed on purpose: logging must never crash the rider.

## Technical decisions

| Date | Decision | Reason |
|---|---|---|
| 2026-11-12 | Core logic targets plain `net10.0`, separate from the WPF project. | Lets tests run on any machine and keeps window code thin. |
| 2026-11-12 | A small built-in file logger instead of a logging library. | Needs are simple; avoids an unnecessary dependency. |
| 2026-11-12 | Warnings are treated as errors with the recommended analyzers switched on. | Keeps quality problems from accumulating. |
| 2026-11-12 | Per-monitor DPI awareness (V2) is declared in the application manifest. | Required for sharp rendering across monitors with different scaling. |
| 2026-11-12 | Window position is handled in physical pixels through `GetWindowRect`/`SetWindowPos`. | WPF's own coordinates are unreliable when monitors use different scaling. |
| 2026-11-12 | Windows Forms is referenced by the Desktop project for `Screen.AllScreens` (and later the tray icon). | It is part of .NET for Windows, so it avoids a third-party dependency. |
| 2026-11-12 | View models live in Core and use hand-written `RelayCommand`/`ViewModelBase`. | Keeps them testable off Windows without adding an MVVM library for a few lines of code. |
| 2026-11-12 | Original reference photographs are not committed. | The repository is public and the photographs are personal. Owner's decision. |
| 2026-11-12 | The notification-area icon uses the Windows Forms `NotifyIcon` that ships with .NET. | Already available in the Desktop project; avoids a third-party package. |
| 2026-11-12 | The icon picture is made from the rider's own resting frame when the application starts. | The approved artwork is the only permitted source for the rider's appearance; no separate icon is invented. |
| 2026-11-12 | Whether the rider is hidden is remembered between sessions, and a hidden start always shows a reminder beside the clock. | Respects the user's choice without leaving them wondering whether the application started. |
| 2026-11-12 | The reminder on hiding is shown once ever (`Application.TrayHintShown`). | Helpful the first time, an unnecessary pop-up afterwards. |
| 2026-11-12 | The Ollama provider uses `HttpClient` and `System.Text.Json` directly rather than an Ollama client library. | Two simple requests; avoids a dependency and keeps full control of time limits and cancellation. |
| 2026-11-12 | The reply time limit restarts with each piece received. | Local models can take a long time overall; only silence means something is wrong. |
| 2026-11-12 | The connection test is the model list request. | One request answers both "is Ollama there?" and "what can I choose?". |
| 2026-11-12 | Until the conversation window exists, the AI is reached from the icon's menu, with progress and replies shown in one small message window (`MessageWindow`). | Lets the owner prove the connection on their own PC now. Messages beside the clock were tried first, but Windows can hide those (Do Not Disturb, notification settings), which left a click looking as if it did nothing. |
| 2026-11-12 | A built-in personality (DESIGN.md 5.4) is sent unless the user has written their own. It gives the rider no name and says it cannot see the screen. | A sensible default is needed for any reply; names and other details are the owner's to decide. |
| 2026-11-12 | Every AI menu choice shows a message the moment it is clicked, before any result. | The user must always see that the click was received, even when Ollama is slow. |
| 2026-11-12 | The icon is always present while Desktop Rider runs. The "Minimise to system tray" setting is left for the settings screen in Phase 8. | With no taskbar button, the icon is the only way back to a hidden rider. |
