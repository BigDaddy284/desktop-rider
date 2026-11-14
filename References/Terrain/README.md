# Jumps and terrain: what real tracks look like

Research for building the trick props (ramps, landings, whoops) so they look like a real motocross track rather than smooth, generic mounds. It is written for whoever builds the props in Blender and places them in Unity.

Each fact is marked:

- **[S]** comes from a source listed at the bottom.
- **[K]** is general motocross knowledge that I could not find a source for tonight. Check these against the owner's photographs before relying on them.

The owner's photographs (see `References/Tricks/RIDER_STYLE.md`) show a young rider on a KTM 50 SX on real tracks. Where they show terrain, they are the best reference of all. Ruts and berms, corner exits with roost and climbing a face are all in that set.

## 1. Scale: our world is a 50 cc track

The real bike is a KTM 50 SX. KTM lists a seat height of 653 mm, 233 mm ground clearance and 165 mm / 190 mm of fork / shock travel [S: KTM]. Our model's axles are 1.09 m apart. Version 1's trick world uses a gravity of 2.6 m/s² and a riding speed of 3.0 m/s, so the timing reads well at desktop size (`Tools/Blender/trick_core.py`).

The rider's own jumps are small: the wheels are about a wheel's height off the ground, the bike flies level and straight, and the rider stands on the pegs (RIDER_STYLE.md). Every size below is therefore scaled to a 50 cc track, not a professional one.

With gravity 2.6 and speed 3.0, a take-off at angle *a* gives (flat ground to flat ground):

| Take-off angle | Distance | Height above the lip |
|---|---|---|
| 25° | 2.65 m | 0.31 m |
| 30° | 3.00 m | 0.43 m |
| 38° (version 1) | 3.36 m | 0.66 m |

**30° gives about a wheel's height of air, exactly the rider's style, and it is also the face angle a supercross practice-track guide recommends** [S: Mototerra, citing Medsker 1999]. Version 1's 38° throws the bike higher than the real rider ever goes. Use 30° for plain jumps and keep steeper angles for the freestyle steel ramp only.

## 2. Take-off faces

Real faces are not circular arcs, and they are not wedges.

- **The curve tightens as it rises** (a clothoid or "easement" curve), so the bike is loaded gradually. It is not one constant radius and not a sharp corner [S: Mototerra]. Our current `build_jump` uses one circular arc, which looks machine-made.
- **The last part is straight**, so every rider leaves at the same angle. Then comes a short flat or rounded **lip** [S: Mototerra; K for the straight run].
- **Faces are about 30°** on supercross-style practice jumps and beginner jumps [S: Mototerra, citing Medsker and McCormack].
- **Jumps are wider at the landing than at the take-off.** One draft guideline asks for the landing to be at least 1 m wider [S: Mototerra, citing MNZ].
- **Dirt has sloping sides.** A dirt jump is a pile of compacted soil, so its sides slope down at roughly the angle loose dirt settles at, about 35 to 40°. They are never vertical walls [K]. Our props are currently straight-sided extrusions, and this is the biggest reason they look like blocks.
- **The face wears.** Rear wheels dig one or two lines up the face. Kickers can form there too: a lump on the face that throws the rear of the bike up [S: Risk Racing slang guide; K for where the lines form]. One shallow groove up the middle of the face, with a slightly raised edge, makes it look ridden.

## 3. Landings

- **Landing faces are flatter than take-off faces**: 20°, 15° or 10° for longer jumps [S: Mototerra, citing Medsker].
- **The top edge (the "knuckle") is rounded.** It matches the take-off angle at the knuckle and then steepens progressively down the slope [S: Mototerra].
- **There is a run-out** of flat ground after the landing [S: Mototerra].
- **Doubles**: the second mound sits about 400 mm lower than the first on a full-size track [S: Mototerra, citing MNZ]. Scaled to our world, that is about 0.15 m lower.
- **Tabletops** have a flat top between take-off and landing. One guideline gives 3 to 20 m on a full-size track [S: Mototerra, citing MNZ]. In our world, make it short, about 1.0 to 1.5 m, so that a 3 m jump clears it.
- **Peaked tops are not allowed** on built jumps [S: Mototerra, citing MNZ]. Every top has a rounded or flat crown.
- Landings take a beating: a darker, smoother, more compacted strip where the wheels touch, and loose dirt kicked down the slope [K].

## 4. Whoops and rollers

- **Supercross whoops are about 0.9 m (3 ft) tall**, evenly spaced, and only take a couple of seconds to cross [S: WSX].
- **Riders take them two ways**: skimming over the tops in a high gear, wheels barely dipping, or jumping them when they are breaking down [S: WSX]. Our Whoops animation skims, which is right.
- **Rollers** (rolling waves) are bigger and rounder: 8 to 10 m from crest to crest, no more than 80 cm high [S: Mototerra, citing FIM].
- **Whoops wear into a shape.** The rear tyre digs the tops out into a cup ("cupped whoops"), and ruts run along the main lines [S: Risk Racing slang guide]. Each whoop also gets a slightly steeper back side than front side, because riders drive into the back [K]. Make them **not quite identical**: tiny differences in height and spacing (2 to 5%) instead of a perfect cosine wave.
- Scaled to our world: whoops about 0.25 to 0.30 m tall, crests 1.2 to 1.4 m apart. That is what `build_whoops` already uses (0.30 m, 1.3 m), so the sizes are fine and only the shape needs work.

## 5. The ground between jumps

These details are small, but they are what make a patch of dirt read as a track.

- **Ruts in turns**: grooves worn into the dirt, ideally five or six lines per turn [S: Racer X]. Each rut is about a tyre wide, with a raised lip of pushed-up dirt on each side [K].
- **Berms**: dirt built up on the outside of a turn into a banked wall, which lets riders turn faster [S: Risk Racing track guide]. The owner's photographs show the rider in ruts and berms with the bike laid over 30 to 45° (RIDER_STYLE.md).
- **Braking bumps**: a washboard of ripples that builds up where riders brake into corners, and grows as the day goes on [S: Risk Racing slang guide]. **Acceleration bumps** form the same way on corner exits [S: Risk Racing slang guide]. Put a short patch of these before a landing's run-out ends and where the rider stops.
- **Prepared dirt is loamy and holds water**; heavily watered tracks go dark, and organic material (sawdust, wood chips) is mixed in on some tracks [S: Racer X]. Show this as darker patches on the jump faces and in the ruts, and lighter, dusty, dry dirt on the flat tops [K].
- **Roost**: the spray of dirt thrown back by the rear wheel. The owner's start video shows a spray of dirt and a puff of blue two-stroke smoke off the line (RIDER_STYLE.md). At take-off and at every hard drive out of a landing, a short spray of small dark clods and a light dust puff should come off the rear wheel [K].

## 6. Freestyle (FMX) set-up

- **Take-off ramps are usually metal**, because that keeps the lip the same every time. **Landings are normally dirt.** At events without much space they are built on trucks or trailers [S: Wikipedia].
- Big Air jumps on full-size bikes are usually more than 75 ft (23 m) [S: Wikipedia]. Our miniature ramp is a toy version of this. It should look like a real FMX ramp, but its size follows our world.
- **What a real steel ramp looks like** [K, check against photos]:
  - A curved deck made of steel or aluminium slats or a grip plate. It is dark and worn shiny where the tyres run.
  - A lattice frame underneath.
  - A dirt "kicker" pile pushed up against the bottom edge, so the bike rolls on from the dirt.
  - The deck is narrow, little more than a bike's width, and it is far steeper than a dirt jump.
- The landing is a long, tall dirt mound with a flat-topped deck and a long face of about 20° or less [S: Mototerra for the landing angle; K for the deck].
- **Do not copy any real ramp maker's logos or branding** onto the ramp (CLAUDE.md §7). Plain metal and dirt only.

## 7. What to change in our props

Ranked by how much each change helps the look.

1. **Sloped sides on every dirt prop** (about 38°) instead of vertical walls. The top surface is a little narrower than the base.
2. **Take-off: an easement curve, then a straight face at 30°, then a rounded lip.** Steel ramp: a steeper face (keep 38 to 45°), a narrow deck, a dirt kicker at its foot.
3. **Landing: a rounded knuckle, a face about 10° flatter than the arrival angle that steepens slightly, then a smooth run-out into flat ground.** Doubles: the second mound 0.15 m lower than the take-off.
4. **Surface wear**:
   - A tyre groove up the take-off face and down the landing, with a slightly raised edge.
   - Cupped whoop tops.
   - A short patch of braking bumps where the rider stops.
5. **Irregular dirt**: a tiny, smooth wobble in height along the length (a few millimetres) and slightly different whoops. Use fixed seeds, so the props stay the same every time they are built.
6. **Colour by wear**:
   - Darker, damp dirt in grooves and on faces.
   - Lighter, dry dirt on flat tops and edges.
   - Scattered small clods (simple low-poly lumps) at the bottom of faces.
7. **Effects in Unity, not in the mesh**: a roost of clods and a dust puff from the rear wheel at take-off and on the drive out of a landing.

`Tools/Blender/terrain_shapes.py` turns points 1 to 6 into tested shape functions that the prop builder can use. `Logic/Tricks` places the props and moves the bike in Unity.

## Sources

- [Motocross Jump Design: How to Build Jumps Safely, Mototerra](https://www.mototerra.mx/motocross-jump-design). Face and landing angles (citing Medsker 1999 and McCormack), the clothoid lip, knuckle and run-out, MNZ tabletop, double and width rules, FIM roller sizes.
- [Tech Tuesday: Mastering the Whoops, World Supercross Championship](https://worldsupercrosschampionship.com/tech-tuesday-mastering-the-whoops/). Whoop height and how riders take them.
- [Motocross Slang Words, Risk Racing](https://riskracing.com/blogs/news/motocross-slang-words-what-do-those-terms-mean). Braking and acceleration bumps, kickers, whoops, cupped whoops.
- [Motocross Track Guide, Risk Racing](https://riskracing.com/blogs/news/motocross-track-guide-terms-layouts-and-obstacles). Tabletops, doubles, rollers, berms, ruts.
- [The Breakdown: Stuck in a Rut, Racer X](https://racerxonline.com/2013/07/10/breakdown-stuck-in-a-rut). Ruts, number of lines per turn, watered and wood-chip dirt.
- [Freestyle motocross, Wikipedia](https://en.wikipedia.org/wiki/Freestyle_motocross). Metal take-off ramps, dirt landings, Big Air distances.
- [KTM 50 SX technical specifications, KTM](https://www.ktm.com/en-gb/models/motocross/2-stroke/2026-ktm-50-sx/technical-specifications.html). Seat height, ground clearance, suspension travel.
