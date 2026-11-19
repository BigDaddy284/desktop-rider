# Desktop Rider 3D (version 2.1) – changed scripts

A copy of the scripts changed in the 3D Unity app (`Assets/DesktopRider/` in the
"Desktop Rider 3D" Unity project, which is kept outside this repository) to add:

- **Who rides** – a choice in Settings between Harvey and Sidecar 41.
- **Riding speed** – a slider in Settings (0.5× to 2×) for how fast the rider rides about.
- A steering fix so a wide-turning outfit (the sidecar) slows for tight turns instead of
  circling its target forever (`RiderSteering.SpeedForTurn`).

Paths mirror `Assets/DesktopRider/`. The sidecar's desktop model and trick layout come from
`Tools/Blender/Sidecar/export_desktop.py`.

Checked on 17 Nov 2026: Unity EditMode tests 392 passed, 0 failed; the Windows build
succeeded with no errors or warnings; the built app ran the sidecar for 3 minutes
with no errors (about 12% of one CPU core, 275 MB).

## 2.2 – lining up for tricks (18 Nov 2026)

The owner's video showed the sidecar crawling round in slow circles near a trick's start, then being snapped
round to face the jump, and setting off at full speed from a standstill. Now:

- The run-up starts three turning circles behind the start (longer for a trick that begins at speed), and the
  last stretch steers along the trick's line, so the rider arrives facing the jump (`RiderSteering.RunUpLength`,
  `LineUpAim`, `SpeedToEnter`).
- He arrives at the trick's own starting speed (`TrickAnimation.EntrySpeed`): Harvey's tricks start from a stop,
  the sidecar's begin moving. A trick that ends moving (`ExitSpeed`) rides straight on afterwards.
- He is no longer turned on the spot: a trick asked for in a different direction is lined up for, and it begins
  facing the way he actually arrived when the trick fits that way.
- The run-up to a trick is ridden at least at normal pace whatever the Riding speed setting.

Checked: Unity EditMode tests 400 passed, 0 failed (including a run-up simulation from 120 starts each for Harvey and
two sidecar tricks); build succeeded; in the built app the sidecar lined up for and rode Double, Wheelie and NoHander
on request, and Harvey Backflip and Whoops, with no errors.

## 2.3 – planned route to tricks, no turning on the spot (18 Nov 2026)

A frame-by-frame recording of the app's own window (`ProofRecorder`, checks only) showed the sidecar still slowing
to under 2 m/s for tight turns on the way to a trick, the leg-out corner pose fully out at the slightest wobble and
snapping off as a trick began, and a full loop when a trick went roughly the way he was already riding. Now:

- The way to a trick is planned once (`RunUpPath`: turn, straight, turn, with turns 1.2 times the tightest turn at
  cruising speed) and followed at cruising speed. Simulated from 900 starts: none slowed below 0.6 of cruising speed,
  every one arrived within 6 degrees of the trick's heading.
- A trick that goes roughly the way he is riding starts further on, so he does not loop back.
- A trick begins facing the way he arrived, turned at most 4 degrees; if it does not fit that way it is given up.
  (Boss saw the bike jump from facing north to facing west as a trick began.)
- The corner pose is measured against the same lean for every rider and fades out over a fifth of a second into a trick.
- `size.txt` beside a proof build sets the rider size for a test run.

Checked: Unity tests 403 passed, 0 failed; build succeeded; recordings of the built app at normal and largest size
showed no heading jumps of more than 5 degrees in a frame and no slowing on the way to tricks.

## 2.4 – the sidecar is called Roy/Dad (19 Nov 2026)

At the owner's request the sidecar's name on the "Who rides" choice is now **Roy/Dad**. The saved setting keeps its
old value (`Sidecar41`), so a sidecar already chosen stays chosen. Unity tests 403 passed, 0 failed; build clean.
