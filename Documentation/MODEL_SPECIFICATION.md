# 3D model specification (version 2.0)

This replaces SPRITE_SPECIFICATION.md for the Unity version. The sprite specification still describes the version 1 sprites.

Version 2.0 shows the owner's own 3D bike and rider in Unity instead of pre-rendered sprites. The models, their textures and every render of them stay on the owner's PC. They are not in this repository (owner's decision, 13 November 2026; see `.gitignore`). Only the scripts that build and check them are here.

## Where the files are on the owner's PC

| What | Where |
|---|---|
| Master Blender file (never changed) | `D:\AI\Claude\Desktop Rider\blender\desktop_rider_animated.blend` |
| Working Blender file (UV maps, textures, helmet changes; still adjustable) | `D:\AI\Claude\Dektop Rider 3D\Blender\desktop_rider_3d.blend` |
| Unity models | `Assets/DesktopRider/Models/` in the Unity project `D:\AI\Claude\Dektop Rider 3D\Desktop Rider 3D` |
| Working files, renders, backups of earlier textures | `D:\AI\Claude\Dektop Rider 3D\kit-fit-work\` |

## Files in `Assets/DesktopRider/Models/`

| File | Contents |
|---|---|
| `DesktopRider.fbx` | Meshes `race_bike_youth` (8,444 vertices, 4,540 faces) and `race_rider` (6,726 vertices, 3,596 faces); rigs `bike_rig` (11 bones) and `rider_rig` (14 bones); 15 clips. |
| `Textures/DesktopRider_Bike.png` | 4096 × 4096 bike texture, with the owner's race-kit graphics. Referenced by the FBX, not embedded. |
| `Textures/DesktopRider_Rider.png` | 4096 × 4096 rider texture. Referenced by the FBX, not embedded. |
| `DesktopRider_Tricks.fbx` | The same two rigs (no meshes) with the 18 trick clips. |
| `DesktopRider_Tricks_manifest.json` | Its bones, and each trick's frame range, stage and timing values. |
| `DesktopRider_model_manifest.json` | Counts, UV and material names, bones and parents, clip names and frame ranges, texture names. |
| `Props/DR_Prop_*.fbx` (9 files) | Static ramp, landing and whoops meshes with flat colours. |
| `Props/props_layout.json` | For each jump or trick: which props, where they sit relative to the bike's start, and how far the bike travels each frame. |

## Conventions

- Units are metres, Y is up, 24 frames per second. The Blender scene is Z up with +X forward and +Y on the bike's left. The export converts this and applies the transforms.
- Each mesh has one UV map, `UVMap`, and one texture. The bike's kit panels have undistorted islands, each projected flat along its own panel. Everything else uses an automatic layout.
- Materials keep their Blender names with `_UV` added, for example `Plastics_UV`. There are 23 on the bike and 15 on the rider.
- All bones deform. The rider's IK on forearms and shins is baked to plain keys on every frame, so Unity needs no IK to play the clips.
- **The rider rig is not nested under the bike.** In Blender, `rider_rig` is parented to the `chassis` bone of `bike_rig`. Blender cannot write that nesting in a form that can be read back, so in the FBX `rider_rig` is a separate root whose transform is keyed every frame to follow `chassis`. Both rigs must play the same clip at the same time. To move the bike and rider around the screen, put both rig roots under one parent object and move the parent.

## Clips (`DesktopRider.fbx`)

Idle, IdleVariation, LookLeft, LookRight, Revving, Thinking, Talking, Wheelie, Celebration, SleepStart, Sleeping, SleepEnd, RideOff, RidingLoop, RideOn. Their frame ranges are in the `DR_animations.json` text block of the Blender file and in the manifest. They are the same 15 animations the owner approved for version 1.

## Clips (`DesktopRider_Tricks.fbx`)

Jump, Whip, Scrub, Backflip, Superman, SupermanSeatGrab, NoHander, NoFooter, Nothing, HeelClicker, CanCan, NacNac, LazyBoy, OneHander, IndianAir, RockSolid, Whoops, Stoppie. They come from `Tools/Blender/trick_animations.py`, keyed in memory on the working file, with IK baked. Each take starts at frame 0 and none loops.

- **Side.** The script keys gestures (one hand off, a foot off) on the side nearest the camera. This file is keyed for the bike seen from its right. A left-side file can be made with `side=Left` if Unity ever shows the bike from the left.
- **Travel.** As in version 1, the bike stays in place and the ground moves under it. The forward distance per frame is `travel` in `Props/props_layout.json`. Add it to the parent object each frame, or move the props back by it.
- Both rigs must play the same take together, as in the main file.

## Graphics on the bike

The kit is the owner's vector design, `Harvey28_v2.pdf`. It is fitted to the plastics as the owner approved on 13 November 2026:

| Panel | Graphic |
|---|---|
| Rear side panels | The kit's side art, with the large 28 on each side number plate, moved 4 cm forward at the owner's request. The rider's name was removed from the side panels at the owner's request. |
| Rear mudguard | The tail of the kit with its sponsor logos on top. Its thin side faces are white with the stripes. |
| Radiator shrouds | Pink on the left, blue on the right, fitted to height and aligned at the front. |
| Front number plate | Name across the top, 28 in the middle, team logo at the bottom. Fitted to width, with white above and below. |
| Front mudguard | The rhino logo on top, pointing forward. |
| Swingarm | Black, with the JS Financial Solutions strips: pink on the left, blue on the right, each running along the arm's centre line (18.25°). |
| Rear shock spring | Steel grey. |
| Wheels | Hubs in the kit's pink (237, 26, 121), rims in the kit's blue (39, 170, 225). |
| Brake discs | Brushed steel, painted from the owner's disc photo: a darker carrier with 6 black bolts, cut-out windows, slots and a wavy edge. Front disc on the left, rear on the right. The mesh discs are round, so the wave and the cut-outs are painted, not cut. |
| Tank cover | No kit piece, so it keeps the earlier painted look. |

## Bike shape change (owner's request, 13 November 2026)

The big upper fork tubes stopped 6.8 cm above the pink fork lowers, leaving a thin section showing. At the owner's request the bottom end of each upper tube (32 vertices per leg, 64 in all) was moved 72.4 mm down the fork axis, so the tube now meets the lower. Vertex and face counts, UVs and weights (100% `steer`) are unchanged. Everything else on the bike is identical to the master. In a full wheelie the fork extends and a short piece of the inner tube shows again, as on a real fork. Then, also at the owner's request, the fork was made to read as grey fork legs running down to the axle with a pink guard on the front only. This was done in the texture: the inner lower legs and the back half of each guard shell are painted the tube's grey (201, 203, 206). Stretching the upper tube to the axle was not done, because the upper tube moves with the steering and the lower legs slide with the suspension, so a rigid tube across that joint would poke out or gap whenever the fork moves. The list of moved vertices is `kit-fit-work\export\bike_changed_vertices.json` on the owner's PC, and their original positions are in `fork_original_vertices.json` beside it.

## Rider changes approved on 13 November 2026

- Helmet reshaped by the owner in Blender: about 69% of its original width and 4% longer. The mouth guard was resized, and its cheek ends and the black rim were brought flush with the shell. Only the 1,894 helmet vertices moved. Everything else is identical to the master.
- Gloves are sky blue and pink, with black palms and a darker blue cuff.
- Helmet top has stripes radiating back from the peak in sky blue, yellow, pink and grey, with thin black lines. The peak's edge is pink.
- No chest protector.

## Building and checking

| Script | What it does |
|---|---|
| `Tools/Blender/build_unity_props.py` | Rebuilds the nine props as single closed meshes, with the same outlines as the version 1 props, and exports them with `props_layout.json`. |
| `Tools/Blender/export_models.py` | Exports `DesktopRider.fbx`, the textures and the manifest from the working Blender file without saving it. With `tricks=<Tools/Blender folder> side=Right name=DesktopRider_Tricks meshes=0` it exports the trick clips instead. The repository copy has three unused lines removed and writes only the file name of the Blender file into the manifest. |
| `Tools/Blender/verify_tricks.py` | Re-keys the tricks in memory and compares `DesktopRider_Tricks.fbx`, and an export of the same with meshes, against them. Exits with status 1 if any check fails. |
| `Tools/Blender/verify_models.py` | Re-imports the exported FBX files into an empty Blender and checks them against the working and master Blender files. Exits with status 1 if any check fails. See TESTING.md. |
