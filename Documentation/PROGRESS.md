# Progress

Last updated: 12 November 2026

## Current task

Phase 2, task 1: transparent, frameless overlay window.

## Completed tasks

| Phase | Task | Result |
|---|---|---|
| 1 | Inspect the development environment | Done. .NET SDK 10.0.115 on Linux; WPF compiles there but cannot run. |
| 1 | Create the Windows application | Done. `DesktopRider.exe` (WPF, .NET 10). |
| 1 | Establish the solution structure | Done. Core, App and Core.Tests; see ARCHITECTURE.md. |
| 1 | Establish logging and configuration | Done. Local daily log files and a JSON settings file with repair and backup. |
| 1 | Build and test the application shell | Built and unit tested. Manual check M1 on Windows 11 is awaiting the owner. |
| 3 | Inspect existing reference photographs | Done. Six photographs received; see the visual reference summary. |
| 3 | Organise all supplied references | Done. Stored privately by the owner; folder structure and written summary are in `References/`. |

## Latest results

| Check | Result |
|---|---|
| Build (`dotnet build`, warnings as errors) | Succeeded, 0 warnings, 0 errors |
| Tests (`dotnet test`) | 54 passed, 0 failed, 0 skipped |
| Standalone Windows build (`dotnet publish`, win-x64) | Succeeded |
| Manual check M1 on Windows 11 | Not yet run |

## Remaining tasks

- **Phase 2 – Desktop overlay:** transparent window, always-on-top, dragging, positioning, multiple monitors, system tray.
- **Phase 3 – Visual asset pipeline:** obtain the missing references, concept artwork, sprite conventions, owner approval, sprite rendering.
- **Phase 4 – Animation system.**
- **Phase 5 – Ollama integration.**
- **Phase 6 – Conversation interface.**
- **Phase 7 – Personality and memory.**
- **Phase 8 – Settings and polish.**
- **Phase 9 – Final validation.**

## Blockers

- **Final artwork** cannot start until the owner supplies the missing references and answers the two decisions listed in `References/Annotated-References/VISUAL_REFERENCE_SUMMARY.md`. Software work is not blocked.

## Known issues

- None.

## Technical decisions

Recorded in ARCHITECTURE.md.

## Next task

Phase 2, task 1: transparent, frameless overlay window with a clearly labelled stand-in shape (not character artwork).
