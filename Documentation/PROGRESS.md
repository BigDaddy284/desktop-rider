# Progress

Last updated: 12 November 2026

## Current task

Phase 2, task 6 (system tray controls) is built and unit tested. It is waiting for the owner's manual check M4 on Windows 11.

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
| 2 | Implement system tray controls | Built and unit tested: icon beside the clock with show, hide, always on top, move back to the corner and exit; hiding is remembered. Manual check M4 awaiting the owner. |
| 3 | Inspect existing reference photographs | Done. 28 photographs received in three batches; see the visual reference summary. |
| 3 | Character designs | Done. The owner supplied their own 3D models; colours and graphics were applied to match the photographs and the owner approved them. |
| 3 | Sprite conventions and rendering | Done. 15 animations rendered in both directions; see SPRITE_SPECIFICATION.md. |
| 4 | Animation playback, transitions, priorities | Built and unit tested (`DesktopRider.Animation`). |
| 4 | Initial animations | All thirteen from DESIGN.md 3.6 built in Blender and approved by the owner, plus sleep start and end. |
| 4 | Animated rider in the window | Built. The owner ran it on Windows 11 and reported it looks and works well; see M3 in TESTING.md. |
| 3 | Organise all supplied references | Done. Stored privately by the owner; folder structure and written summary are in `References/`. |

## Latest results

| Check | Result |
|---|---|
| Build (`dotnet build`, warnings as errors) | Succeeded, 0 warnings, 0 errors |
| Tests (`dotnet test`) | 177 passed, 0 failed, 0 skipped |
| GitHub Actions build and test on Windows | Passed for Phase 1; runs again on each push |
| Standalone Windows build (`dotnet publish`, win-x64) | Succeeded |
| Manual checks on Windows 11 | The owner ran the 0.3.0 test build: it starts, shows the animated rider and works. M1 to M3 not yet reported step by step. |

## Remaining tasks

- **Phase 2 – Desktop overlay:** multiple monitor verification by the owner. (Placement rules for multiple monitors are already built and unit tested.)
- **Phase 4 – Animation system:** connect animations to behaviour (moving around the screen, reacting to the user) as those features arrive.
- **Phase 5 – Ollama integration.**
- **Phase 6 – Conversation interface.**
- **Phase 7 – Personality and memory.**
- **Phase 8 – Settings and polish.**
- **Phase 9 – Final validation.**

## Blockers

- None.

## Known issues

- The window has never been run by the developer, because the build machine is Linux. Everything visual depends on manual checks M1 and M2.
- The icon beside the clock is a shrunken picture of the whole rider and bike. At the smallest size it may be hard to make out; the owner is to judge it in M4.
- The rider's sprite pictures are not in this branch. The owner has agreed they can go in the repository (real-life photographs must not); they are being added from the animation work.

## Technical decisions

Recorded in ARCHITECTURE.md.

## Next task

Phase 5: Ollama integration, once the owner has checked M4.
