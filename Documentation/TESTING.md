# Testing

## Commands

Run from the repository root.

| Purpose | Command |
|---|---|
| Build everything (warnings are errors, analyzers on) | `dotnet build` |
| Run all automated tests | `dotnet test` |
| Run one test class | `dotnet test --filter FullyQualifiedName~JsonSettingsStoreTests` |
| Make a standalone Windows build | `dotnet publish src/DesktopRider.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish` |

Every push and pull request is also built and tested on Windows by GitHub Actions (`.github/workflows/ci.yml`).

## What is covered automatically

| Area | Test class | Covers |
|---|---|---|
| Settings defaults and repair | `AppSettingsTests` | Defaults match DESIGN.md; missing sections, out-of-range numbers, bad addresses and unusable positions are repaired. |
| Settings file | `JsonSettingsStoreTests` | Round trip of every value, missing file, damaged file and its backup, hand-edited files, overwrite, simultaneous saves, cancellation. |
| File locations | `AppPathsTests` | Paths stay inside the data folder; folders are created; version is available. |
| Logging | `FileLoggerProviderTests` | Line format, exception details, level filtering, a new file each day, old-file clean-up, many threads at once, an unusable folder, use after shutdown. |
| Wiring | `CompositionTests` | Services resolve and work together; single-instance guard. |

Code coverage has not been measured. The requirement is that all applicable tests pass, and they do.

## Known limitations

- Development happens on a Linux build machine. It compiles the WPF application but cannot run it, so anything involving a real window is verified by GitHub Actions on Windows (build only) and by hand on the owner's Windows 11 PC.
- There are no automated tests of the WPF window itself yet.

## Manual test procedures

### M1. Application shell (Phase 1)

1. Double-click `DesktopRider.exe`. **Expect:** a small "Desktop Rider" window with the version number and the message "Welcome! Standard settings are in use."
2. With it still open, double-click `DesktopRider.exe` again. **Expect:** a message saying Desktop Rider is already running; no second window.
3. Close the window. **Expect:** it closes immediately and `DesktopRider.exe` is gone from Task Manager.
4. Open `%LOCALAPPDATA%\DesktopRider\Logs`. **Expect:** a log file for today with a "starting" line and a "closing with exit code 0" line.

Status: **awaiting the owner's check on Windows 11.**
