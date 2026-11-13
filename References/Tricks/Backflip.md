# Backflip

**Kind:** Freestyle trick

## What it is

A full backward rotation of bike and rider together. First landed on a full-size bike in 2002. The rotation is started on the ramp, not in the air.

## The real move: key poses in order

| # | When | Rider | Bike |
|---|---|---|---|
| 1 | Approach | Standing, centred, steady speed. Second or third gear on a full-size bike. | Level. |
| 2 | Up the ramp | Halfway up, the throttle goes wide open. Near the top the rider throws their weight back with arms locked straight, legs slightly bent, hips as far back as possible, head thrown back. | Rear suspension squats hard. The bike is close to vertical as the front wheel leaves the lip and is already rotating. |
| 3 | First quarter turn | Arms straight, pulling the bars back, throttle held on, looking back over the top for the ground. | Rotating fast. This rate was set on the ramp. |
| 4 | Upside down (about 40% to 50%) | Brings the body back toward the middle of the bike, knees gripping. Tucking in speeds the turn up, leaning forward slows it. | Inverted at the top of the arc. |
| 5 | Three-quarter turn | Sees the landing, opens the body to slow the turn. | Nose coming round to point down the landing. |
| 6 | Landing | Standing, absorbing with legs. | Both wheels close together or rear slightly first, on the down slope. |

Once in the air the rider can only fine-tune: a rev speeds the rotation slightly, leaning forward slows it. Mistakes at take-off cannot be fixed. One source describes a ramp with an 8 m radius, another a 35 ft gap from ramp to landing at about 30 mph.

## Version 1, for comparison

**Timing and bike motion**

- **Stage:** steel freestyle ramp, 38° at the lip, lip 0.5 m high, landing on a dirt mound 0.8 m high with a 31° down slope. Bike speed 3 m/s. Gravity is scaled to 2.6 m/s² so a small ramp gives real-looking hang time.
- **Timeline (seconds):** pulls away 0 to 0.6, leaves the lip at 1.46, in the air for 1.79, lands at 3.25, brakes from 4.04, stopped at 5.08. That is 122 keyed frames at 24 a second, 62 sprite frames at 12 a second.
- **Height:** the centre of bike and rider peaks about 2.4 m above the ground.
- **Bike in the air:** 38° nose up at the lip, held for the first 0.27 s, then eases over to 27° nose down just before landing. No roll, no yaw, bars straight. Rear wheel spins 15% faster than on the ground, front wheel slows to half. Suspension hangs out in the air (30 mm rear, 45 mm fork), compresses on the ramp face and on landing, with one small overshoot after the hit.
- **Rider on the ground:** seated to pull away, up into the attack position by 0.7 s, legs soak the ramp, a small pop at the lip, legs soak the landing, sits back to rest while braking.
- **Rotation in the code:** the bike turns 295° backwards between the lip and touchdown (from 38° nose up, over the top, to 27° nose down). The rate starts slow, is fastest in the middle and slows again before landing. The average is about 165° a second.
- **Pose in the code:** tucked in close to the bike with the head tipped back 34° to look for the landing, blended in from just after the lip and held until 70% of the flight.

**Pose in the version 1 sprite sheet**

Bike and rider turn over as one, rider tucked and upside down at the top of the arc, landing on the down slope.

## What to change for the 3D version

The biggest gap is the take-off. v1 leaves the lip at the same 38° as every other trick and then begins to turn. A real flip leaves the lip already near vertical and already at full rotation speed, with the rider's arms locked and body thrown back. For 3D the rotation rate should be nearly constant through the air, with only a small slow-down at the end.

## Best views

Side-on is the classic view and shows the rotation and the arc. Front three-quarter from the landing shows the head looking back.

## Reference links

| Type | Link |
|---|---|
| Read | [Motorcycle News, "How do you do a backflip?" with Jamie Squibb](https://motorcyclenews.com/news/2016/october/how-the-hell-do-you-do-a-backflip-nitro-circus-jamie-squibb) |
| Read | [Visordown, "How to do a back-flip"](https://www.visordown.com/features/guides/how-do-back-flip) |
| Read | [Wikipedia, "Freestyle motocross" (history, ramps, gap distances)](https://en.wikipedia.org/wiki/Freestyle_motocross) |
| Read | [Red Bull, "FMX Tricktionary" (written trick list with photos)](https://redbull.com/gb-en/fmx-trick-list-dictionary) |
| Video | ["FMX Whip and Backflip in Realtime and Slow Motion at the Extreme Stunt Show"](https://www.youtube.com/watch?v=dnNp84JbAnA) |
| Video | ["Backflip Motocross Freestyle Slow Motion por Jeff e VAZ"](https://www.youtube.com/watch?v=iV9UjI8CfZ0) |
| Video | ["GoPro HERO 3 Black 120fps FMX Freestyle Motocross 70ft Ramp"](https://www.youtube.com/watch?v=pn4rveEUzE8) |

Video, not yet watched: add timestamps and the camera angle when you open it.
