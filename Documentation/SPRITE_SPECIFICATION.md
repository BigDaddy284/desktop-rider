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

Because every standard frame is the same size and the ground line never moves, frames line up exactly when the animation changes.

### Larger frames for jumps, tricks and whoops

An animation that needs more room has its own, larger frame. The standard frame sits inside it at a stated place, so the rider is drawn in exactly the same spot as in every other animation.

| Kind | Frame size | Room added around the standard frame | Used by |
|---|---|---|---|
| Air | 907 × 849 pixels | 180 behind the bike, 260 in front, 460 above | Jumps and freestyle tricks |
| Ground | 907 × 489 pixels | 180 behind the bike, 260 in front, 100 above | Whoops, stoppie |

In `sprites.json` such an entry also has `frameWidth`, `frameHeight`, `originX` and `originY`. The origin is where the standard frame's top-left corner sits inside the larger frame with the bike facing right. The left-facing sheet is a mirror image, so there the room in front is on the left; the application works that out. The added room is always a multiple of 20 pixels, so that at the normal on-screen size (45%) the larger frame lines up with the standard one on a whole pixel.

The application draws these animations in a second see-through window laid over the rider (see ARCHITECTURE.md).

On screen, a frame is drawn at 45% of its pixel size when the character size setting is 1.0 (about 210 × 175). The owner chose this size on 12 November 2026.

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

### Jumps, tricks and whoops (added 13 November 2026)

All are one-shots that start and end in the resting pose. The bike stays where it is and the ground moves past: the rider pulls away, the ramp or whoops roll in from the front, and after the landing the rider brakes to a stop as the last of it rolls out behind.

| Animation | Frames | What happens |
|---|---|---|
| Jump | 59 | A motocross jump off a dirt take-off: attack position, bike floats level, lands on the down slope. |
| Whip | 61 | Off a dirt take-off the bike is laid flat and swung sideways, then brought back straight for the landing. |
| Scrub | 56 | Staying low off a dirt take-off: the bike is thrown flat the moment it leaves the lip and picked up before the landing. |
| Backflip | 62 | A full backward rotation off the freestyle ramp. |
| Superman | 62 | Feet off the pegs, body stretched out flat behind the bars. |
| SupermanSeatGrab | 62 | As Superman, but one hand lets go of the bar and holds the seat, so the body trails further back. |
| NoHander | 62 | Both hands off the bars and thrown up. |
| NoFooter | 62 | Both feet off the pegs and kicked back. |
| Nothing | 62 | No hands and no feet at once. |
| HeelClicker | 62 | Legs swung up over the bars and the boots clicked together in front. |
| CanCan | 62 | The far leg lifted over the seat and kicked out on the near side. |
| NacNac | 62 | The far leg swung back over the rear mudguard while the rider looks back. |
| LazyBoy | 62 | Lying back flat along the seat, legs out in front, arms stretched behind. |
| OneHander | 62 | One hand off the bar and held high. |
| IndianAir | 62 | A superman with the legs scissoring past each other. |
| RockSolid | 62 | From a seat grab, both hands let go for a moment and the rider floats free above the bike. |
| Whoops | 61 | A supercross whoop section taken at speed: wheels skimming the tops, suspension working, rider standing with weight back. |
| Stoppie | 37 | Hard front brake: the forks dive, the rear wheel lifts, is balanced and set back down. |

How the movement is made (`Tools/Blender/trick_core.py` and `trick_animations.py`):

- **Speed.** The bike rides at 3 metres a second, the same as RidingLoop, so the wheels turn at the right rate for the ground passing under them.
- **Wheels on the ground.** Each wheel is treated as a circle resting on the ground profile, so on a ramp face, a landing slope or a whoop the bike sits and tilts as a real one would.
- **Take-off.** The freestyle ramp is a curved steel kicker ending at 38 degrees; the dirt take-offs end at 24 to 32 degrees. The suspension is pushed down through the curve of the ramp and springs back at the lip, and the wheels hang down at full stretch once the bike is in the air.
- **Flight.** The bike follows a true thrown path (a parabola). Its nose stays high through the first part of the flight and comes down to match the landing slope, with the rear wheel touching a moment before the front.
- **Landing.** The landing mound is placed where the flight path comes down, so the wheels meet its down slope. The suspension goes deep on the hit, comes back with one small overshoot and settles, and the rider's legs soak it up.
- **Timing.** Flight time for the freestyle tricks is about 1.8 seconds, with the trick held through the middle of the flight, which is how long these tricks take off a real freestyle ramp. To fit that on a desktop the ramp is a miniature (the lip is half a metre high), so gravity in the animation is gentler than real gravity (2.6 instead of 9.8 metres per second squared). The effect is of a full-size jump seen in a small space.
- **Whoops.** The wheels skim from top to top instead of dropping into every dip. The chassis follows a smoothed path while each wheel follows the ground, and the difference between the two is what the suspension is seen doing.

These were built from descriptions of the real techniques, not traced from film frame by frame, and have not yet been seen by the owner. See ASSET_INDEX.md for their approval status.

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

For a jump or trick, `Tools/Blender/trick_animations.py` renders the frames from the owner's Blender file and `Tools/Blender/pack_sprites.py` packs them into sheets and writes the entry. A new freestyle trick is one new rider pose in `trick_animations.py`.

It then appears in the rider's right-click menu and can be requested by name. No code changes are needed.
