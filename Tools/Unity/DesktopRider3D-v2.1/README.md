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
