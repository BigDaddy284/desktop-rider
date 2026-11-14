"""Shapes for the trick props that look like a real, ridden motocross track, not smooth generic mounds.

Pure maths, no Blender: every function here can be tested with plain Python (see tests/test_terrain_shapes.py)
and used by trick_animations.py / build_unity_props.py inside Blender. The reasons behind each shape, with
sources, are in References/Terrain/README.md. Sizes are in model metres (the 50 cc world: gravity 2.6, speed 3.0).

A Profile is a height outline h(x) for x from 0 to its length, which is what trick_core.Terrain.add() takes, so a
Profile can replace the lambdas used there. A Section is the shape across the track at one point along it.
Everything that looks random is driven by a fixed seed, so a prop is the same every time it is built.
"""
import bisect
import math
import random

# Loose, compacted dirt settles at roughly this angle; the sides of a dirt jump slope at it, never straight down.
SIDE_ANGLE = 38.0
# A plain jump face, as on supercross practice tracks and beginner jumps (References/Terrain §2).
FACE_ANGLE = 30.0


class Profile:
    """A height outline sampled along x. Calling it gives the height at x (0 outside the outline)."""

    def __init__(self, xs, zs, marks=None):
        if len(xs) != len(zs) or len(xs) < 2:
            raise ValueError('a profile needs at least two matching points')
        for a, b in zip(xs, xs[1:]):
            if b <= a:
                raise ValueError('profile points must go forward along x')
        self.xs = [x - xs[0] for x in xs]
        self.zs = [max(0.0, z) for z in zs]
        self.marks = dict(marks or {})   # named places along the outline, e.g. 'lip', 'knuckle'

    @property
    def length(self):
        return self.xs[-1]

    @property
    def height(self):
        return max(self.zs)

    def __call__(self, x):
        if x <= 0.0:
            return self.zs[0] if x == 0.0 else 0.0
        if x >= self.length:
            return self.zs[-1] if x == self.length else 0.0
        i = bisect.bisect_right(self.xs, x)
        x0, x1 = self.xs[i - 1], self.xs[i]
        w = (x - x0) / (x1 - x0)
        return self.zs[i - 1] + (self.zs[i] - self.zs[i - 1]) * w

    def slope(self, x, dx=0.01):
        """Steepness in degrees at x; positive going up."""
        a = max(0.0, x - dx); b = min(self.length, x + dx)
        return math.degrees(math.atan2(self(b) - self(a), b - a))

    def then(self, other, gap=0.0):
        """This outline followed by another (and an optional flat gap at ground level between them)."""
        xs = list(self.xs); zs = list(self.zs)
        marks = dict(self.marks)
        start = self.length + gap
        if gap > 0:
            xs.append(self.length + gap * 0.5); zs.append(0.0)
        for x, z in zip(other.xs, other.zs):
            nx = start + x
            if nx <= xs[-1] + 1e-9:
                zs[-1] = max(zs[-1], z)
                continue
            xs.append(nx); zs.append(z)
        for k, v in other.marks.items():
            marks[k] = start + v
        return Profile(xs, zs, marks)

    def plus(self, extra):
        """The same outline with extra(x) added to every height (bumps, wear, wobble). Ground stays at 0."""
        return Profile(self.xs, [max(0.0, z + extra(x)) if z > 0 else z for x, z in zip(self.xs, self.zs)], self.marks)


def _trace(steps):
    """Walks along a path given as (length, start angle, end angle, how the angle changes) pieces. Returns xs, zs."""
    xs, zs = [0.0], [0.0]
    for length, a0, a1, ease in steps:
        n = max(2, int(math.ceil(length / 0.01)))
        for i in range(1, n + 1):
            u0 = (i - 1) / n; u1 = i / n; um = 0.5 * (u0 + u1)
            ang = math.radians(a0 + (a1 - a0) * ease(um))
            ds = length / n
            xs.append(xs[-1] + ds * math.cos(ang)); zs.append(zs[-1] + ds * math.sin(ang))
    return xs, zs


def _linear(u):
    return u


def _clothoid(u):
    # Curvature grows steadily along the curve, so the angle grows with the square of the distance.
    return u * u


def _smooth(u):
    return u * u * (3 - 2 * u)


def _clean(xs, zs):
    out_x, out_z = [xs[0]], [zs[0]]
    for x, z in zip(xs[1:], zs[1:]):
        if x > out_x[-1] + 1e-6:
            out_x.append(x); out_z.append(z)
    return out_x, out_z


def takeoff(height, angle=FACE_ANGLE, straight=0.25, lip=0.06, back_angle=SIDE_ANGLE):
    """A dirt take-off: a curve that tightens as it rises (not one circle), a straight face so the bike always leaves
    at the same angle, a slightly rounded lip, then the back of the mound falling away at the angle loose dirt sits at.
    Marks: 'face' (start of the straight), 'lip' (where the wheels leave)."""
    sa = math.sin(math.radians(angle))
    straight = min(straight, 0.8 * height / sa)
    # Height gained along a clothoid of length L that turns to `angle`: L * mean of sin(angle * u^2).
    mean = sum(math.sin(math.radians(angle) * ((i + 0.5) / 200) ** 2) for i in range(200)) / 200
    curve = (height - straight * sa) / mean
    xs, zs = _trace([(curve, 0.0, angle, _clothoid), (straight, angle, angle, _linear)])
    marks = {'face': xs[-1 - int(math.ceil(straight / 0.01))], 'lip': xs[-1]}
    top = zs[-1]
    # a short rounded crown, then the back slope down to the ground
    xr, zr = _trace([(lip, angle, -back_angle, _smooth)])
    xs += [xs[-1] + x for x in xr[1:]]; zs += [top + z for z in zr[1:]]
    xb, zb = _trace([(zs[-1] / math.sin(math.radians(back_angle)), -back_angle, -back_angle, _linear)])
    base = zs[-1]
    xs += [xs[-1] + x for x in xb[1:]]; zs += [base + z for z in zb[1:]]
    xs.append(xs[-1] + 0.02); zs.append(0.0)
    xs, zs = _clean(xs, zs)
    return Profile(xs, zs, marks)


def landing(height, face_angle=20.0, steepen=4.0, knuckle=0.30, deck=0.4, runout=0.8, front_angle=SIDE_ANGLE):
    """A dirt landing: the front of the mound at the angle loose dirt sits at, a flat deck, a rounded knuckle that
    eases into the face, a face that gets a little steeper as it goes down (as real landings are shaped), then a long
    smooth transition back to flat ground. Marks: 'deck', 'knuckle', 'slope' (start of the straight part of the face), 'toe'."""
    up = height / math.sin(math.radians(front_angle))
    round_top = min(0.1, 0.3 * height)
    xs, zs = _trace([(up - round_top, front_angle, front_angle, _linear), (round_top * 1.6, front_angle, 0.0, _smooth)])
    lift = height - zs[-1]
    zs = [z + lift * (x / xs[-1]) for x, z in zip(xs, zs)]     # land the top exactly at `height`
    marks = {'deck': xs[-1]}
    x0, z0 = xs[-1], zs[-1]
    xd, zd = _trace([(deck, 0.0, 0.0, _linear), (knuckle, 0.0, -face_angle, _clothoid)])
    xs += [x0 + x for x in xd[1:]]; zs += [z0 + z for z in zd[1:]]
    marks['knuckle'] = x0 + deck; marks['slope'] = xs[-1]
    # The face: from face_angle to face_angle + steepen, then the transition (as long as needed) back to flat.
    remain = zs[-1]
    trans = min(0.6, 0.5 * remain / math.sin(math.radians(face_angle)))
    trans_drop = trans * math.sin(math.radians(face_angle)) * 0.45
    face_len = max(0.0, (remain - trans_drop) / math.sin(math.radians(face_angle + 0.5 * steepen)))
    x1, z1 = xs[-1], zs[-1]
    xf, zf = _trace([(face_len, -face_angle, -(face_angle + steepen), _linear),
                     (trans, -(face_angle + steepen), 0.0, lambda u: 1 - (1 - u) * (1 - u))])
    xs += [x1 + x for x in xf[1:]]; zs += [z1 + z for z in zf[1:]]
    # whatever height is left is taken out evenly along the transition so it meets the ground smoothly
    over = zs[-1]
    n0 = len(xs) - (len(xf) - 1)
    span = xs[-1] - xs[n0]
    for i in range(n0, len(xs)):
        zs[i] -= over * ((xs[i] - xs[n0]) / span if span > 0 else 1.0)
    marks['toe'] = xs[-1]
    xs.append(xs[-1] + runout); zs.append(0.0)
    xs, zs = _clean(xs, zs)
    return Profile(xs, zs, marks)


def tabletop(height, top=1.2, angle=FACE_ANGLE, face_angle=20.0, **landing_args):
    """Take-off, a flat top and a landing in one mound (a tabletop): the top is where a short jump lands safely."""
    up = takeoff(height, angle)
    lip = up.marks['lip']
    xs = [x for x in up.xs if x <= lip]; zs = up.zs[:len(xs)]
    land = landing(height, face_angle, deck=top, **landing_args)
    start = land.marks['deck']
    for x, z in zip(land.xs, land.zs):
        if x < start:
            continue
        xs.append(lip + 0.01 + (x - start)); zs.append(z)
    marks = dict(up.marks)
    for k in ('knuckle', 'slope', 'toe'):
        marks[k] = lip + 0.01 + land.marks[k] - start
    xs, zs = _clean(xs, zs)
    return Profile(xs, zs, marks)


def whoops(count=7, pitch=1.3, height=0.28, seed=1, jitter=0.04, cup=0.18, skew=0.12):
    """A whoop section as it looks after a few laps: no two whoops quite the same, the back side of each a little
    steeper than the front (riders drive into it), and the tops dug out into a shallow cup by the rear tyre.
    Marks: 'crest0', 'crest1', ... at each top."""
    rnd = random.Random(seed)
    xs, zs, marks = [0.0], [0.0], {}
    x = 0.0
    for k in range(count):
        p = pitch * (1 + rnd.uniform(-jitter, jitter))
        h = height * (1 + rnd.uniform(-jitter, jitter) * 1.5)
        peak = 0.5 + skew * 0.5          # the crest sits past the middle, so the back side is shorter and steeper
        n = 60
        for i in range(1, n + 1):
            u = i / n
            if u <= peak:
                v = 0.5 - 0.5 * math.cos(math.pi * u / peak)
            else:
                v = 0.5 + 0.5 * math.cos(math.pi * (u - peak) / (1 - peak))
            # a cup worn into the top: a small dip in the highest part
            d = (u - peak) / 0.12
            v -= cup * math.exp(-d * d) * v * v
            xs.append(x + p * u); zs.append(h * v)
        marks['crest%d' % k] = x + p * peak
        x += p
    return Profile(xs, zs, marks)


def braking_bumps(length=1.2, spacing=0.22, height=0.012, seed=2, grow=True):
    """A washboard of small ripples where riders brake, growing towards the place they stop. Returns extra(x), to add
    to a profile with Profile.plus or to flat ground."""
    rnd = random.Random(seed)
    phases = [rnd.uniform(0, 2 * math.pi) for _ in range(3)]

    def extra(x):
        if x < 0 or x > length:
            return 0.0
        k = (x / length) if grow else 1.0
        fade = min(1.0, x / 0.15, (length - x) / 0.1)
        wave = 0.5 + 0.5 * math.cos(2 * math.pi * x / spacing + 0.4 * math.sin(phases[0] + x * 3.1))
        return height * (0.4 + 0.6 * k) * wave * fade
    return extra


def wobble(seed=3, amount=0.004, scale=0.7):
    """A small, smooth unevenness in the dirt (a few millimetres), the same every time for the same seed."""
    rnd = random.Random(seed)
    waves = [(rnd.uniform(0.6, 1.4) / scale * (k + 1), rnd.uniform(0, 2 * math.pi), 1.0 / (k + 1)) for k in range(4)]
    total = sum(w[2] for w in waves)

    def extra(x):
        return amount * sum(a * math.sin(f * x * 2 * math.pi + ph) for f, ph, a in waves) / total
    return extra


class Section:
    """The shape across the track: a top surface with a worn tyre groove down the middle and slightly raised edges,
    rounded shoulders, then sides sloping to the ground. y = 0 is the middle of the track."""

    def __init__(self, half_top=0.42, side_angle=SIDE_ANGLE, shoulder=0.05, groove_width=0.07, groove_depth=0.012,
                 groove_offset=0.0, berm=0.0):
        self.half_top = half_top
        self.side_angle = side_angle
        self.shoulder = shoulder
        self.groove_width = groove_width
        self.groove_depth = groove_depth
        self.groove_offset = groove_offset
        self.berm = berm               # metres the outside (positive y) edge is raised, for banked turns

    def half_base(self, h):
        """How far the sides reach out at ground level for a mound of height h (the shoulders take the top corner)."""
        sh = min(self.shoulder, 0.45 * h)
        return self.half_top + max(0.0, h - sh) / math.tan(math.radians(self.side_angle))

    def groove(self, y):
        """How deep the worn tyre line is at y (negative is down), with a lip of pushed-up dirt on each side."""
        if self.groove_depth <= 0:
            return 0.0
        d = (y - self.groove_offset) / (self.groove_width * 0.5)
        dip = -self.groove_depth * math.exp(-d * d * 1.6)
        rim = 0.35 * self.groove_depth * math.exp(-(abs(d) - 1.5) ** 2 * 2.0)
        return dip + rim

    def points(self, h, n_top=16, n_side=4):
        """Points (y, z) across the track, from the left foot to the right foot, for a mound h high at this spot."""
        if h <= 1e-4:
            return [(-self.half_top, 0.0), (self.half_top, 0.0)]
        sh = min(self.shoulder, 0.45 * h)
        t = math.tan(math.radians(self.side_angle))
        left_top = h - sh
        right_top = h - sh + self.berm
        pts = []
        for i in range(n_side + 1):                      # left side, foot to the edge of the top
            u = i / n_side
            pts.append((-self.half_top - left_top / t * (1 - u), left_top * u))
        for i in range(1, n_top):                        # across the top: shoulders, groove, banking
            u = i / n_top
            y = -self.half_top + 2 * self.half_top * u
            edge = min(u, 1 - u) * 2 * self.half_top     # distance in from the nearer edge
            round_off = sh * (1 - min(1.0, edge / (2 * sh))) ** 2
            bank = self.berm * u
            pts.append((y, h - round_off + self.groove(y) + bank))
        for i in range(n_side + 1):                      # right side, edge of the top to the foot
            u = i / n_side
            pts.append((self.half_top + right_top / t * u, right_top * (1 - u)))
        return pts


def wear(slope_deg, groove, height):
    """Which dirt colour a spot of surface gets, from how it is worn:
    'damp' for faces and grooves (watered, compacted, darker), 'dry' for flat tops (dusty, lighter), else 'dirt'."""
    if groove < -0.004 or abs(slope_deg) > 14.0:
        return 'damp'
    if height > 0.05 and abs(slope_deg) < 4.0:
        return 'dry'
    return 'dirt'


def clods(profile, seed=4, count=14, near=('toe', 'slope'), spread=0.35, half_width=0.5):
    """Small lumps of dirt lying about, mostly at the bottom of faces where they are knocked off. Returns a list of
    (x, y, size) on the ground or on the slope, in metres. Deterministic for a seed."""
    rnd = random.Random(seed)
    spots = [profile.marks[k] for k in near if k in profile.marks] or [profile.length * 0.5]
    out = []
    for i in range(count):
        cx = spots[i % len(spots)]
        x = min(profile.length, max(0.0, cx + rnd.gauss(0.0, spread)))
        y = rnd.uniform(-half_width, half_width)
        size = rnd.uniform(0.012, 0.035)
        out.append((round(x, 4), round(y, 4), round(size, 4)))
    return out


def flight(angle, speed=3.0, gravity=2.6, drop=0.0):
    """Distance along the ground and height above the lip of a jump in our world. drop: how much lower the landing
    point is than the lip. Returns (distance, apex height above the lip, time in the air)."""
    a = math.radians(angle)
    vx, vz = speed * math.cos(a), speed * math.sin(a)
    t = (vz + math.sqrt(vz * vz + 2 * gravity * drop)) / gravity
    return vx * t, vz * vz / (2 * gravity), t
