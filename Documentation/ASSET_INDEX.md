# Asset index

Every piece of artwork in the repository, and whether the owner has approved it.

The approved character designs are the owner's painted 3D models described below. The sprite sheets in `Assets/Sprites/Rider/` are rendered from them.

| File | What it is | Source | Status |
|---|---|---|---|
| `Assets/Concepts/bike-left-v1.svg` | First concept of the bike, left side, standing still. Hand-drawn vector illustration, 1000 × 700 units, transparent background. | Drawn from the owner's left-side bike photograph. Shows the number 28. Sponsor and brand logos are simplified to plain shapes pending the owner's decision. | **Rejected by the owner** (12 Nov 2026): did not look like a motocross bike. Kept for the record. |
| `Assets/Concepts/bike-left-v2.svg` | Second concept of the bike, left side. Vector illustration with a transparent background, levelled so both wheels sit on the ground. | Traced from measurements of the owner's left-side photograph (February 2012 state: gold ignition cover, black rims, gold hubs, as the owner chose). Logos are simplified shapes pending the owner's decision. | **Rejected by the owner** (12 Nov 2026): hand-drawn vector art is not good enough. Kept for the record. |

## How artwork is produced

The artwork route was settled with the owner on 12 November 2026:

1. The owner supplied their own 3D models of the bike and the rider (two separate models, so the rider can get on and off). Their shapes are the owner's and are not to be altered. The one agreed exception is the rider's hands, moved inward onto the grips at the owner's request.
2. Colours and graphics were applied to those models in Blender to match the reference photographs: the pink, sky blue, white, black and orange scheme, the number 28 and the owner's team logo. The owner approved the result as the default models.
3. Both models are rigged and animated in Blender. Sprite frames with transparent backgrounds will be rendered from those animations once the owner approves them.

The models, the Blender files and the reference photographs stay on the owner's computer and are not in this repository.

| Stage | Status |
|---|---|
| Painted default models | **Approved by the owner** (12 Nov 2026) |
| Animations (all thirteen in DESIGN.md 3.6, plus sleep start and end) | **Approved by the owner** (12 Nov 2026) |
| Rendered sprite sheets | Rendered: 15 animations in each direction, 684 frames. See SPRITE_SPECIFICATION.md. |
| Jump, trick and whoop animations (18, listed in SPRITE_SPECIFICATION.md) | Rendered on 13 November 2026 in each direction, 1,080 frames a side. **Awaiting the owner's approval.** Built overnight at the owner's request without a concept review first. |
| Ramps, landing mounds and whoops shown in those animations | **Awaiting the owner's approval.** See "Props" below. |

## Props

The jump and whoop animations show things for the bike to ride over. They are new artwork, made in Blender by the developer, and are not based on any reference photograph:

| Prop | What it is |
|---|---|
| Freestyle take-off ramp | A curved black deck on a plain silver tube frame, with no markings or logos. |
| Dirt take-off, landing mound, whoops | Plain brown dirt shapes with a darker packed surface. |

Their colours and shapes were the developer's choice so that the animations could be built. The owner has not seen them. If the owner has reference photographs of a real ramp or track, or wants different colours, the props can be changed in `Tools/Blender/trick_core.py` and the animations rendered again. Nothing on the rider or the bike was changed or added.

## How the jump and trick sheets are made

`Tools/Blender/trick_animations.py` opens the owner's animated Blender file (which stays on the owner's computer; a copy is in the project's shared files), keys the bike and rider, and renders each frame side-on with a transparent background. `Tools/Blender/pack_sprites.py` packs the frames into sheets. The owner's models are only posed; their shapes, colours and graphics are untouched.

## The in-app stand-in

If the sprite files cannot be found, the application shows a grey card drawn in XAML (`OverlayWindow.xaml`). It is a labelled stand-in, not artwork, and has no asset file.

## Version 2.0: 3D models for Unity

Version 2.0 uses the owner's 3D bike and rider directly in Unity. The models, textures and renders stay on the owner's PC and are not in this repository. Their layout, clips and conventions are in MODEL_SPECIFICATION.md.

| Asset | Source | Status |
|---|---|---|
| `DesktopRider.fbx`: bike and rider meshes, both rigs, 15 clips | The owner's models, with UV maps added. The rider's helmet was reshaped by the owner in Blender. | **Approved by the owner** (13 Nov 2026, after 9-angle renders). Verified: 19 of 19 checks pass. |
| `DesktopRider_Bike.png` | The owner's kit `Harvey28_v2.pdf` fitted to the plastics, with the JS Financial Solutions strips on the swingarm. The rider's name is off the side panels. Hubs in the kit's pink, rims in the kit's blue, and steel brake discs painted from the owner's disc reference photo. | **Approved by the owner** (13 Nov 2026; wheels and discs approved the same evening) |
| `DesktopRider_Rider.png` | The earlier painted look, plus sky blue and pink gloves, the striped helmet top and a pink peak edge, taken from the correct-kit riding photographs | **Approved by the owner** (13 Nov 2026). The helmet's right side is still the earlier look. An update from the photographs of the same helmet model is being prepared for the owner's approval. |
| `Props/DR_Prop_*.fbx` (9) | Rebuilt from the version 1 props as clean single meshes, with the same outlines and colours | Developer-made, as in version 1. Not yet seen by the owner. |
| `DesktopRider_Tricks.fbx` | The 18 jump, trick and whoop animations from `Tools/Blender/trick_animations.py`, on the same two rigs (no meshes), keyed for the bike seen from its right side | Exported 13 Nov 2026. Verified: 17 of 17 checks pass. The animations are the ones built for version 1; how they look in Unity is for the owner to judge. |
