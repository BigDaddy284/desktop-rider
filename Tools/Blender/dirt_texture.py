"""Seamless dirt textures for the jumps, so they read as real, ridden track dirt rather than smooth flat colour.

Three kinds, matching the three dirt materials:
    'dirt'  the sides of a mound: loose, lumpy, mid-brown, with clods and stones;
    'damp'  faces and the tyre line: watered and packed, darker and smoother, with tyre-tread prints and wheel ruts
            along the direction of travel (the texture's U axis runs along the track);
    'dry'   flat tops: dusty and paler, cracked, with a few stones.

Each kind gives a colour image and a normal map made from the same bumps, so lumps, clods, ruts and cracks catch the
light in the game rather than only being painted on.

Scale matters: on the desktop one metre of dirt is only about 100 pixels across, so anything smaller than a centimetre
blurs away. The features that carry the look are a few centimetres to tens of centimetres across: clods, lumps,
ruts, wet patches. Sizes below are in metres; TILE is how many metres one image covers.

The colours are real, dark track dirt (sRGB about #6b4f3a for loose dirt, #4a3628 for damp, #8f7a63 for dry tops),
not the pale tan the first version used, which looked like sand or plastic.

Pure numpy (Blender ships it), no Blender, so it can be tested anywhere (tests/test_dirt_texture.py).
Every feature wraps around the edges, so the images tile without seams. A seed gives the same image every time.
The images are made when the props are built; like all artwork they are not kept in the repository.
"""
import math

import numpy as np

TILE = 2.0                      # metres of dirt one image covers

# Linear colours (what Blender and Unity light with), from the sRGB colours above.
BASE = {
    'dirt': (0.147, 0.078, 0.043),
    'damp': (0.069, 0.036, 0.022),
    'dry': (0.22, 0.158, 0.103),
}
KINDS = tuple(BASE)


def to_srgb(linear):
    linear = np.clip(linear, 0.0, 1.0)
    return np.where(linear <= 0.0031308, linear * 12.92, 1.055 * np.power(linear, 1 / 2.4) - 0.055)


def periodic_noise(size, cells, rnd):
    """Smooth value noise that wraps around: a cells x cells grid of random values, smoothly interpolated."""
    cells = max(1, int(cells))
    grid = rnd.random((cells, cells))
    coords = np.arange(size) * cells / size
    i0 = np.floor(coords).astype(int) % cells
    i1 = (i0 + 1) % cells
    t = coords - np.floor(coords)
    t = t * t * (3 - 2 * t)
    a = grid[i0][:, i0] * (1 - t)[None, :] + grid[i0][:, i1] * t[None, :]
    b = grid[i1][:, i0] * (1 - t)[None, :] + grid[i1][:, i1] * t[None, :]
    return a * (1 - t)[:, None] + b * t[:, None]


def fbm(size, rnd, start=4, octaves=6, falloff=0.55):
    """Several layers of noise, each finer and fainter, scaled to 0..1."""
    total = np.zeros((size, size)); amp = 1.0; norm = 0.0
    cells = min(start, size)                 # a small image cannot hold finer noise than one cell a pixel
    for _ in range(octaves):
        if cells > size:
            break
        total += amp * periodic_noise(size, cells, rnd)
        norm += amp; amp *= falloff; cells *= 2
    total /= norm
    lo, hi = total.min(), total.max()
    return (total - lo) / max(hi - lo, 1e-9)


def cells_for(size_m):
    """Noise cells across the tile for features about size_m metres across."""
    return max(1, int(round(TILE / size_m)))


def blur(a, passes=1):
    """A light, wrapping blur."""
    for _ in range(passes):
        a = (a * 4 + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 8
    return a


def lumps(height, colour, rnd, count, r_m, h_m, tone, px_per_m, squash=1.0):
    """Clods or stones: rounded bumps sitting on the dirt. Adds to height (metres) and paints colour, darkening
    the ground just round each one a little (contact shadow). Radii r_m = (smallest, largest), in metres. Wraps round."""
    size = height.shape[0]
    for _ in range(count):
        cy, cx = rnd.integers(0, size, 2)
        r = rnd.uniform(*r_m) * px_per_m
        ri = int(math.ceil(r * 1.4)) + 1
        ys, xs = np.mgrid[-ri:ri + 1, -ri:ri + 1]
        # slightly irregular outline: radius wobbles with angle
        ang = np.arctan2(ys, xs)
        wob = 1.0 + sum(rnd.uniform(0.0, 0.12) * np.sin(k * ang + rnd.uniform(0, 6.3)) for k in (2, 3, 5, 7))
        d = np.sqrt((xs / squash) ** 2 + ys ** 2) / (r * wob)
        yy = (cy + ys) % size; xx = (cx + xs) % size
        inside = d < 1.0
        bump = np.where(inside, np.sqrt(np.clip(1 - d * d, 0, 1)), 0.0) * h_m * rnd.uniform(0.7, 1.2)
        height[yy[inside], xx[inside]] += bump[inside]
        ring = (d >= 1.0) & (d < 1.4)
        colour[yy[ring], xx[ring]] *= (0.82 + 0.18 * (d[ring] - 1.0) / 0.4)[:, None]
        t = np.array(tone) * rnd.uniform(0.75, 1.25)
        # lit a little from above (the normal map does the rest) and darker towards the edge where it meets the ground
        shade = 0.8 + 0.25 * np.sqrt(np.clip(1 - d * d, 0, 1)) - 0.08 * (xs + ys) / max(r, 1.0)
        colour[yy[inside], xx[inside]] = t[None, :] * shade[inside][:, None]


def ruts(height, colour, rnd, count, px_per_m, depth_m=0.006, width_m=0.06, darken=0.75):
    """Wheel ruts along U (the track direction): wavy channels, darker at the bottom where wet dirt collects."""
    size = height.shape[0]
    x = np.arange(size) / size
    for _ in range(count):
        centre = rnd.random() * size
        wave = sum(rnd.uniform(1, 4) / (k + 1) * np.sin(2 * math.pi * (k + 1) * x + rnd.uniform(0, 6.3)) for k in range(3))
        path = centre + wave * 0.02 * px_per_m
        w = width_m * px_per_m * rnd.uniform(0.7, 1.3)
        rows = np.arange(size)[:, None]
        dist = np.abs(((rows - path[None, :]) + size / 2) % size - size / 2) / (w * 0.5)
        dip = np.exp(-dist * dist * 1.5) * depth_m * rnd.uniform(0.6, 1.2)
        rim = np.exp(-(dist - 1.5) ** 2 * 2.5) * depth_m * 0.35
        fade = 0.4 + 0.6 * periodic_noise(size, 6, rnd)[0][None, :]          # ruts come and go along the track
        height += (rim - dip) * fade
        colour *= (1 - (1 - darken) * np.exp(-dist * dist * 1.5) * fade)[:, :, None]


def tread(height, rnd, px_per_m, tracks=2, depth_m=0.0015):
    """Tyre-tread prints: staggered knobby blocks across a band, repeating along U. Worn and patchy like real prints."""
    size = height.shape[0]
    spacing = max(4, int(0.035 * px_per_m)); length = max(2, int(0.016 * px_per_m)); width = max(6, int(0.09 * px_per_m))
    marks = np.zeros((size, size))
    for _ in range(tracks):
        cy = int(rnd.integers(0, size))
        wobble = rnd.uniform(0, 2 * math.pi)
        for x0 in range(0, size, spacing):
            off = int(round(0.01 * px_per_m * math.sin(2 * math.pi * x0 / size * 2 + wobble)))
            for k, half in enumerate((-1, 1)):
                if rnd.random() < 0.3:
                    continue                                       # worn away or filled in
                d = rnd.uniform(0.5, 1.2)
                xs = (np.arange(length) + x0 + (spacing // 2 if k else 0)) % size
                ys = (np.arange(width // 2 - 1) + cy + off + (1 if half > 0 else -(width // 2) + 1)) % size
                marks[np.ix_(ys, xs)] = np.maximum(marks[np.ix_(ys, xs)], d)
    height -= blur(marks, 3) * depth_m


def cracks(height, colour, rnd, cells, darkness=0.3, thickness=0.012, depth_m=0.002):
    """Cracks in dried mud: the edges between random cells (wrapping round)."""
    size = height.shape[0]
    pts = rnd.random((cells * cells, 2))
    ys, xs = np.mgrid[0:size, 0:size] / size
    ys = (ys + 0.03 * (periodic_noise(size, 8, rnd) - 0.5)) % 1.0       # bend the cracks so the plates are irregular
    xs = (xs + 0.03 * (periodic_noise(size, 8, rnd) - 0.5)) % 1.0
    best = np.full((size, size), 9.0); second = np.full((size, size), 9.0)
    for py, px in pts:
        dy = np.abs(ys - py); dy = np.minimum(dy, 1 - dy)
        dx = np.abs(xs - px); dx = np.minimum(dx, 1 - dx)
        d = np.sqrt(dx * dx + dy * dy)
        closer = d < best
        second = np.where(closer, best, np.minimum(second, d))
        best = np.where(closer, d, best)
    edge = np.clip(1 - (second - best) / thickness, 0, 1)
    edge *= np.clip((periodic_noise(size, 5, rnd) - 0.3) * 3, 0, 1)                         # not every crack runs the whole way
    height -= edge * depth_m
    colour *= (1 - darkness * edge)[:, :, None]


def normal_map(height, px_per_m, strength=1.0):
    """Tangent-space normal map (OpenGL style, green up) from a height field in metres, values 0..1.
    Row 0 is the top of the image, which is the highest V."""
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5 * px_per_m * strength
    dv = (np.roll(height, 1, 0) - np.roll(height, -1, 0)) * 0.5 * px_per_m * strength
    n = np.stack([-dx, -dv, np.ones_like(height)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def make_maps(kind, size=1024, seed=1):
    """(colour, normal) images, each size x size x 3, values 0..1, top row first. Colour is sRGB."""
    if kind not in BASE:
        raise ValueError('unknown dirt kind: %s' % kind)
    rnd = np.random.default_rng(seed * 101 + KINDS.index(kind))
    ppm = size / TILE
    base = np.array(BASE[kind])
    patches = fbm(size, rnd, start=cells_for(0.5), octaves=3)          # wet and dry patches, tens of centimetres
    lumpy = fbm(size, rnd, start=cells_for(0.12), octaves=4)            # lumps and hollows, a few centimetres
    grain = fbm(size, rnd, start=cells_for(0.015), octaves=3)           # coarse soil grain
    warm = fbm(size, rnd, start=cells_for(0.7), octaves=2)              # redder or greyer soil

    rough = {'damp': 0.4, 'dry': 0.6, 'dirt': 1.0}[kind]
    height = (lumpy - 0.5) * 0.012 * rough + (grain - 0.5) * 0.002
    light = {'damp': 0.75 + 0.45 * patches, 'dry': 0.78 + 0.4 * patches, 'dirt': 0.7 + 0.55 * patches}[kind]
    speckle = blur(rnd.random((size, size)), 1)                          # millimetre grit
    light = light * (0.8 + 0.4 * lumpy) * (0.82 + 0.36 * grain) * (0.85 + 0.3 * speckle)
    tint = np.stack([1.0 + 0.12 * (warm - 0.5), np.ones_like(warm), 1.0 - 0.15 * (warm - 0.5)], axis=-1)
    colour = base[None, None, :] * light[:, :, None] * tint

    if kind == 'dry':
        # dust lifts the colour to a pale grey-beige in patches; cracks between dried plates
        dust = np.clip((patches - 0.45) * 1.8, 0, 1)[:, :, None] * 0.3
        colour = colour * (1 - dust) + np.array([0.25, 0.19, 0.13])[None, None, :] * dust
        cracks(height, colour, rnd, cells=cells_for(0.25), darkness=0.22, thickness=0.006)
        lumps(height, colour, rnd, int(14 * TILE * TILE), (0.005, 0.014), 0.004, (0.30, 0.26, 0.21), ppm)
        lumps(height, colour, rnd, int(8 * TILE * TILE), (0.012, 0.03), 0.01, base * 0.8, ppm)
    elif kind == 'damp':
        # packed and darker; wet, near-black patches where water sits; ruts and tread prints from the wheels
        wet = np.clip((0.55 - patches) * 2.5, 0, 1)[:, :, None]
        colour *= 1 - 0.35 * wet
        ruts(height, colour, rnd, 3, ppm)
        tread(height, rnd, ppm)
        lumps(height, colour, rnd, int(10 * TILE * TILE), (0.008, 0.025), 0.008, base * 0.75, ppm)
    else:
        # loose dirt: lots of clods of the same dirt, some stones, some darker damp clods knocked off the faces
        lumps(height, colour, rnd, int(16 * TILE * TILE), (0.012, 0.035), 0.014, base * 1.05, ppm)
        lumps(height, colour, rnd, int(7 * TILE * TILE), (0.01, 0.025), 0.01, np.array(BASE['damp']) * 1.4, ppm)
        lumps(height, colour, rnd, int(10 * TILE * TILE), (0.004, 0.01), 0.005, (0.2, 0.17, 0.14), ppm)
    return to_srgb(colour), normal_map(height, ppm, strength=1.5)


def make(kind, size=1024, seed=1):
    """The colour image only: sRGB, size x size x 3, values 0..1, top row first."""
    return make_maps(kind, size, seed)[0]


def save_png(path, rgb):
    """Writes the image with Pillow if it is there (tests); Blender uses its own image writer instead."""
    from PIL import Image
    Image.fromarray((np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8)).save(path)
