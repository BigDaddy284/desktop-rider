# Sprite specification

How the rider's animations are stored and played. Following this lets a new animation, or a whole new character, be added without changing the application's code.

## Where the sprites come from

The owner's painted 3D models of the bike and rider are animated in Blender and rendered side-on with a transparent background (see ASSET_INDEX.md). The Blender files stay on the owner's computer. Only the rendered sheets are in `Assets/Sprites/Rider/`.

## Files

```
Assets/Sprites/Rider/
  sprites.json        the list of animations
  Right/<Name>.png    one sheet per animation, bike pointing right
  Left/<Name>.png     the same animation, bike pointing left
```

The left-facing sheets are rendered separately, not flipped copies, because the two sides of the bike are different colours and the number must read the right way round.

## Frames

| Property | Value |
|---|---|
| Frame size | 467 × 389 pixels, the same for every frame of every animation |
| Scale | 240 pixels per metre of the real bike |
| Ground line | 382 pixels from the top of the frame; the tyres stand on it |
| Frame rate | 12 frames per second |
| Background | Fully transparent |
| Sheet layout | Frames run left to right, then top to bottom, 8 to a row |
| Format | PNG with transparency, 256-colour palette |

Because every frame is the same size and the ground line never moves, frames line up exactly when the animation changes.

On screen, a frame is drawn at three quarters of its pixel size when the character size setting is 1.0.

## The list of animations (`sprites.json`)

Each entry has a `name`, whether it is a `loop`, its `frameCount`, how many `columns` its sheet has, and its `right` and `left` sheet files. A looping animation may also name an `enter` and an `exit` animation.

| Animation | Type | Frames | Notes |
|---|---|---|---|
| Idle | Loop | 24 | The resting animation |
| IdleVariation | One-shot | 49 | Looks around and shifts weight |
| LookLeft, LookRight | One-shot | 17 each | |
| Revving | Loop | 12 | |
| Thinking | One-shot | 37 | Hand to chin |
| Talking | Loop | 12 | |
| Wheelie | One-shot | 31 | |
| Celebration | One-shot | 25 | |
| Sleeping | Loop | 48 | Enters through SleepStart, leaves through SleepEnd |
| SleepStart, SleepEnd | One-shot | 13 each | Only played around Sleeping |
| RidingLoop | Loop | 12 | Enters through RideOff, leaves through RideOn |
| RideOff, RideOn | One-shot | 16 each | Played on the spot; the application moves the window |

## Playback rules

- Every one-shot starts and ends in the resting pose, so one can follow another without a jump.
- A loop that needs a different pose (sleeping, riding) is always entered and left through its `enter` and `exit` animations.
- A one-shot that is playing finishes before the next animation starts.
- A request with a lower priority than the one-shot that is playing is ignored.
- After a one-shot, the rider goes back to the loop it was in, or to Idle if it had to leave that loop.
- Animation speed follows the Animation speed setting (0.5 to 2.0).
- Only the sheet on screen and the Idle sheet are kept in memory.

## Adding an animation

1. Render it to two sheets that follow the frame rules above.
2. Add an entry to `sprites.json`.

It then appears in the rider's right-click menu and can be requested by name. No code changes are needed.
