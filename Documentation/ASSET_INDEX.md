# Asset index

Every piece of artwork in the repository, and whether the owner has approved it.

**No artwork has been approved yet.** Nothing listed here may be used as final character artwork or animated until it is.

| File | What it is | Source | Status |
|---|---|---|---|
| `Assets/Concepts/bike-left-v1.svg` | First concept of the bike, left side, standing still. Hand-drawn vector illustration, 1000 × 700 units, transparent background. | Drawn from the owner's left-side bike photograph. Shows the number 28. Sponsor and brand logos are simplified to plain shapes pending the owner's decision. | **Rejected by the owner** (12 Nov 2026): did not look like a motocross bike. Kept for the record. |
| `Assets/Concepts/bike-left-v2.svg` | Second concept of the bike, left side. Vector illustration with a transparent background, levelled so both wheels sit on the ground. | Traced from measurements of the owner's left-side photograph (February 2012 state: gold ignition cover, black rims, gold hubs, as the owner chose). Logos are simplified shapes pending the owner's decision. | **Rejected by the owner** (12 Nov 2026): hand-drawn vector art is not good enough. Kept for the record. |

## How artwork is produced

The owner has asked for the artwork to be produced with an image generator, using the reference photographs. No image-generation tool is available in the development environment, so this is an open dependency: the artwork task stays incomplete until a generator route is agreed with the owner. Hand-drawn vector concepts were tried twice and rejected.

## The in-app stand-in

The grey card the application currently shows is drawn in XAML (`OverlayWindow.xaml`). It is a labelled stand-in, not artwork, and has no asset file.
