# RideOff

**Kind:** Riding

## What it is

Pulling away from a standstill. In version 2 these three animations become free riding. The poses below are the building blocks: they should blend with speed, throttle, brake and steering instead of playing as fixed clips.

## The real move: key poses in order

| # | When | Rider | Bike |
|---|---|---|---|
| 1 | Waiting | Seated in the middle of the seat, one foot or both on the ground, head up. On a start line: both feet down just in front of the pegs, chest forward over the bars, elbows up. | Upright or leaning slightly onto the foot that is down. |
| 2 | Throttle on | Chest goes forward and down to keep the front wheel on the ground. Squeezes the bike with the knees. | Rear suspension squats, fork extends, rear tyre digs in and may spin a little, throwing dirt. |
| 3 | First metres | Feet come up onto the pegs once the bike is balanced, usually within the first couple of bike lengths. | Front wheel light, just skimming. |
| 4 | Up to speed | Slides back to the middle of the seat, then stands up into the attack position. | Settles level. |

Under acceleration the weight moves to the rear wheel, so the rider moves forward to hold the front down. On this bike there is no gear change and no clutch lever to work.

## Braking (the reverse of pulling away)

| # | When | Rider | Bike |
|---|---|---|---|
| 1 | Brakes on | Standing, hips move back over the rear of the seat, arms brace, knees grip, head stays up. | Fork dives, rear goes light and may hop over bumps. |
| 2 | Slowing | Stays back and low. | Rear wheel may lock and slide briefly on loose ground. |
| 3 | Into the corner or stop | Comes forward and sits down at the front of the seat. For a stop, one foot goes down as the bike halts. | Fork comes back up as the brake is released. |

## Version 1, for comparison

**Timing and bike motion**

Not in the trick scripts. From the original base animation set.
- **Length:** 16 sprite frames, 1.3 s, played on the spot while the application slides the window. It leads into RidingLoop.
- The jump and trick animations contain their own pull-away: seated and leaning forward for the first 0.4 s with a small 4° nose lift and 20 mm of rear squat, then up to the attack position by 0.7 s, reaching 3 m/s at 0.6 s.

**Pose in the version 1 sprite sheet**

Seated rider leans forward slightly as the wheels start to turn. Both feet are on the pegs throughout.

## What to change for the 3D version

v1 never puts a foot down, so there is no foot-up moment. For 3D decide whether the rider rests with a foot on the ground (see the Idle sheet), then the pull-away should include that foot coming up.

## Best views

Side-on shows the squat and the lean forward. Front three-quarter shows the elbows and the foot coming up.

## This rider's own style

From the owner's start-gate video (see [RIDER_STYLE.md](RIDER_STYLE.md)). These take priority over the general technique above.

- Waiting: seated forward, both feet on the ground, elbows very high and wide.
- Launch: blue two-stroke smoke and dirt from the rear wheel. Feet stay down, skimming the ground, for about 0.6 s, and are on the pegs by about 1 s.

## Reference links

| Type | Link |
|---|---|
| Read | [Motocross Action, "Ten things about getting the perfect start"](https://motocrossactionmag.com/ten-things-about-getting-the-perfect-start) |
| Read | [Motocross Action, "Speed secrets: body position and the art of bike handling"](https://motocrossactionmag.com/speed-secrets-body-position-the-art-of-bike-handling) |
| Video | ["Motocross basics: Body position for accelerating and braking in motocross"](https://www.youtube.com/watch?v=xzuluxyqMX8) |
| Video | ["6 yr old first year racing a KTM SX 50 mini"](https://www.youtube.com/watch?v=qSRg8xtUV4M) |

Video, not yet watched: add timestamps and the camera angle when you open it.
