# Asset index

Every piece of artwork in the repository, and whether the owner has approved it.

No file in the repository is approved artwork yet. The approved character designs are the owner's painted 3D models described below; sprite frames rendered from them will be listed here when they are added.

| File | What it is | Source | Status |
|---|---|---|---|
| `Assets/Concepts/bike-left-v1.svg` | First concept of the bike, left side, standing still. Hand-drawn vector illustration, 1000 × 700 units, transparent background. | Drawn from the owner's left-side bike photograph. Shows the number 28. Sponsor and brand logos are simplified to plain shapes pending the owner's decision. | **Rejected by the owner** (12 Nov 2026): did not look like a motocross bike. Kept for the record. |
| `Assets/Concepts/bike-left-v2.svg` | Second concept of the bike, left side. Vector illustration with a transparent background, levelled so both wheels sit on the ground. | Traced from measurements of the owner's left-side photograph (February 2012 state: gold ignition cover, black rims, gold hubs, as the owner chose). Logos are simplified shapes pending the owner's decision. | **Rejected by the owner** (12 Nov 2026): hand-drawn vector art is not good enough. Kept for the record. |

## How artwork is produced

The artwork route was settled with the owner on 12 November 2026:

1. The owner supplied their own 3D models of the bike and the rider (two separate models, so the rider can get on and off). Their shapes are the owner's and are not to be altered. The one agreed exception is the rider's hands, moved inward onto the grips at the owner's request.
2. Colours and graphics were applied to those models in Blender to match the reference photographs: the pink, sky blue, white, black and orange scheme, the number 28 and the owner's team logo. The owner approved the result as the default models.
3. Both models are rigged and animated in Blender. Sprite frames with transparent backgrounds will be rendered from those animations once the owner approves them.

The models, the Blender files, the team logo and the reference photographs stay on the owner's computer and are not in this repository. Only rendered sprite frames will be added here, after approval.

| Stage | Status |
|---|---|
| Painted default models | **Approved by the owner** (12 Nov 2026) |
| Animations (first pass of all thirteen in DESIGN.md 3.6) | Awaiting the owner's review in Blender |
| Rendered sprite frames | Not started |

## The in-app stand-in

The grey card the application currently shows is drawn in XAML (`OverlayWindow.xaml`). It is a labelled stand-in, not artwork, and has no asset file.
