# Architecture

Desktop Rider is a native Windows application written in C# on .NET 10 with WPF, following DESIGN.md section 8.

## Projects

| Project | Target | Purpose |
|---|---|---|
| `src/DesktopRider.Core` | `net10.0` | Everything that does not need Windows: settings, file locations, logging, shared service registration. Fully unit tested. |
| `src/DesktopRider.App` | `net10.0-windows` | The WPF application: start-up, shutdown, windows and view models. Produces `DesktopRider.exe`. |
| `tests/DesktopRider.Core.Tests` | `net10.0` | xUnit tests for Core. |

The remaining projects named in DESIGN.md (`DesktopRider.AI`, `DesktopRider.Animation`, `DesktopRider.Desktop` and their tests) are added when their phase begins, so the solution never contains empty projects.

The rule for where code goes: if it can work without a window, it lives outside the App project so that it can be tested automatically on any machine.

## Start-up and shutdown

1. `App.OnStartup` takes the single-instance guard. A second copy explains that the rider is already running and exits.
2. The local data folder is created and services are registered through `AddDesktopRiderCore`.
3. Error handlers are attached so an unexpected problem is logged and does not close the rider.
4. Settings are loaded without blocking the interface, then the main window is shown.
5. `App.OnExit` logs the exit, detaches handlers and disposes the services and the guard.

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
| 2026-11-12 | Original reference photographs are not committed. | The repository is public and the photographs are personal. Owner's decision. |
