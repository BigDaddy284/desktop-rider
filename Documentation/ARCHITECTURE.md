# Architecture

Desktop Rider is a native Windows application written in C# on .NET 10 with WPF, following DESIGN.md section 8.

## Projects

| Project | Target | Purpose |
|---|---|---|
| `src/DesktopRider.Core` | `net10.0` | Everything that does not need Windows: settings, file locations, logging, shared service registration. Fully unit tested. |
| `src/DesktopRider.Desktop` | `net10.0-windows` | Windows desktop integration: the monitor list and moving the character's window in physical pixels. |
| `src/DesktopRider.App` | `net10.0-windows` | The WPF application: start-up, shutdown, windows and view models. Produces `DesktopRider.exe`. |
| `src/DesktopRider.Animation` | `net10.0` | The sprite list and the animation player: which frame of which animation shows at any moment. No window, no timer, fully unit tested. |
| `tests/DesktopRider.Core.Tests` | `net10.0` | xUnit tests for Core. |
| `tests/DesktopRider.Animation.Tests` | `net10.0` | xUnit tests for Animation. |

The remaining projects named in DESIGN.md (`DesktopRider.AI` and its tests) are added when their phase begins, so the solution never contains empty projects.

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

Positions are measured and saved in physical screen pixels through the Windows API, not in WPF units, so they mean the same thing on monitors with different scaling.

Until the artwork is approved, the window shows a neutral grey stand-in card that says so. It is not character artwork.

## Animation

`AnimationPlayer` (Animation project) is told how much time has passed and answers with the current animation and frame. It knows the rules in SPRITE_SPECIFICATION.md: loops, one-shots, lead-in and lead-out animations, priorities and speed. It never touches the AI, so the rider keeps moving whatever the AI is doing.

`RiderSprite` (App project) is the thin Windows layer around it. It owns one timer that ticks at the sprites' frame rate, cuts the current frame out of its sheet and hands it to the window. The timer stops while the character is hidden. If the sprite files are missing or damaged the window shows a plain stand-in card instead of failing, and the reason is logged.

`IdleVariationPicker` asks for a small idle animation every 8 to 20 seconds while the rider is resting, unless idle animations are switched off in settings.

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
