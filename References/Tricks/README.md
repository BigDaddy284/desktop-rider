# Trick and animation reference sheets

One sheet for each of the 33 animations in Desktop Rider version 1, written for the version 2 rebuild in 3D, where the rider is seen from every side.

Each sheet has the same parts:

1. **What it is.**
2. **The real move**: key poses in order, with rough timing given as a share of the time in the air, and what the bike does.
3. **Version 1, for comparison**: the timing and bike motion taken from `Tools/Blender/trick_animations.py` and `trick_core.py` (on `main`, and in the project files under `blender/`), and the pose in the version 1 sprite sheet.
4. **What to change for the 3D version.**
5. **Best views**: which camera angles show the move.
6. **Reference links.**

There is also [PHOTO_AUDIT.md](PHOTO_AUDIT.md), which lists the views of the rider, gear and bike that the owner's photographs do not cover.

## The sheets

| Animation | Kind |
|---|---|
| [Idle](Idle.md) | Character |
| [IdleVariation](IdleVariation.md) | Character |
| [LookLeft](LookLeft.md) | Character |
| [LookRight](LookRight.md) | Character |
| [Revving](Revving.md) | Character |
| [Celebration](Celebration.md) | Character |
| [Thinking](Thinking.md) | Character |
| [Talking](Talking.md) | Character |
| [SleepStart](SleepStart.md) | Character |
| [Sleeping](Sleeping.md) | Character |
| [SleepEnd](SleepEnd.md) | Character |
| [RideOff](RideOff.md) | Riding |
| [RidingLoop](RidingLoop.md) | Riding |
| [RideOn](RideOn.md) | Riding |
| [Wheelie](Wheelie.md) | Ground trick |
| [Stoppie](Stoppie.md) | Ground trick |
| [Whoops](Whoops.md) | Supercross move |
| [Jump](Jump.md) | Motocross move |
| [Scrub](Scrub.md) | Motocross move |
| [Whip](Whip.md) | Motocross move |
| [Backflip](Backflip.md) | Freestyle trick |
| [CanCan](CanCan.md) | Freestyle trick |
| [HeelClicker](HeelClicker.md) | Freestyle trick |
| [IndianAir](IndianAir.md) | Freestyle trick |
| [LazyBoy](LazyBoy.md) | Freestyle trick |
| [NacNac](NacNac.md) | Freestyle trick |
| [NoFooter](NoFooter.md) | Freestyle trick |
| [NoHander](NoHander.md) | Freestyle trick |
| [Nothing](Nothing.md) | Freestyle trick |
| [OneHander](OneHander.md) | Freestyle trick |
| [RockSolid](RockSolid.md) | Freestyle trick |
| [Superman](Superman.md) | Freestyle trick |
| [SupermanSeatGrab](SupermanSeatGrab.md) | Freestyle trick |

## How far to trust this

- **Written sources were read.** Links marked "Read" were opened and the technique notes come from them.
- **Videos were not watched.** They were found by search on 13 November 2026 and are listed by the title the search gave. There are no timestamps and the camera angle is not confirmed. Whoever opens one first should add both to the sheet. No video has been downloaded or stored.
- **Poses with no written source** come from general knowledge of the sport and should be checked against the videos before being treated as final. This applies most to OneHander, NoFooter, Nothing, CanCan and the character animations.
- **Version 1 numbers are exact.** They were calculated by running the trick scripts' own maths, not estimated by eye. The base animations (Idle, Revving, Wheelie and so on) are not in those scripts, so only their length and appearance are recorded.
- **The version 1 sprite sheets are on `main`**, not on `v2.0`. They were viewed there.

## Things that apply to every sheet

- **Timing scale.** Version 1 uses a miniature world: 3 m/s riding speed and gravity scaled down to 2.6 m/s², so a 0.5 m ramp gives about 1.8 s in the air. A real freestyle jump of around 75 ft gives roughly 2 to 3 s (an estimate, not from a source). Trick timing is therefore given as a share of the flight, which carries over to any ramp size.
- **The bike is a KTM 50 SX.** It has an automatic clutch and a single gear. No clutch lever is worked and no gears are changed in any animation.
- **Which side.** Version 1 keyed one-sided tricks (CanCan, NacNac, OneHander, seat grabs, whips) toward the camera. With a free camera each needs a fixed side. Riders normally keep the right hand on the throttle.
- **The bike reacts to the rider.** When the rider throws their weight back, the front of the bike rises, and the reverse. Version 1 mostly leaves this out. It is the cheapest way to make the 3D versions look real.
- **Throttle and brake in the air.** Speeding up the rear wheel lifts the nose. Braking it drops the nose. The wheels should visibly change speed when the bike's attitude is being corrected.
- **A resting stance is undecided.** A real bike cannot stand up by itself. See the Idle sheet.
