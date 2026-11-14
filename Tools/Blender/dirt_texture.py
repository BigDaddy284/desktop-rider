"""Seamless dirt textures for the jumps, so they read as real, ridden track dirt rather than smooth flat colour.

Three kinds, matching the three dirt materials:
    'dirt'  the sides of a mound: loose, mid-brown, lumpy, scattered small stones;
    'damp'  faces and the tyre line: watered and packed, darker and smoother, with tyre-tread prints along the
            direction of travel (the texture's U axis runs along the track);
    'dry'   flat tops: dusty and pale, small cracks, a few stones.
The colours are based on the props' existing three dirt colours, so the look changes but the palette does not.

Pure numpy (Blender ships it), no Blender, so it can be tested anywhere (tests/test_dirt_texture.py).
Every feature wraps around the edges, so the images tile without seams. A seed gives the same image every time.
The images are made when the props are built; like all artwork they are not kept in the repository.
"""
import math

import numpy as np

# The props' dirt colours (Blender base colours, linear) — see build_unity_props.build_dirt.
BASE = {
    'dirt': (0.42, 0.26, 0.14),
    'damp': (0.27, 0.16, 0.085),
    'dry': (0.52, 0.35, 0.20),
}
KINDS = tuple(BASE)


def to_srgb(linear):
    linear = np.clip(linear, 0.0, 1.0)
    return np.where(linear <= 0.0031308, linear * 12.92, 1.055 * np.power(linear, 1 / 2.4) - 0.055)


def periodic_noise(size, cells, rnd):
    """Smooth value noise that wraps around: a cells x cells grid of random values, smoothly interpolated."""
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
    cells = start
    for _ in range(octaves):
        if cells > size:
            break
        total += amp * periodic_noise(size, cells, rnd)
        norm += amp; amp *= falloff; cells *= 2
    total /= norm
    lo, hi = total.min(), total.max()
    return (total - lo) / max(hi - lo, 1e-9)


def _disc_offsets(radius):
    r = int(math.ceil(radius))
    ys, xs = np.mgrid[-r:r + 1, -r:r + 1]
    d = np.sqrt(xs * xs + ys * ys)
    keep = d <= radius
    return ys[keep], xs[keep], d[keep] / max(radius, 1e-9), xs[keep], ys[keep]


def stones(img, rnd, count, rmin, rmax, colour, shade=0.35):
    """Small stones or clods: rounded blobs, lit from the top left, with a soft shadow below right. Wraps round."""
    size = img.shape[0]
    for _ in range(count):
        cy, cx = rnd.integers(0, size, 2)
        radius = rnd.uniform(rmin, rmax)
        tone = np.array(colour) * rnd.uniform(0.8, 1.2)
        # shadow first, offset down and right
        sy, sx, sd, _, _ = _disc_offsets(radius * 1.15)
        yy = (cy + sy + max(1, int(radius * 0.4))) % size; xx = (cx + sx + max(1, int(radius * 0.4))) % size
        img[yy, xx] *= (1 - shade * (1 - sd ** 2))[:, None]
        oy, ox, od, dx, dy = _disc_offsets(radius)
        light = 1.0 + 0.35 * (-(dx + dy) / max(radius, 1e-9)) * 0.5 - 0.25 * od ** 2
        yy = (cy + oy) % size; xx = (cx + ox) % size
        img[yy, xx] = tone[None, :] * light[:, None]


def grain(size, rnd, amount):
    """Per-pixel sandy grain, very lightly softened so it reads as soil rather than television static."""
    g = rnd.normal(0.0, 1.0, (size, size))
    g = (g + np.roll(g, 1, 0) + np.roll(g, 1, 1) + np.roll(g, -1, 0) + np.roll(g, -1, 1) + 3 * g) / 8
    return 1.0 + amount * g


def specks(img, rnd, count, colour, strength=0.6):
    """Single-pixel grit: light sand grains or dark bits of organic matter."""
    size = img.shape[0]
    ys = rnd.integers(0, size, count); xs = rnd.integers(0, size, count)
    img[ys, xs] = img[ys, xs] * (1 - strength) + np.array(colour)[None, :] * strength


def tread(img, rnd, tracks=2, spacing=16, length=7, width=40, depth=0.16):
    """Tyre-tread prints: staggered knobby blocks across a band, repeating along U (the track direction). Worn and
    patchy like real prints: some blocks are missing, each is pressed in a different amount, and edges are soft."""
    size = img.shape[0]
    mask = np.ones((size, size))
    for _ in range(tracks):
        cy = int(rnd.integers(0, size))
        wobble = rnd.uniform(0, 2 * math.pi)
        for x0 in range(0, size, spacing):
            off = int(round(3 * math.sin(2 * math.pi * x0 / size * 2 + wobble)))
            for k, half in enumerate((-1, 1)):
                if rnd.random() < 0.25:
                    continue                                       # worn away or filled in
                d = depth * rnd.uniform(0.5, 1.2)
                jx, jy = rnd.integers(-1, 2, 2)
                xs = (np.arange(length) + x0 + jx + (spacing // 2 if k else 0)) % size
                ys = (np.arange(width // 2 - 2) + cy + off + jy + (1 if half > 0 else -(width // 2) + 1)) % size
                mask[np.ix_(ys, xs)] *= (1 - d)
    # soften the block edges a little
    soft = (mask + np.roll(mask, 1, 0) + np.roll(mask, -1, 0) + np.roll(mask, 1, 1) + np.roll(mask, -1, 1)) / 5
    img *= soft[:, :, None]


def cracks(img, rnd, cells=7, darkness=0.22, thickness=0.018):
    """Faint cracks in dried mud: the edges between random cells (wrapping round)."""
    size = img.shape[0]
    pts = rnd.random((cells * cells, 2))
    ys, xs = np.mgrid[0:size, 0:size] / size
    best = np.full((size, size), 9.0); second = np.full((size, size), 9.0)
    for py, px in pts:
        dy = np.abs(ys - py); dy = np.minimum(dy, 1 - dy)
        dx = np.abs(xs - px); dx = np.minimum(dx, 1 - dx)
        d = np.sqrt(dx * dx + dy * dy)
        closer = d < best
        second = np.where(closer, best, np.minimum(second, d))
        best = np.where(closer, d, best)
    edge = np.clip(1 - (second - best) / thickness, 0, 1)
    img *= (1 - darkness * edge)[:, :, None]


def make(kind, size=512, seed=1):
    """An sRGB image, size x size x 3, values 0..1, top row first."""
    if kind not in BASE:
        raise ValueError('unknown dirt kind: %s' % kind)
    rnd = np.random.default_rng(seed * 101 + KINDS.index(kind))
    base = np.array(BASE[kind])
    broad = fbm(size, rnd, start=3, octaves=3)            # big patches, 10-30 cm
    fine = fbm(size, rnd, start=32, octaves=4)            # small lumps and hollows
    warm = fbm(size, rnd, start=5, octaves=3)             # slightly redder or greyer patches
    if kind == 'damp':
        light = 0.82 + 0.25 * broad + 0.10 * fine
    elif kind == 'dry':
        light = 0.85 + 0.22 * broad + 0.22 * fine
    else:
        light = 0.72 + 0.35 * broad + 0.28 * fine
    tint = np.stack([1.0 + 0.10 * (warm - 0.5), 1.0 - 0.02 * (warm - 0.5), 1.0 - 0.10 * (warm - 0.5)], axis=-1)
    img = base[None, None, :] * light[:, :, None] * tint
    img *= grain(size, rnd, {'damp': 0.05, 'dry': 0.09, 'dirt': 0.11}[kind])[:, :, None]
    if kind == 'dry':
        # dust: lift towards a pale grey-beige in patches
        dust = np.clip((broad - 0.45) * 2.0, 0, 1)[:, :, None] * 0.35
        img = img * (1 - dust) + np.array([0.55, 0.47, 0.36])[None, None, :] * dust
        cracks(img, rnd, cells=16, darkness=0.09, thickness=0.006)
        stones(img, rnd, size // 6, 0.7, 2.0, (0.50, 0.44, 0.36))
        specks(img, rnd, size * 12, (0.66, 0.60, 0.50), 0.35)
        specks(img, rnd, size * 4, (0.20, 0.13, 0.08), 0.4)
    elif kind == 'damp':
        tread(img, rnd)
        stones(img, rnd, size // 8, 0.8, 2.2, (0.20, 0.12, 0.065), shade=0.3)
        specks(img, rnd, size * 6, (0.12, 0.07, 0.04), 0.4)
    else:
        stones(img, rnd, size // 3, 0.7, 2.2, (0.47, 0.40, 0.32))           # stones
        stones(img, rnd, size // 6, 2.5, 7.0, base * 0.9, shade=0.5)        # clods of the same dirt
        specks(img, rnd, size * 10, (0.62, 0.52, 0.40), 0.35)
        specks(img, rnd, size * 6, (0.16, 0.10, 0.06), 0.45)
    return to_srgb(img)


def save_png(path, rgb):
    """Writes the image with Pillow if it is there (tests); Blender uses its own image writer instead."""
    from PIL import Image
    Image.fromarray((np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8)).save(path)
