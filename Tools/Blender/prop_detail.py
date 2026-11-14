"""Dirt props that look like part of a real, ridden track: sloping sides, rounded shoulders, a worn tyre line with a
lip of pushed-up dirt, a little unevenness at the edges, damp faces and dry tops, and loose clods at the feet.

Pure Python, no Blender, so it can be tested anywhere (tests/test_prop_detail.py). build_unity_props.py turns the
result into a Blender mesh. The riding line down the middle keeps the outline the trick animations were made for
(only the shallow tyre groove is cut into it), so the wheels still meet the ground where they did.
"""
import math
import random

import terrain_shapes as TS

DIRT, DAMP, DRY = 0, 1, 2
MATERIALS = ('DR_Dirt', 'DR_DirtDark', 'DR_DirtLight')     # the same three materials (and colours) as before
MIN_HEIGHT = 0.002                                          # keeps every slice a proper solid where it touches the ground
N_TOP, N_SIDE = 28, 8                                       # points across the top and down each side (smooth when shaded)
SIDE_LUMPS = 0.022                                          # how far the loose dirt on the sides bulges in and out, metres


def dirt_mesh(length, fn, half_top=0.45, step=0.02, seed=7, groove_depth=0.006, edge_wobble=0.02, clods=10, back=True):
    """Returns (verts, faces) for a closed dirt mound with outline fn(x), x from 0 to length.
    verts: list of (x, y, z). faces: list of (vertex indices, material index)."""
    xs, hs = outline(length, fn, step, back)
    n = len(xs) - 1
    first = next((i for i, h in enumerate(hs) if h > 1e-5), None)
    if first is None:
        return [], []
    last = max(i for i, h in enumerate(hs) if h > 1e-5)
    lo, hi = max(0, first - 1), min(n, last + 1)
    section = TS.Section(half_top=half_top, groove_depth=groove_depth, groove_width=0.075)
    bumps = Noise2(seed)
    sway = TS.wobble(seed=seed + 1, amount=edge_wobble, scale=0.5)
    lumpy = Noise2(seed + 2)
    patchy = Noise2(seed + 3)
    profile = TS.Profile(xs, hs)

    verts, faces, rings = [], [], []
    for i in range(lo, hi + 1):
        x = xs[i]; h = max(MIN_HEIGHT, hs[i])
        ring = []
        # Where the mound runs out to nothing, the tyre groove fades with it.
        ring_section = section if h > 4 * groove_depth else TS.Section(half_top=half_top, groove_depth=0.25 * h, groove_width=0.075)
        for y, z in ring_section.points(h, n_top=N_TOP, n_side=N_SIDE):
            share = min(1.0, abs(y) / half_top)                 # 0 on the riding line, 1 at the edges and sides
            if abs(y) > half_top + 1e-9:
                out = min(1.0, (abs(y) - half_top) / max(1e-6, section.half_base(h) - half_top))
                # Loose dirt on the sides: lumps, and rills where rain has run down (stretched up the slope).
                bulge = SIDE_LUMPS * (lumpy(x * 9.0, z * 9.0) + 0.6 * lumpy(x * 30.0 + 50.0, z * 5.0)) * min(1.0, h / 0.05)
                y += math.copysign(sway(x) * out + bulge * math.sin(math.pi * min(1.0, out * 1.4)), y)
            if z > 0:
                z = max(0.0005, z + 0.008 * bumps(x * 12.0, y * 12.0) * share * share * min(1.0, h / 0.02))
            ring.append(len(verts)); verts.append((x, y, z))
        rings.append(ring)

    m = len(rings[0])
    top_from, top_to = N_SIDE, m - N_SIDE - 1                   # Section.points: the side points, the top, the side points
    for r in range(len(rings) - 1):
        a, b = rings[r], rings[r + 1]
        xm = 0.5 * (verts[a[0]][0] + verts[b[0]][0])
        slope = profile.slope(xm)
        hm = profile(xm)
        for j in range(m - 1):
            if top_from <= j < top_to:
                ym = 0.5 * (verts[a[j]][1] + verts[a[j + 1]][1])
                # The line between damp and dry dirt is never straight: wet and dry patches run into each other.
                kind = TS.wear(slope + 6.0 * patchy(xm * 6.0, ym * 6.0), section.groove(ym), hm)
                mat = {'damp': DAMP, 'dry': DRY}.get(kind, DIRT)
            else:
                mat = DIRT
            faces.append(((a[j], b[j], b[j + 1], a[j + 1]), mat))
        faces.append(((a[m - 1], b[m - 1], b[0], a[0]), DIRT))     # underside
    faces.append((tuple(rings[0]), DIRT))                       # end caps
    faces.append((tuple(reversed(rings[-1])), DIRT))

    if clods:
        add_clods(verts, faces, profile, section, seed, clods)
    return verts, faces


def outline(length, fn, step=0.02, back=True):
    """The heights the mound is built on: fn sampled every step, plus the sloping back of a take-off.
    Returns (xs, hs)."""
    n = max(2, int(round(length / step)))
    xs = [length * i / n for i in range(n + 1)]
    hs = [max(0.0, fn(x)) for x in xs]
    if back and hs[-1] > 0.01:
        # A take-off ends at its lip. Real dirt does not stop in a wall there: round the crown over and let the back
        # fall away at the angle loose dirt settles at. The bike has left the ground by then, so riding is unchanged.
        xb, hb = back_slope(hs[-1], step)
        xs += [length + x for x in xb]; hs += hb
    return xs, hs


class Noise2:
    """Smooth 2D value noise, about -1..1, the same every time for the same seed. One unit is one noise cell."""

    def __init__(self, seed):
        self.seed = seed * 7919

    def _at(self, i, j):
        h = (i * 374761393 + j * 668265263 + self.seed * 2246822519) & 0xFFFFFFFF
        h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
        return ((h ^ (h >> 16)) & 0xFFFF) / 32767.5 - 1.0

    def __call__(self, x, y):
        i, j = math.floor(x), math.floor(y)
        u, v = x - i, y - j
        u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v)
        a = self._at(i, j) + (self._at(i + 1, j) - self._at(i, j)) * u
        b = self._at(i, j + 1) + (self._at(i + 1, j + 1) - self._at(i, j + 1)) * u
        return a + (b - a) * v


def back_slope(height, step, crown=0.06, angle=TS.SIDE_ANGLE):
    """Points (x after the lip, height) for the back of a take-off: a short rounded crown, then a straight fall."""
    t = math.tan(math.radians(angle))
    xs, hs = [], []
    x = 0.0
    while True:
        x += step * 0.5 if x < crown else step
        drop = t * x * x / (2 * crown) if x < crown else t * (x - crown / 2)
        h = height - drop
        if h <= 0:
            xs.append(x - h / t); hs.append(0.0)
            return xs, hs
        xs.append(x); hs.append(h)


def add_clods(verts, faces, profile, section, seed, count):
    """Small lumps of dirt on the ground beside the mound and past its end, where they get knocked off."""
    xs = profile.xs
    steep = min(range(len(xs) - 1), key=lambda i: profile.zs[i + 1] - profile.zs[i])   # steepest way down
    toe = max(i for i, z in enumerate(profile.zs) if z > 1e-5)
    spots = TS.Profile(profile.xs, profile.zs, {'slope': xs[steep], 'toe': xs[toe]})
    for k, (x, y, size) in enumerate(TS.clods(spots, seed=seed, count=count, spread=0.3, half_width=0.25)):
        base = section.half_base(max(MIN_HEIGHT, profile(x)))
        side = 1 if y >= 0 else -1
        cy = side * (base + abs(y) * 0.6 + size)
        lump(verts, faces, (x, cy, 0.0), size, seed * 31 + k)


def lump(verts, faces, centre, size, seed):
    """A small closed, slightly squashed and irregular octahedron sitting on the ground."""
    rnd = random.Random(seed)
    cx, cy, cz = centre
    s = [size * rnd.uniform(0.8, 1.25) for _ in range(6)]
    pts = [(cx + s[0], cy, cz + 0.45 * size), (cx - s[1], cy, cz + 0.45 * size), (cx, cy + s[2], cz + 0.45 * size),
           (cx, cy - s[3], cz + 0.45 * size), (cx, cy, cz + 0.45 * size + 0.8 * s[4]), (cx, cy, cz)]
    i0 = len(verts); verts.extend(pts)
    px, nx, py, ny, top, bot = range(i0, i0 + 6)
    for a, b in ((px, py), (py, nx), (nx, ny), (ny, px)):
        faces.append(((a, b, top), DAMP))
        faces.append(((b, a, bot), DAMP))


def open_edges(faces):
    """How many edges are not shared by exactly two faces (0 for a closed solid)."""
    count = {}
    for idx, _ in faces:
        for a, b in zip(idx, idx[1:] + idx[:1]):
            key = (min(a, b), max(a, b))
            count[key] = count.get(key, 0) + 1
    return sum(1 for c in count.values() if c != 2)


def centre_line(verts, x_step):
    """Height of the riding line (y nearest 0) at each x, for checking against the outline."""
    best = {}
    for x, y, z in verts:
        k = round(x / x_step)
        if k not in best or abs(y) < abs(best[k][0]) - 1e-9:
            best[k] = (y, z, x)
    return sorted((x, z) for _, z, x in best.values())
