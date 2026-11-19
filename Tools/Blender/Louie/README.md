# Second rider and bike (Rider 2)

`desktop_rider_2.blend` is a copy of Harvey's `Blender/desktop_rider_3d.blend` (built 2026-11-18). Harvey's file was only read.

* Same object, rig and bone names (`race_rider`, `race_bike_youth`, `rider_rig`, `bike_rig`), same meshes, UVs,
  weights, helmet fixes and clip list. `tools/fingerprint.py` confirmed everything matches Harvey except the textures,
  and `trick_check.py` gave identical numbers for Backflip, Whip and Nac Nac, so Harvey's tricks file plays on it as is.
* The bike mesh in this file is its own copy, so it can be remodelled without touching Harvey's bike.
  If the remodel changes the rest shape, re-run the trick checks before exporting.
* Textures: `Textures/DR2_rider_atlas_blank.png` and `DR2_bike_atlas_blank.png` (4096, packed in the .blend). Every
  colourable zone is flat #d9d9d9; rubber, lenses and metal are plain greys. No graphics, names or numbers.
* `Textures/*_zone_guide.png` show each material zone in its own colour (listed in `Textures/build_summary.json`).
* Rebuild: `blender --background Blender/desktop_rider_3d.blend --python "Second Rider/tools/build_blank_model.py" -- <out.blend> <textures dir>`
* Not yet exported to the Unity project.

## Rider kit v2 (2026-11-18)
Painted from Boss's kit photos (References/Original-Photographs/Rider-2/Kit in the project files) by `tools/paint_rider2.py`
(input `tools/rider_tris.npz` from `tools/dump_tris.py`), applied and saved with `tools/apply_atlas.py`.
Atlas: `Work/DR2_rider_atlas_kit_v2.png` (packed in the .blend). Bike still blank. Previews in `Work/v2`.

## Rider kit APPROVED (2026-11-18, Boss)
v3: helmet stripes removed, goggle strap fluo yellow. Locked copies in `Approved/` (atlas + snapshot of the .blend).
Number changed to 11 at Boss's request (v4 atlas, 22:51). Approved/desktop_rider_2_rider_approved_2026-11-18_no11.blend is the current approved snapshot.

## Rider is Louie (Boss, 2026-11-19)
This rider is named **Louie**. Object and bone names stay the same (`race_rider`, `rider_rig` ...) so Harvey's tricks
keep working; the name is for the app and for files from now on.

## Electric bike + final kit LOCKED IN (2026-11-19 15:55, Boss: "lock it in, bike and rider good")
* `tools/electric_motor.py` was applied to `desktop_rider_2.blend` (STOMP-style electric bike: battery, motor, chain on
  the left inside the frame and swingarm, new rear shock, black plastics and swingarm, copper forks with red tops, black
  triple clamps, white 11 on the three plates). Bike atlas: `Textures/DR2_bike_atlas_electric.png` (packed).
* Rider kit v5: O'NEAL stacks moved up the thighs (`tools/paint_rider2.py`), atlas `Textures/DR2_rider_atlas_kit_v5.png`
  (packed), swapped in with `tools/set_rider_atlas.py`.
* Backup of the file before this: `Work/backup/desktop_rider_2_before_electric_20261119.blend`.
  Locked snapshot: `Approved/desktop_rider_2_louie_electric_locked_2026-11-19.blend`.
* `electric_motor.py` must not be run on the locked file again (its checks will stop it); it builds from the pre-electric backup.
* Still to do: the swingarm pivot moved (0.034, 0.361), so re-run the trick checks and export a bike-2 trick FBX before
  Unity; check the down tube against the forks at full steering lock.

## Shock and mud flap moved back (2026-11-19 16:15, Boss)
Shock from inside the box under the seat down to the swingarm; flap hangs flush from the box underside. Rebuilt from
`Work/backup/desktop_rider_2_before_electric_20261119.blend` with `tools/electric_motor.py` + `tools/set_rider_atlas.py`
(v5 rider atlas). The locked file before this change: `Work/backup/desktop_rider_2_locked_before_shockmove_20261119.blend`.
Approved snapshot refreshed. Swingarm pivot swing test: `Work/bike/swingarm_pivot_test.png`.

## Louie in Unity (2026-11-19)
* Trick check (Harvey's `trick_check.py`, read-only on both files): Backflip and Whip give exactly Harvey's numbers
  (hands/feet 0 mm off, same jerk), so the moved swingarm pivot does not change the tricks.
* Exported with `tools/export_louie.py` (copy of Harvey's exporter; writes only to its output folder):
  `Louie.fbx` (meshes + 15 everyday clips), `Louie_Tricks.fbx` (19 tricks), `Louie_Style.fbx` (4 riding-style clips),
  textures `Louie_Bike.png` / `Louie_Rider.png`. Staged in `Work/louie-export`, copied to
  `Desktop Rider 3D/Assets/DesktopRider/Louie/Models`. Command lines in `Work/louie-export/run.bat`.
* Unity: `Assets/DesktopRider/Louie/Editor/LouieImport.cs` (import rules for that folder only) and `LouieViewer.cs`
  (menu Desktop Rider > Louie > Louie Viewer). Louie stands 2 m to Harvey's right as a viewer-only object (never saved
  into the scene); tricks use Harvey's ramp layout and props. Harvey's files, the app code and the sidecar are untouched.
* Not yet a selectable character in the app itself (that needs the app's character list changed).

## Shock: Harvey's look, black, angled, clean mount (2026-11-19 17:03, Boss "yep sweet")
* Harvey's own shock faces (spring, body, eyes) copied from the pre-electric file, moved onto Louie's eyes and painted
  black (spring #2c2c2e). Leans forward along Boss's line: bottom eye (-0.195, 0.3325), top eye (-0.100, 0.598) in the seat box.
* Harvey's lower mount block is not used. Instead: a cross bar between the swingarm arms (inside their height),
  two clevis tabs on it, a bolt, and the shock's own round bottom eye on the bolt. All weighted to the swingarm.
* Main file rebuilt from the pre-electric backup; locked file before this: `Work/backup/desktop_rider_2_locked_before_harveyshock_20261119.blend`.
  Approved snapshot refreshed; `Louie.fbx` re-exported and copied into Unity (tricks/style FBX unchanged: rig unchanged).

## Louie in the app, and several riders at once (2026-11-19)
* "Who rides" has Harvey, Roy/Dad and Louie. Each name is an on/off button: lit = out riding. One, two or all three at once;
  the last one out always stays. Settings: `Appearance.Riders` (list) + `Appearance.Character` (rider in the main window).
* Each extra rider runs in its own small window: a second copy of the program started with `--companion Louie --host <id>`
  (`RiderLineUp`, `CompanionRiders`, `ReadOnlySettingsStore`). Companions have no tray or settings, follow the main
  settings file, never write it, and close when the main program closes or the rider is sent home.
  Their logs: `Player-<Name>.log` beside `Player.log`. Unity's "Force Single Instance" is off; AppHost keeps one main copy.
* Louie: `Assets/DesktopRider/Louie/Models` (style and trick clips merged into his model at setup). Harvey's tricks,
  props, riding and click area; no exhaust smoke (electric).
* Sizes and speeds: back to the originals (Boss reverted the matching attempts, 19 Nov 18:16). Bikes drawn at 1, the
  sidecar at 0.45 with its own speeds; 100 px per metre; Size 0.5 to 2.0. Then (Boss, 18:20) people matched by height: meshes at rest, feet to
  helmet top, Harvey's rider 1.134 m, sidecar driver 1.472 m (helmets 0.264 / 0.347 high agree), so the sidecar is drawn
  at 1.134/1.472 = 0.770 (was 0.45; a skeleton-based 1.115 was wrong). Bikes unchanged at 1. Tricks/room to sort next.
* Sizes approved (Boss 18:34, "do not change their sizes anymore"). Size slider: 0.5 to 1.3 (Boss 19:26, max set where his slider sat). On a screen narrower than
  1920 px (at 100% scaling; the narrowest screen counts) the top size is 1.3 x width/1920, never below 0.5; 1920 and wider stay 1.3
  (AppearanceSettings.MaxScaleFor, applied in AppHost.DrawnSize);
  the sidecar's long tricks (Double, Superman, Rollers, Lost Passenger) are to be shortened by the sidecar thread. Sidecar cruise/acceleration/braking = bikes' / 0.770, turn rate = bikes'
  (110), so all three ride and turn alike on screen; its tricks keep their approved speeds.
* Tests: 409 of 409 pass. Backups of the edited files: `Work/backup/EditorBridge.cs.before_louie`, `AppHost.cs.before_multi`.
* Right-click on any rider opens the settings: a companion sets the named event `DesktopRider.OpenSettings`, which the
  main program waits on (ThreadPool.RegisterWaitForSingleObject, no polling). Each window now finds its own Unity window
  by process and class (`OverlayWindow.FindOwnWindow`), so a companion started while another window has focus still turns
  see-through. "Move to corner" now really goes to the bottom-right of the main screen, companions in a row beside it.
