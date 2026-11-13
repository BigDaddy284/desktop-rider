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

`CharacterBehaviour` (Animation project) is the state-based behaviour system from DESIGN.md section 6. The rest of the application only says which state the character is in (`Idle`, `Thinking`, `Talking`); the behaviour turns that into requests to the player. Thinking is a single gesture in the sprites, so it is asked for again each time it ends. Talking is a loop; it waits for a gesture in progress to finish, and is shown for at least two seconds so a very quick reply is still seen being spoken. Further states (moving, sleeping and so on) will be added here as those features arrive.

**Big animations.** A jump needs far more room than the rider's small window. Such an animation has its own larger frame in the sprite list, and says where the standard frame sits inside it. `OverlayWindow` shows these in a second see-through window (`StageWindow`) laid exactly over the rider, and hides the rider's own picture while it plays, so the rider seems to stay put while a ramp arrives and it takes off. Clicks pass straight through the stage window (`ClickThroughWindow`). The rider's own window never changes size or place, so dragging, the saved position and the speech bubbles are unaffected. The swap between the two windows always overlaps by one frame, in which both show the same resting pose, so there is no flicker. The large sheets are read on a background thread before the animation starts and kept in their compact 256-colour form; only the frame on screen is expanded.

`IdleVariationPicker` asks for a small idle animation every 8 to 20 seconds while the rider is resting, unless idle animations are switched off in settings.

## AI

The AI project depends on Core only. Nothing in Animation or Desktop refers to it, so the rider animates and behaves normally whether or not an AI is available.

- `IAiProvider` is the abstraction every provider implements: list the models (which doubles as the connection test) and stream a reply piece by piece. Both can be cancelled. Problems are always reported as an `AiProviderException` with a kind (`NotRunning`, `Timeout`, `NoModelSelected`, `ModelNotFound`, `ConnectionLost`, `InvalidResponse`, `ProviderError`, `UnknownProvider`) and a plain-English message that can be shown to the user as it is.
- `OllamaProvider` implements it over Ollama's HTTP interface (`GET /api/tags`, `POST /api/chat` with streaming). It reads the address and waiting time from settings for every request, so a change applies immediately.
- `AiProviderRegistry` finds the provider named in settings. Adding LM Studio or another local provider later means writing one class and registering it.
- `AiChatService` is what the rest of the application uses. It applies the user's settings (provider, model, personality, temperature) so callers only pass the conversation. Closing the application cancels anything still in progress.
- `AiMenuViewModel` adds the AI lines to the icon's menu through `ITrayMenuContributor` (Core): talk to the rider, check the connection, choose a model. It announces progress and results as events; `App` shows them in `MessageWindow`, one reused window.

Rules the code follows:

- **No request without the user asking.** Creating the services contacts nothing. There is no polling and no background check. Opening the conversation window asks Ollama once for its model list, which is how the window shows the connection status and the models to choose from.
- **No model name is assumed.** The only model names the code ever uses come from Ollama's own list, after the user picks one.
- **Time limits are on silence, not on the whole reply.** Listing models allows 10 seconds. A reply allows `Ai.RequestTimeoutSeconds` (120 by default) before the first piece and again between pieces, so a long reply that keeps arriving is never cut off, but a stalled one is.
- **Stopped is not failed.** Cancellation by the user or by shutdown surfaces as `OperationCanceledException`, never as an error message.
- **Privacy.** Conversation text is never logged. Addresses are logged and shown without any sign-in details they may contain.

## Conversation

The conversation follows the same rule as everything else: decisions in window-free, tested classes; thin windows that carry them out.

- `ConversationViewModel` (AI project) is the conversation: the lines said so far, what is being typed, send, stop and clear, the model in use, the connection status and plain-English advice when the AI is not ready. Its `State` (`Idle`, `Thinking`, `Talking`) is what the rider's animation follows. Explanations of problems are shown as lines of their own and are never sent to the AI. At most the newest 30 lines are sent with each message and at most 200 are kept on screen.
- `SpeechBubbleViewModel` (AI project) decides what the speech bubble says and when. The bubble is for when the conversation window is closed: "Thinking…", the reply as it arrives, then the finished reply for a reading time based on its length. While the conversation window is open the bubble stays away, because the same words are already there.
- `ChatWindow`, `SpeechBubbleWindow` and `InputBubbleWindow` (App project) are the windows. All are borderless and transparent around their rounded shapes. The speech bubble never takes the keyboard.
- **Chat in bubbles** (`Appearance.ChatInBubbles`, off by default) swaps the conversation window for `InputBubbleWindow`, a small typing bubble that asks to sit below the rider, with the rider answering in the speech bubble. Both ways share the one `ConversationViewModel`, so switching loses nothing. While the typing bubble is open, `SpeechBubbleViewModel.KeepOpen` holds a finished reply on screen instead of letting it time out.
- `Themes/Dark.xaml` (App project) holds the shared look: the dark, rounded style of Windows 11's own right-click menus, used by the rider's menu, the conversation window and both bubbles. The model list in the conversation window is a menu in the same style. `DarkMenuRenderer` (Desktop) gives the notification-area menu the same colours.
- `PlacementCalculator.Beside` (Core) decides where they go: above the rider by preference, then left, right, below, always fully on the rider's screen. `CompanionWindowPlacer` (Desktop) carries that out in physical pixels and keeps them beside the rider while it is dragged.
- `ConversationPresenter` (App project) joins it all up: a click on the rider opens or closes the conversation window, the conversation's state is passed to `CharacterBehaviour`, and hiding the rider hides both windows. The windows are created the first time they are needed.
- `WindowPlacementController` tells a click from a drag: if the rider has not moved when the mouse button comes up, it was a click. The second press of a double-click is ignored.

**Tricks on request.** `RiderActions` (AI project) lists what the rider can be asked to do (wheelie, celebrate, rev, sleep, ride, look left, look right) and recognises a request two ways. The AI is told, in an addition to the personality that is always sent (`RiderPersonality.Compose`), to start its reply with a tag such as `[wheelie]`; `ActionTagFilter` takes tags out of the reply as it streams, so they are never shown. Because small local models often forget the tag, `RiderActions.FromRequest` also recognises a plain request in the user's own message ("do a wheelie", but not "what is a wheelie?"), and that is carried out as the answer starts. Each action happens at most once per message. `ConversationViewModel.ActionRequested` announces it, `ConversationPresenter` passes the animation name on, and `CharacterBehaviour.Perform` plays it: a trick takes precedence over thinking and talking, which carry on afterwards, and a looping animation is kept up for four seconds and then ended through its proper finishing animation. The AI project still knows nothing about animation; it only names the action.

The conversation exists only in memory for now. Saving it between sessions is Phase 7.

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
| 2026-11-12 | The speech bubble and the conversation window are separate windows placed beside the rider, not part of the rider's own window. | The rider's window is sized to the artwork and its position is saved; growing it for a bubble would move the rider and block clicks around it. |
| 2026-11-12 | The speech bubble only shows while the conversation window is closed. | With the window open the reply is already on screen; showing it twice side by side would be clutter. Closing the window mid-reply hands the rest to the bubble. |
| 2026-11-12 | The conversation window always sits beside the rider and cannot be dragged on its own. | It keeps the rider as the interface: move the rider and the conversation comes too. |
| 2026-11-12 | The rider's menu, the conversation window and the bubbles use the dark, rounded style of Windows 11's right-click menus. | The owner asked for it, with a screenshot of the Windows menu as the reference. Greys only; no colours are invented for the rider. |
| 2026-11-12 | "Chat in bubbles" is a tick on the rider's menu, also reachable from a button in each chat style. | The owner asked for a switch. One setting, remembered, visible where the conversation is. |
| 2026-11-12 | Clicking the speech bubble always opens the conversation window, even in bubble mode. | The bubble shows only part of a long reply, and bubble mode has no history; the window is where the whole conversation can be read. |
| 2026-11-13 | Tricks are asked for by a tag in the AI's reply, backed up by recognising plain requests in the user's message. | Tags work with any model and need no special model features; the backup makes "do a wheelie" reliable with small models that ignore the instruction. |
| 2026-11-13 | Closing the typing bubble takes a finished answer away with it. | The owner expected a click on the rider to clear both bubbles. |
| 2026-11-13 | The conversation only ends talking that it started itself. | Otherwise choosing **Talking** from the rider's menu would be cancelled at once. Found by a test while adding tricks. |
| 2026-11-12 | Menu pictures come from the icon font built into Windows 11 (Segoe Fluent Icons). | Matches the Windows menus without adding image files or a dependency. |
| 2026-11-12 | The conversation view model does not use `ConfigureAwait(false)`. | Its lists are shown directly by the window, and Windows only allows those to change on the interface thread. |
| 2026-11-12 | The "Ask the rider to say hello" menu line and its tests were removed. | It was a stand-in for the conversation window, which now exists. Its cases (reply growing, stopping, empty reply, problems, shutdown) are covered by `ConversationViewModelTests`. |
| 2026-11-12 | Opening the conversation window checks the connection once. | DESIGN.md 5.1 asks for a connection status and current model; one small request when the user opens the window provides both without any background polling. |
| 2026-11-12 | The icon is always present while Desktop Rider runs. The "Minimise to system tray" setting is left for the settings screen in Phase 8. | With no taskbar button, the icon is the only way back to a hidden rider. |
| 2026-11-13 | Jumps and tricks are drawn in a separate click-through window over the rider instead of making the rider's window bigger. | Everything that depends on the rider's window (dragging, the saved position, keeping it on screen, where bubbles go) keeps working unchanged. |
| 2026-11-13 | The bike stays in place during a jump and the ramps move past it. | The window does not have to travel across the desktop, and "a jump that appears" is exactly what is seen. |
| 2026-11-13 | The jump animations are built from rules (ramp shape, flight path, suspension travel) in `Tools/Blender`, not keyed by hand. | Every trick shares the same take-off, flight and landing, so they stay consistent and a new trick is only a new rider pose. |
| 2026-11-13 | Asking for "a trick" without naming one picks one at random in `RiderActions`. | Works even when a small AI model does not choose one itself. |
