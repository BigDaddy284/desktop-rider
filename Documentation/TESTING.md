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
| Character placement | `PlacementCalculatorTests` | Default corner, restoring a saved spot, negative-coordinate monitors, unplugged monitors, resolution changes, nudging back on screen, bad saved numbers. |
| Settings in use | `SettingsServiceTests` | Changes are applied, saved, kept in range and announced; nothing is announced when a save fails. |
| Character window logic | `OverlayViewModelTests` | Always-on-top toggle, showing and hiding the rider, menu actions, behaviour when a save fails. |
| Notification-area icon logic | `TrayViewModelTests` | The menu while the rider is showing and while hidden, show, hide and exit, the tick on Always on top, what a click does, the hover text, and the reminder (once ever on first hide, every time on a hidden start, never twice). |
| Icon picture layout | `IconLayoutTests` | Finding the drawn part of a frame and fitting it into a square without distortion. |
| Sprite list | `SpriteManifestTests` | Reading the list, frame positions on a sheet, twelve kinds of broken list, and the real rider list covering every animation DESIGN.md requires. |
| Animation playback | `AnimationPlayerTests` | Loops, one-shots, returning to the right loop, waiting requests, priorities, lead-in and lead-out animations, speed, long pauses. |
| Idle animations | `IdleVariationPickerTests` | Timing, switched off, not interrupting other animations. |
| Wiring | `CompositionTests` | Services resolve and work together; single-instance guard. |

Code coverage has not been measured. The requirement is that all applicable tests pass, and they do.

## Known limitations

- Development happens on a Linux build machine. It compiles the WPF application but cannot run it, so anything involving a real window is verified by GitHub Actions on Windows (build only) and by hand on the owner's Windows 11 PC.
- Loading the sprite pictures and drawing them (`RiderSprite`) needs a real window, so it is covered by manual procedure M3.
- There are no automated tests of the WPF window itself. Its decisions (where to sit, what the menu does) are made by tested code in Core; the thin window layer that carries them out (`OverlayWindow`, `WindowPlacementController`, `TrayIconController`, `TrayIconFactory`) is covered by the manual procedures below.

- Multiple monitors (M2 steps 9 and 10) cannot be checked by hand: the owner has one ultrawide monitor. `PlacementCalculatorTests` covers those rules automatically.

## Manual test procedures

### M1. Start and stop (Phase 1)

1. Double-click `DesktopRider.exe`. **Expect:** the stand-in card appears near the bottom-right corner of the main screen.
2. With it still running, double-click `DesktopRider.exe` again. **Expect:** a message saying Desktop Rider is already running; no second card.
3. Right-click the card and choose **Exit Desktop Rider**. **Expect:** it disappears and `DesktopRider.exe` is gone from Task Manager.
4. Open `%LOCALAPPDATA%\DesktopRider\Logs`. **Expect:** a log file for today with a "starting" line and a "closing with exit code 0" line.

Status: **awaiting the owner's check on Windows 11.**

### M2. Overlay window (Phase 2: transparency, always-on-top, dragging, positioning)

1. Look at the card. **Expect:** no window frame, title bar or background rectangle; the desktop shows around its rounded corners. No taskbar button.
2. Click on the desktop or a window just outside the card's edge. **Expect:** the click goes to whatever is behind; the empty margin does not block it.
3. Open another application and drag it over the card. **Expect:** the card stays on top.
4. Right-click the card and choose **Always on top** to untick it, then click another window that overlaps it. **Expect:** the card now goes behind. Tick it again. **Expect:** it comes back on top.
5. Drag the card somewhere else. **Expect:** it follows the mouse smoothly.
6. Drag it so it hangs partly off the edge of the screen and let go. **Expect:** it hops back fully onto the screen.
7. Exit and start Desktop Rider again. **Expect:** the card appears where it was left, with the always-on-top choice remembered.
8. Right-click and choose **Move back to the corner**. **Expect:** it returns to the bottom-right corner of the main screen.
9. If a second monitor is available: drag the card onto it, exit, restart. **Expect:** it reappears on the second monitor. Then exit, unplug that monitor and restart. **Expect:** it appears in the corner of the main screen.
10. If the monitors use different scaling: drag the card between them. **Expect:** it stays sharp and a sensible size on both.

Status: **awaiting the owner's check on Windows 11.**

### M3. The animated rider (Phases 3 and 4)

1. Start Desktop Rider. **Expect:** the rider on the bike appears, facing right, gently moving. No grey card, no background rectangle.
2. Click just above the seat or between the wheels, where there is only desktop showing. **Expect:** the click goes to whatever is behind.
3. Watch for half a minute. **Expect:** now and then the rider looks around, then settles again.
4. Right-click the rider, choose **Do something**, then **Wheelie**. **Expect:** the front wheel lifts and comes back down without the picture jumping.
5. Try **Thinking**, **Celebration**, **Revving** and **Talking**. **Expect:** each plays smoothly; choosing **Idle** stops the looping ones.
6. Choose **Sleeping**. **Expect:** the rider slumps forward and stays asleep. Choose **Idle**. **Expect:** the rider sits back up before returning to rest.
7. Choose **Riding loop**. **Expect:** the bike pulls away on the spot with the wheels spinning. Choose **Idle**. **Expect:** it brakes and settles.
8. Choose **Turn around**. **Expect:** the bike faces left, the number 28 still reads the right way round, and the left side is the pink one.
9. Drag the rider while it is animating. **Expect:** it moves as before and keeps animating.
10. Open Task Manager and watch Desktop Rider for a minute while it rests. **Expect:** processor use stays low (a few percent at most).

Status: the owner ran the 0.3.0 test build on 12 November 2026 and reported that it looks and works well. The steps were not reported one by one, so steps 2, 9 and 10 (click-through, dragging while animating, processor use) are still to be confirmed individually.

### M4. Icon beside the clock (Phase 2: system tray controls)

1. Start Desktop Rider and look beside the clock (click the small arrow if the icon is not in view). **Expect:** a Desktop Rider icon that is a tiny picture of the rider and bike. Resting the mouse on it shows "Desktop Rider".
2. Right-click the icon. **Expect:** a menu with **Hide the rider**, **Always on top** (ticked), **Move back to the corner** and **Exit Desktop Rider**.
3. Choose **Hide the rider**. **Expect:** the rider disappears. The first time ever, a short message says the rider is hidden and Desktop Rider is still running.
4. Rest the mouse on the icon. **Expect:** "Desktop Rider (hidden)". Right-click it. **Expect:** the first item now reads **Show the rider**.
5. Choose **Show the rider**. **Expect:** the rider comes back in the same place, still animating.
6. Click the icon once with the left button. **Expect:** the rider hides. Click again. **Expect:** it comes back. No message this time.
7. Right-click the rider itself and choose **Hide the rider**. **Expect:** it hides, the same as from the icon.
8. With the rider hidden, open Task Manager and watch Desktop Rider for a minute. **Expect:** processor use at or very near 0%.
9. With the rider hidden, right-click the icon and choose **Exit Desktop Rider**. **Expect:** the icon disappears straight away and `DesktopRider.exe` is gone from Task Manager.
10. Start Desktop Rider again. **Expect:** the rider stays hidden as it was left, the icon is there, and the message appears to say it is running. Clicking the message or the icon brings the rider back.
11. With Desktop Rider running, double-click `DesktopRider.exe` again. **Expect:** a message that it is already running, which explains how to bring the rider back from the icon.
12. Untick **Always on top** from the icon's menu. **Expect:** the same effect as from the rider's own menu, and the tick matches in both menus.

Status: the owner ran the 0.4.0 build on Windows 11 on 12 November 2026 and reported that it all looks and works well. The steps were not reported one by one.
