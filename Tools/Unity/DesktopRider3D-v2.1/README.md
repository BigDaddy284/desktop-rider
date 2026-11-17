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
