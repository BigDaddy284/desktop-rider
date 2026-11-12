# Progress

Last updated: 12 November 2026

## Current task

Phase 2, task 5: multiple monitor support verification, then task 6: system tray controls.

## Completed tasks

| Phase | Task | Result |
|---|---|---|
| 1 | Inspect the development environment | Done. .NET SDK 10.0.115 on Linux; WPF compiles there but cannot run. |
| 1 | Create the Windows application | Done. `DesktopRider.exe` (WPF, .NET 10). |
| 1 | Establish the solution structure | Done. Core, App and Core.Tests; see ARCHITECTURE.md. |
| 1 | Establish logging and configuration | Done. Local daily log files and a JSON settings file with repair and backup. |
| 1 | Build and test the application shell | Built and unit tested. Manual check M1 on Windows 11 is awaiting the owner. |
| 2 | Create transparent window | Built. Manual check M2 awaiting the owner. |
| 2 | Implement always-on-top | Built and unit tested (menu toggle, remembered). Manual check M2 awaiting the owner. |
| 2 | Implement dragging | Built. Manual check M2 awaiting the owner. |
| 2 | Implement desktop positioning | Built and unit tested (placement rules). Manual check M2 awaiting the owner. |
| 3 | Inspect existing reference photographs | Done. 24 photographs received in two batches; see the visual reference summary. |
| 3 | Organise all supplied references | Done. Stored privately by the owner; folder structure and written summary are in `References/`. |

## Latest results

| Check | Result |
|---|---|
| Build (`dotnet build`, warnings as errors) | Succeeded, 0 warnings, 0 errors |
| Tests (`dotnet test`) | 90 passed, 0 failed, 0 skipped |
| GitHub Actions build and test on Windows | Passed for Phase 1; runs again on each push |
| Standalone Windows build (`dotnet publish`, win-x64) | Succeeded |
| Manual checks M1 and M2 on Windows 11 | Not yet run |

## Remaining tasks

- **Phase 2 – Desktop overlay:** multiple monitor verification, system tray controls. (Placement rules for multiple monitors are already built and unit tested.)
- **Phase 3 – Visual asset pipeline:** obtain the missing references, concept artwork, sprite conventions, owner approval, sprite rendering.
- **Phase 4 – Animation system.**
- **Phase 5 – Ollama integration.**
- **Phase 6 – Conversation interface.**
- **Phase 7 – Personality and memory.**
- **Phase 8 – Settings and polish.**
- **Phase 9 – Final validation.**

## Blockers

- **Final artwork** cannot start until the owner supplies the remaining references (bike rear, helmet side, gloves, bike model) and answers the two decisions listed in `References/Annotated-References/VISUAL_REFERENCE_SUMMARY.md`. Software work is not blocked.

## Known issues

- The window has never been run by the developer, because the build machine is Linux. Everything visual depends on manual checks M1 and M2.
- With no tray icon yet, the only way to close the application is the right-click menu on the character.

## Technical decisions

Recorded in ARCHITECTURE.md.

## Next task

Phase 2, task 6: system tray controls (show, hide, exit).
