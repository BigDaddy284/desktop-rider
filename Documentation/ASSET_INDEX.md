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

## Second character set: sidecar outfit 41

A separate set from the rider and bike above, with its own driver, passenger and tricks. Nothing in the first set changes.

| Stage | Status |
|---|---|
| Models (`sidecar.glb`, `sidecar_crew.glb`) | Supplied by the owner on 16 Nov 2026. Their shapes are the owner's and are not altered. At the owner's request, the three black vents on the chair's front board are hidden (not deleted). |
| Reference photographs | 12 photographs supplied on 16 Nov 2026 (project files, `References/Original-Photographs/Sidecar/`). Summary in `References/SIDECAR_VISUAL_REFERENCE_SUMMARY.md`. |
| Painted models | **Approved by the owner** (16 Nov 2026) after six rounds of changes. White, blue and yellow O'Neal crew kit; yellow and light-blue outfit with the number 41; Duck Smart, HB Halo, O'Neal and YSS stickers redrawn by hand from the photos. Approved renders: `References/Approved-Artwork/Sidecar-41/`. |
| Rigging and animations | First pass built 16 Nov 2026 and set up in the owner's Unity project for viewing; **awaiting the owner's review**. 28 clips (see below). |
| Sprite sheets | Not needed for the 3D (Unity) version. |

The painting follows the same method as the first set: images projected onto the models from the side, front and back. The driver and passenger are the same mesh in two poses, so the passenger takes the driver's rest coordinates per face corner and both wear exactly the same artwork. The Blender file (`sidecar_41_approved.blend`, with its `tex/` folder) is on the owner's computer in `Dektop Rider 3D\Sidecar`, and the scripts that build it are in the project files under `sidecar/blender/`.

### Sidecar motions (second pass, 17 Nov 2026)

Built by `Tools/Blender/Sidecar/` from the approved painted file: `bake.py` (joins the outfit's parts and bakes the approved paint into three textures), `rig.py` (outfit, driver and passenger rigs), `scrig.py` (pose engine), `poses.py` (crew pose library), `sidecar_anim.py` (keys every clip and checks the wheels), `props.py` (dirt props), `export.py` (FBX, props and manifest for Unity), `sheet.py` (contact sheets) and `bodycheck.py` (checks the crew against the bike and each other).

The owner rejected the first pass (16 Nov 2026): the passenger's arm, the corner poses (an arm through the head), the transitions, the wheelie, lost passenger, slow rollers and small, glitchy jumps. In the second pass:

- Every crew pose is built on the rig in `poses.py` and matched to the owner's 12 race photos, his photo as passenger, and a pro race video he supplied on 17 Nov 2026 (`My video (2).mp4` in the project files, mirrored so the chair is on the left like his). The model's own Left / Right / Jump shapes stay at zero because blending them moved limbs through the body.
- Riding crouch: low and forward over the rail, head down between the hands, elbows out wide (owner's photo). Hang-out (turning towards the chair, owner's photo and annotated sheet 17 Nov 2026): boots flat on the outer edge of the chair floor just ahead of the chair wheel, squatting with thighs level and the backside out over the chair wheel (a deeper squat folds this model's legs into the body, rejected by the owner in Unity), body leaning forward, both hands on the bar. Turning away from the chair (owner's photo 17 Nov 2026): the passenger sits sideways on the back of the seat behind the driver, legs hanging down into the chair, left hand on the rail and right arm round the driver's waist. This replaces the earlier stand-and-lean swing-over taken from the pro race video. The moves between poses go body first and hands after; the feet step across rather than slide, the body goes forward and over clear of the chair wheel when hanging out, and up and back behind the driver when moving onto the seat. Comparison: `/mnt/project-files/sidecar/concept/corners-vs-photos-v1.jpg`.
- Jumps (owner's notes 17 Nov 2026): NacNac removed. Tabletop replaced by Double: a big double with no trick, 7.9 m gap from the 1 m lip to its own landing `SC_Prop_DoubleLanding` (1 m high, 4.5 m far slope), taken at 13 m/s (about 29 mph), about a second in the air. The landing is placed from the simulated flight so the back wheel clears the top and touches down on the far slope.
- Celebration: both throw the left fist up together. Wheelie: the passenger stands with arms straight. Lost passenger: thrown out, dragged hanging on with the legs trailing behind the chair, then climbs back in.
- Speeds and physics are real: gravity 9.81 m/s², riding at 6.3 m/s (about 14 mph), rollers at 7.5 m/s with throttle on each face and the back tyre spinning up in the air, jumps at 8.8 m/s (about 20 mph) for about 0.85 s in the air as in the video. In the air the outfit keeps its nose up and both crew stand tall; the back wheel lands first on the down slope. Each axle flies off crests and lips under gravity and lands with a squash.
- Checks on every frame of every clip: no wheel sinks more than 9 mm (tyre squash) into the ground, rollers or jumps; the driver and passenger never pass through each other; contact with the bike stays at the level the model has as built (hands round the rail, boots on the platform, driver on the seat).

| Group | Clips |
|---|---|
| Everyday | Idle, LookAround, Revving, Thinking, Talking, Celebration, SleepStart, Sleeping, SleepEnd |
| Riding | RideOff, RideOn, RidingLoop, CornerLeft (towards the chair: passenger hangs out off the chair edge), CornerRight (away from the chair: passenger sits behind the driver), and In/Out moves for each corner. The hang-out and swing-over are part of turning, not separate tricks. |
| Tricks | FlyingChair, Wheelie, PowerSlide, PassengerWave, LostPassenger, Rollers. No stoppie (owner's request). |
| Jumps | Double (big double, no trick), NoHander and PassengerSuperman (on the big double, pose held about 0.6 s). Double and PassengerSuperman approved by the owner 17 Nov 2026. |

Props: `SC_Prop_JumpRamp` (1.0 m lip at 28°, 3.8 m long), `SC_Prop_JumpLanding` (1.0 m high, 5.5 m long, about 2 m gap from the lip), and `SC_Prop_Rollers` (5 rollers, 3.0 m long, 0.55 m high, 1.4 m of flat between, 2 m wide).

Unity: `Assets/DesktopRider/Sidecar/` in the owner's project (model, textures, manifest, props, `SidecarViewer.unity`). Copies of its scripts are in `Tools/Unity/Sidecar/`. Menu **Desktop Rider > Sidecar > Open the sidecar viewer**, then Play and pick a move from the panel; or, without Play, **Desktop Rider > Sidecar > Move Viewer** to pick a move and step through it in the Scene view. The riding demo uses the riding speed and turn size from the manifest. Harvey's scene, models and scripts are not touched. The FBX and textures stay off GitHub like the first set's.
