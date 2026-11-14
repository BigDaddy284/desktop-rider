"""One texture per dirt prop, blended from the three dirt kinds (dirt_texture.py) by how each spot is worn.

The first version gave each kind its own material, so the line between damp faces and dry tops followed the mesh's
faces: dead straight, or (once made ragged) a staircase of squares. Real dirt fades from wet to dry over a hand's
width or so, in irregular patches. Here every pixel gets its own mix:
    damp  on faces steeper than about 14 degrees and in the worn tyre line,
    dry   on flat tops that stand up off the ground,
    dirt  everywhere else, including the loose sides,
with the boundaries softened and bent by noise. The texture is laid over the prop from above (U along the track).

Pure numpy (Blender ships it), no Blender, so it can be tested anywhere (tests/test_dirt_bake.py).
"""
import math

import numpy as np

import dirt_texture as DT
import terrain_shapes as TS

PIXELS_PER_METRE = 320          # plenty: on the desktop one metre is about 100 pixels
MAX_SIZE = 4096


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


class Footprint:
    """The ground area a prop covers, in metres, and the pixel grid laid over it."""

    def __init__(self, x0, y0, x1, y1, ppm=PIXELS_PER_METRE):
        self.x0, self.y0 = x0, y0
        self.width = max(1, min(MAX_SIZE, int(math.ceil((x1 - x0) * ppm))))
        self.height = max(1, min(MAX_SIZE, int(math.ceil((y1 - y0) * ppm))))
        self.sx = (x1 - x0) / self.width            # metres per pixel, along and across
        self.sy = (y1 - y0) / self.height
        self.x1, self.y1 = x0 + self.width * self.sx, y0 + self.height * self.sy

    def xs(self):
        return self.x0 + (np.arange(self.width) + 0.5) * self.sx

    def ys(self):
        """Top row first: row 0 is the largest y, as images are stored."""
        return self.y1 - (np.arange(self.height) + 0.5) * self.sy

    def uv(self, x, y):
        return (x - self.x0) / (self.x1 - self.x0), (y - self.y0) / (self.y1 - self.y0)


def patch_noise(foot, rnd, scales=((3.0, 1.0), (9.0, 0.5), (27.0, 0.25))):
    """Smooth noise over the footprint, about -1..1, with blobs of 1/cells metres for each (cells, weight)."""
    gx = foot.xs(); gy = foot.ys()
    noise = np.zeros((foot.height, foot.width)); total = 0.0
    for cells, amp in scales:
        grid = rnd.random((int(cells * (foot.y1 - foot.y0)) + 3, int(cells * (foot.x1 - foot.x0)) + 3))
        fy = (gy - foot.y0) * cells; fx = (gx - foot.x0) * cells
        iy = np.floor(fy).astype(int); ix = np.floor(fx).astype(int)
        ty = (fy - iy)[:, None]; tx = (fx - ix)[None, :]
        ty = ty * ty * (3 - 2 * ty); tx = tx * tx * (3 - 2 * tx)
        a = grid[iy][:, ix] * (1 - tx) + grid[iy][:, ix + 1] * tx
        b = grid[iy + 1][:, ix] * (1 - tx) + grid[iy + 1][:, ix + 1] * tx
        noise += amp * ((a * (1 - ty) + b * ty) * 2 - 1); total += amp
    return noise / total * 1.6


def groove_depth_at(section, y):
    """terrain_shapes.Section.groove for a whole array at once (the same formula, much faster than one at a time)."""
    d = (y - section.groove_offset) / (section.groove_width * 0.5)
    dip = -section.groove_depth * np.exp(-d * d * 1.6)
    rim = 0.35 * section.groove_depth * np.exp(-(np.abs(d) - 1.5) ** 2 * 2.0)
    return dip + rim


def wear_weights(foot, xs, hs, half_top, groove_depth, seed=7):
    """How much damp, dry and loose dirt each pixel gets: three arrays (height x width) that add up to 1."""
    gx = foot.xs(); gy = foot.ys()
    rnd = np.random.default_rng(seed)
    # Bend where each pixel "looks" along and across the track, so wet and dry dirt meet in an irregular, patchy
    # line rather than straight across at the change of slope.
    wx = gx[None, :] + 0.12 * patch_noise(foot, rnd)
    wy = gy[:, None] + 0.05 * patch_noise(foot, rnd, ((6.0, 1.0), (18.0, 0.4)))
    fine = patch_noise(foot, rnd, ((14.0, 1.0), (40.0, 0.5)))
    xi = np.asarray(xs); hi = np.asarray(hs)
    slope_line = np.degrees(np.arctan(np.gradient(hi, xi)))
    h = np.interp(wx, xi, hi, left=0.0, right=0.0)
    steep = np.abs(np.interp(wx, xi, slope_line, left=0.0, right=0.0)) + 3.0 * fine
    section = TS.Section(half_top=half_top, groove_depth=groove_depth, groove_width=0.075)
    groove = groove_depth_at(section, wy) if groove_depth > 0 else np.zeros_like(wy)
    on_top = 1.0 - smoothstep(half_top - 0.07, half_top + 0.03, np.abs(wy))
    raised = smoothstep(0.004, 0.03, h)
    worn = smoothstep(groove_depth * 0.2, groove_depth * 0.9, -groove) if groove_depth > 0 else 0.0
    damp = on_top * raised * np.clip(smoothstep(9.0, 17.0, steep) + 0.7 * worn, 0.0, 1.0)
    dry = on_top * (1.0 - damp) * smoothstep(7.0, 2.0, steep) * smoothstep(0.03, 0.07, h)
    dirt = 1.0 - damp - dry
    return damp, dry, dirt


def sample(tile, foot):
    """A dirt tile (TILE metres square, top row = largest V) laid over the footprint from above, repeating."""
    size = tile.shape[0]
    ppm = size / DT.TILE
    gx = foot.xs(); gy = foot.ys()
    col = np.floor(gx * ppm).astype(int) % size
    row = (size - 1 - np.floor(gy * ppm).astype(int)) % size
    return tile[row][:, col]


def bake(foot, weights, maps):
    """Blends the three kinds' (colour, normal) maps by the weights. maps: {'damp': (colour, normal), ...}, colour in
    sRGB. Returns (colour sRGB, normal 0..1), each height x width x 3, top row first."""
    colour = np.zeros((foot.height, foot.width, 3)); normal = np.zeros_like(colour)
    for kind, w in zip(('damp', 'dry', 'dirt'), weights):
        c, n = maps[kind]
        lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)     # blend in linear light
        colour += sample(lin, foot) * w[:, :, None]
        normal += (sample(n, foot) * 2 - 1) * w[:, :, None]
    normal /= np.maximum(np.linalg.norm(normal, axis=-1, keepdims=True), 1e-6)
    return DT.to_srgb(colour), normal * 0.5 + 0.5
