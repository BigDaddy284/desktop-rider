"""Paints Rider 2's kit (from References/Original-Photographs/Rider-2/Kit) into a 4096 atlas.
Input: rider_tris.npz from dump_tris.py (UV triangles + rest-pose world positions + normals).
World axes: +X forward, +Y rider's left, +Z up (metres)."""
import sys, numpy as np
from PIL import Image, ImageDraw, ImageFont
TRIS, OUT = sys.argv[1:3]
S = 4096; PAD = 8
d = np.load(TRIS); UV, POS, NRM, MAT = d["uv"], d["pos"], d["nrm"], d["mat"]; NAMES = list(d["names"])

def hx(h): return np.array([int(h[i:i+2], 16) for i in (0, 2, 4)], np.float32) / 255
BLACK = hx("161616"); YEL = hx("d4f01c"); WHITE = hx("f2f2f2"); RED = hx("c8202a")

# ---------------------------------------------------------------- raster: per-pixel world pos, normal, zone
pos = np.zeros((S, S, 3), np.float32); nrm = np.zeros((S, S, 3), np.float32); zone = np.full((S, S), -1, np.int16)
for t in range(len(UV)):
    q = UV[t] * S; a, b, c = q
    x0 = max(int(q[:, 0].min()) - 1, 0); x1 = min(int(q[:, 0].max()) + 2, S)
    y0 = max(int(q[:, 1].min()) - 1, 0); y1 = min(int(q[:, 1].max()) + 2, S)
    if x1 <= x0 or y1 <= y0: continue
    xs, ys = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
    den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
    if abs(den) < 1e-9: continue
    l1 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
    l2 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
    l3 = 1 - l1 - l2; e = -0.75 / max(abs(den), 1)
    ins = (l1 >= e) & (l2 >= e) & (l3 >= e)
    P = POS[t]; w = np.stack([l1, l2, l3], -1)[ins]
    yy, xx = np.nonzero(ins); yy += y0; xx += x0
    pos[yy, xx] = w @ P; nrm[yy, xx] = NRM[t]; zone[yy, xx] = MAT[t]
img = np.zeros((S, S, 3), np.float32)

def Z(name): return zone == NAMES.index(name)

# ---------------------------------------------------------------- decals
FONT_IT = "/usr/share/fonts/opentype/inter/Inter-BlackItalic.otf"; FONT = "/usr/share/fonts/opentype/inter/Inter-Black.otf"
def text_mask(txt, font=FONT_IT, size=400, track=0):
    f = ImageFont.truetype(font, size)
    w = int(sum(f.getlength(ch) + track for ch in txt) + size); im = Image.new("L", (w, int(size * 1.6)), 0); dr = ImageDraw.Draw(im)
    x = size // 4
    for ch in txt:
        dr.text((x, size // 4), ch, font=f, fill=255); x += f.getlength(ch) + track
    im = im.crop(im.getbbox()); return np.asarray(im, np.float32) / 255

def logo_24mx(size=300):
    t = text_mask("24MX", FONT, size); h, w = t.shape
    out = np.zeros((h, w + h + h // 4, 3), np.float32); al = np.zeros(out.shape[:2], np.float32)
    yy, xx = np.mgrid[0:h, 0:h]; circ = ((yy - h / 2) ** 2 + (xx - h / 2) ** 2) <= (h / 2) ** 2
    out[:h, :h][circ] = RED; al[:h, :h][circ] = 1
    o = h + h // 4; out[:, o:o + w] = np.where(t[..., None] > 0.5, WHITE, out[:, o:o + w]); al[:, o:o + w] = np.maximum(al[:, o:o + w], t)
    return out, al

def stamp(mask_px, u, v, mask, colour, cu, cv, width, height=None, colour_img=None):
    """Composite a decal (alpha `mask`) onto pixels `mask_px` whose decal-plane coords are (u right, v up), centred on
    (cu, cv) with the given width (height from the decal's aspect unless given)."""
    h, w = mask.shape
    if height is None: height = width * h / w
    fu = (u - cu) / width + 0.5; fv = 0.5 - (v - cv) / height
    inside = mask_px & (fu >= 0) & (fu < 1) & (fv >= 0) & (fv < 1)
    iy = np.clip((fv[inside] * h).astype(int), 0, h - 1); ix = np.clip((fu[inside] * w).astype(int), 0, w - 1)
    a = mask[iy, ix][:, None]
    src = colour_img[iy, ix] if colour_img is not None else colour
    img[inside] = img[inside] * (1 - a) + src * a

ONEAL = text_mask("O'NEAL"); ISB = text_mask("ISBORNE", FONT, track=10); NUM = text_mask("11", FONT, track=20)        # race number (was 720 in the photos; Boss asked for 11)
X24, A24 = logo_24mx()
def stack3(gap=0.12):
    h, w = ONEAL.shape; g = int(h * gap); out = np.zeros((3 * h + 2 * g, w), np.float32)
    for i in range(3): out[i * (h + g): i * (h + g) + h] = ONEAL
    return out
ONEAL3 = stack3()

X, Y, Zc = pos[..., 0], pos[..., 1], pos[..., 2]; NX, NY, NZ = nrm[..., 0], nrm[..., 1], nrm[..., 2]

# ---------------------------------------------------------------- jersey
J = Z("Jersey_UV"); img[J] = BLACK
spine_o = np.array([-0.12, 0, 0.92]); dsp = np.array([0.12, 0, 0.28]); dsp /= np.linalg.norm(dsp); fwd = np.array([dsp[2], 0, -dsp[0]])
s = (pos - spine_o) @ dsp; fdot = nrm @ fwd
arm = J & (np.abs(Y) > 0.19)
torso = J & ~arm
front = torso & (fdot > 0.35); back = torso & (fdot < -0.35)
stamp(front, Y, s, ONEAL3, YEL, 0.0, 0.17, 0.25)
img[front & (s > 0.035) & (s < 0.05)] = YEL                       # thin waist band seen on the front
stamp(back, -Y, s, ISB, WHITE, 0.0, 0.285, 0.20)
stamp(back, -Y, s, NUM, WHITE, 0.0, 0.205, min(0.20, 0.11 * NUM.shape[1] / NUM.shape[0]), 0.11)
stamp(back, -Y, s, ONEAL3, YEL, 0.0, 0.085, 0.17)
stamp(back, -Y, s, A24, None, 0.0, 0.015, 0.10, colour_img=X24)
# sleeves: one O'NEAL on the outer side of each upper arm and forearm, reading correctly from that side
for side in (1, -1):
    for (a0, a1, cen) in (((-0.02, 0.18, 1.17), (0.05, 0.34, 1.07), 0.55), ((0.05, 0.345, 1.07), (0.17, 0.345, 1.0), 0.5)):
        A0 = np.array([a0[0], side * a0[1], a0[2]]); A1 = np.array([a1[0], side * a1[1], a1[2]])
        ax = np.array([A1[0] - A0[0], 0, A1[2] - A0[2]]); L = np.linalg.norm(ax); ax /= L
        up = np.array([-ax[2], 0, ax[0]]);  up *= np.sign(up[2]) if up[2] else 1
        t = (pos - A0) @ ax
        sel = arm & (side * Y > 0) & (side * NY > 0.35) & (t > 0) & (t < L)
        xdir = -1 if side > 0 else 1                                  # text runs left-to-right as seen from that side
        stamp(sel, xdir * (t - cen * L), (pos - A0) @ up, ONEAL, YEL, 0.0, 0.0, min(0.75 * L, 0.11))

# ---------------------------------------------------------------- pants
Pn = Z("Pants_UV"); img[Pn] = BLACK
for side in (1, -1):
    H0 = np.array([-0.1, side * 0.1, 0.78]); K = np.array([0.17, side * 0.21, 0.69])
    ax = np.array([K[0] - H0[0], 0, K[2] - H0[2]]); L = np.linalg.norm(ax); ax /= L
    up = np.array([-ax[2], 0, ax[0]]); up *= np.sign(up[2])
    t = (pos - H0) @ ax
    sel = Pn & (side * Y > 0.05) & (side * NY > 0.3)
    xdir = -1 if side > 0 else 1
    stamp(sel, xdir * (t - 0.10 * L), (pos - H0) @ up, ONEAL3, YEL, 0.0, 0.0, 0.145)   # moved up the thigh (Boss, 19 Nov)
img[Z("Trim_knee_UV")] = BLACK

# ---------------------------------------------------------------- helmet and goggles
Hm = Z("Helmet_UV")
n1 = np.sin(X * 61 + 1.3) + np.sin(Y * 73 + Zc * 37) + np.sin(Zc * 55 - X * 29 + 0.7)      # soft blotchy camo
img[Hm] = hx("1c1d1f")
img[Hm & (n1 > 0.9)] = hx("3f4246"); img[Hm & (n1 < -1.3)] = hx("2b2d30")
HD = Z("Trim_helmet_dark_UV"); img[HD] = hx("2a2b2d"); img[HD & (Zc < 1.15)] = BLACK
img[Z("Visor_UV")] = YEL                     # goggle strap (fluo yellow, per Boss)
img[Z("DR_GoggleFrame_UV")] = YEL
img[Z("Trim_lens_UV")] = hx("9a6a2a")         # amber lens
img[Z("Trim_neck_UV")] = BLACK

# ---------------------------------------------------------------- gloves and boots
G = Z("Gloves_UV"); img[G] = BLACK; img[G & (NZ < -0.4)] = hx("3a3a3a")
B = Z("Boots_UV"); img[B] = hx("5b5f64"); img[B & (Zc < 0.37)] = hx("1a1a1a")
img[Z("DR_BootStrap_UV")] = hx("1a1a1a"); img[Z("Trim_buckle_UV")] = hx("b5b8bb")
img[Z("Trim_boot_plate_UV")] = hx("3b3d40"); img[Z("Trim_sole_UV")] = hx("121212")

# ---------------------------------------------------------------- padding + save (V flipped: image row 0 = top = UV v 1)
mask = zone >= 0
for _ in range(PAD):
    grow = np.zeros_like(mask); src = np.zeros_like(img)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        m = np.roll(mask, (dy, dx), (0, 1)) & ~mask & ~grow
        src[m] = np.roll(img, (dy, dx), (0, 1))[m]; grow |= m
    img[grow] = src[grow]; mask |= grow
img[~mask] = BLACK
Image.fromarray((np.clip(img[::-1], 0, 1) * 255 + 0.5).astype(np.uint8)).save(OUT)
print("saved", OUT)
