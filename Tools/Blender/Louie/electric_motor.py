"""Turns the bike copy in desktop_rider_2.blend from petrol to electric (engine area and frame), and saves it.

    blender --background desktop_rider_2.blend --python electric_motor.py -- <atlas in png> <atlas out png> <stomp decal png> [saveas=<copy .blend>]

* removes the exhaust header, silencer, cylinder + fins, crankcase, carburettor, ignition cover, kick-start, gear lever,
  rear brake pedal (all controls are on the bars), the old single down tube, the lower engine cradle and the old skid
  plate;
* adds a finned battery box, a round motor with a cover on the right, a Y-shaped twin-spar frame (head -> down and back
  -> junction, then one branch up to the top rail and one down to the swingarm pivot), and the STOMP front cover that
  wraps around and under the motor back to the footpegs, all weighted 100% to the bike rig's `chassis` bone (like the
  engine they replace), in one new material slot;
* new faces use small flat-colour patches in the free strip at the top of the bike atlas (v 0.955..1), the cover's
  front face uses the STOMP decal there;
* writes the atlas with those patches (and the frame painted black) and swaps it in.
Reference photos: References/Original-Photographs/Bike-2/ (project files).
World axes: +X forward, +Y bike's left, +Z up. Sizes in metres.
"""
import bpy, bmesh, sys, os, math
import numpy as np
from collections import defaultdict
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
ATLAS_IN, ATLAS_OUT, DECAL = a[:3]
OPT = dict(x.split("=", 1) for x in a[3:] if "=" in x); SAVE_AS = OPT.get("saveas")
sc = bpy.context.scene; sc.frame_set(1)
ob = bpy.data.objects["race_bike_youth"]; me = ob.data; MW = ob.matrix_world
slot_names = [s.name for s in ob.material_slots]
REMOVE = ("Trim_exhaust_UV", "DR_Silencer_UV", "Trim_a39997_UV", "Trim_918e93_UV", "Trim_ecbe2a_UV", "DR_Ignition_UV",
          "Trim_4b4c57_UV", "Trim_radiator_UV")   # the old chain (rebuilt below, on the sprockets and clear of the swingarm)
# whole pieces to remove: (material, world bbox lo, bbox hi) of the connected piece, matched within 6 mm
REMOVE_PIECES = [
    ("Trim_b1aeaf_UV", (-0.022, -0.165, 0.381), (0.082, -0.087, 0.485)),   # kick-start
    ("Trim_b1aeaf_UV", (0.014, 0.081, 0.354), (0.118, 0.127, 0.403)),      # gear lever
    ("Trim_b1aeaf_UV", (-0.018, -0.118, 0.329), (0.125, -0.083, 0.366)),   # rear brake pedal
    ("Frame_UV", (0.194, -0.016, 0.471), (0.28, 0.016, 0.735)),            # old single down tube
    ("Frame_UV", (-0.081, 0.019, 0.306), (0.217, 0.065, 0.475)),           # old lower cradle, left
    ("Frame_UV", (-0.081, -0.065, 0.306), (0.217, -0.019, 0.475)),         # old lower cradle, right
    ("Trim_3b3c45_UV", (-0.017, -0.049, 0.293), (0.186, 0.049, 0.303)),    # old skid plate
    ("Frame_UV", (-0.114, 0.021, 0.353), (0.261, 0.069, 0.799)),           # old main spar + top rail, left
    ("Frame_UV", (-0.114, -0.069, 0.353), (0.261, -0.021, 0.799)),         # old main spar + top rail, right
    ("Trim_3b3c45_UV", (-0.110, -0.023, 0.493), (0.000, 0.023, 0.574)),    # old engine-bay block
]
# pieces moved outwards (+Y) so the chain line clears the swingarm's left arm (y 0.050..0.086)
MOVE_PIECES = [
    ("Trim_3b3c45_UV", (-0.014, 0.051, 0.332), (0.048, 0.060, 0.396)),     # front sprocket
]
# rear sprocket: Trim_3b3c45 faces on the left of the rear hub (y 0.048..0.062) within 0.095 of the axle
REAR_AXLE = Vector((-0.544, 0.248))
CHAIN_SHIFT = 0.0   # chain back in Harvey's plane: rear sprocket inside the swingarm arm, chain inboard of the frame

def hx(h): return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
PATCH = {"black": "161616", "fin": "2c2c2e", "cover": "0f0f0f", "ring": "8c8f93", "red": "d0201a", "rail": "343537",
         "plate": "202022", "slot": "070707", "chain": "b08a2c", "plastic": "1c1c1e", "swingarm": "232325",
         "plastic_hi": "28282b"}
PATCH_UV = {k: (0.02 + 0.03 * i, 0.975) for i, k in enumerate(PATCH)}     # centre of each 0.02 x 0.02 patch
HALF = 0.008
DECAL_UV = (0.40, 0.9565, 0.50, 0.9965)                                     # u0, v0, u1, v1 of the STOMP decal

# ---------------------------------------------------------------- 1. remove the petrol parts
bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
rm_idx = {i for i, n in enumerate(slot_names) if n in REMOVE}
dead = {f for f in bm.faces if f.material_index in rm_idx}
def find_pieces(mname, lo, hi):
    """Faces of one connected piece (faces sharing a vertex position) of that material whose bbox matches."""
    mi = slot_names.index(mname)
    faces = [f for f in bm.faces if f.material_index == mi]
    parent = {f.index: f.index for f in faces}
    def find(i):
        while parent[i] != i: parent[i] = parent[parent[i]]; i = parent[i]
        return i
    key = defaultdict(list)
    for f in faces:
        for v in f.verts: key[tuple(round(c / 0.004) for c in MW @ v.co)].append(f.index)
    for l in key.values():
        for j in l[1:]: parent[find(j)] = find(l[0])
    groups = defaultdict(list)
    for f in faces: groups[find(f.index)].append(f)
    hit = []
    for g in groups.values():
        P = [MW @ v.co for f in g for v in f.verts]
        glo = [min(p[k] for p in P) for k in range(3)]; ghi = [max(p[k] for p in P) for k in range(3)]
        if all(abs(glo[k] - lo[k]) < 0.006 and abs(ghi[k] - hi[k]) < 0.006 for k in range(3)):
            hit += g
    print("ELECTRIC piece", mname, lo, "faces", len(hit))
    assert hit, "piece not found"
    return hit
for mname, lo, hi in REMOVE_PIECES:
    dead.update(find_pieces(mname, lo, hi))
# Harvey's rear shock (body + lower link rod, and its grey lower mount) - the shock moves forward behind the battery
# (left photo) and a mud flap takes this place (Boss). Faces of that material lying wholly inside the box.
def faces_in(mname, lo, hi):
    mi = slot_names.index(mname)
    hit = [f for f in bm.faces if f.material_index == mi and
           all(lo[k] <= (MW @ v.co)[k] <= hi[k] for v in f.verts for k in range(3))]
    print("ELECTRIC box", mname, lo, "faces", len(hit)); return hit
_shock = faces_in("Trim_3b3c45_UV", (-0.27, -0.025, 0.31), (-0.09, 0.025, 0.64))
_mount = faces_in("Trim_b1aeaf_UV", (-0.20, -0.06, 0.345), (-0.14, 0.06, 0.40))
_extra = [f for f in _shock if f not in dead]
print("ELECTRIC shock faces not already removed", len(_extra), [tuple(round(c, 3) for c in MW @ f.calc_center_median()) for f in _shock if f in dead][:6])
assert len(_extra) == 48 and len(_mount) == 12, (len(_extra), len(_mount))
dead.update(_shock); dead.update(_mount)
# Harvey's shock as it looks (Boss 19 Nov 16:26: "same look as Harvey's but black"): keep a copy of its faces (spring,
# body, eyes and the lower mount) in world space before they are deleted; it is rebuilt at Louie's shock position below.
H_B, H_T = Vector((-0.182, 0, 0.356)), Vector((-0.114, 0, 0.629))      # Harvey's bottom and top eye centres (x, z)
def _near_axis(f):
    a = Vector((H_T.x - H_B.x, H_T.z - H_B.z)); L = a.length; a /= L; n = Vector((-a.y, a.x))
    for v in f.verts:
        q = MW @ v.co; d = Vector((q.x - H_B.x, q.z - H_B.z))
        if not (-0.12 <= d.dot(a) / L <= 1.12 and abs(d.dot(n)) < 0.05): return False
    return True
_spring = faces_in("Trim_ecbe2a_UV", (-0.27, -0.06, 0.31), (-0.05, 0.06, 0.64))
HARVEY_SHOCK = ([("fin", [tuple(MW @ v.co) for v in f.verts]) for f in _spring] +
                [("black", [tuple(MW @ v.co) for v in f.verts]) for f in _shock if _near_axis(f)] +
                [("mount", [tuple(MW @ v.co) for v in f.verts]) for f in _mount])
assert len(HARVEY_SHOCK) == 124 + 36 + 12, len(HARVEY_SHOCK)
# the old fuel tank's white sides showing under the shrouds, both sides (Boss: delete) - the whole tank shell between
# the shrouds goes (the battery fills that space now); the shrouds themselves (|y| out to 0.119+) are not inside the box
# only the rear part of the tank (back to x 0.136) goes; the front part by the steering head stays (Boss: put it back)
# and gets a cap on its open back end below
_tank = faces_in("Plastics_UV", (-0.040, -0.1145, 0.62), (0.136, 0.1145, 0.79))
assert len(_tank) >= 30, len(_tank)
dead.update(_tank)
# radiator shrouds (Boss, marked): the top rear corner of each shroud is pulled back towards the frame joint under the
# seat, so the shroud's own panel and edge strip stretch to it (one clean piece, nothing added)
_pi = slot_names.index("Plastics_UV"); _sv = set()
for f in bm.faces:
    if f.material_index != _pi or f in dead: continue
    for v in f.verts:
        w = MW @ v.co
        if abs(w.x) < 0.002 and 0.09 < abs(w.y) < 0.122 and abs(w.z - 0.7323) < 0.002: _sv.add(v)
_pos = {tuple(round(c, 3) for c in MW @ v.co) for v in _sv}; assert len(_pos) == 4, _pos   # 2 per side (split mesh: several vert copies)
for v in _sv:
    w = MW @ v.co; v.co = MW.inverted() @ Vector((-0.074, w.y, 0.729))
print("ELECTRIC shroud corners moved", len(_sv), "tank faces removed", len(_tank))
moved = set()
for mname, lo, hi in MOVE_PIECES:
    for f in find_pieces(mname, lo, hi):
        moved.update(f.verts)
_mi = slot_names.index("Trim_3b3c45_UV"); n_rear = 0
for f in bm.faces:
    if f.material_index != _mi: continue
    c = MW @ f.calc_center_median()
    if 0.048 <= c.y <= 0.062 and (Vector((c.x, c.z)) - REAR_AXLE).length < 0.095:
        moved.update(f.verts); n_rear += 1
print("ELECTRIC rear sprocket faces", n_rear); assert n_rear >= 20
inv0 = MW.inverted()
SPROCKET_NEW = (0.017 + (183 - 145) * 0.0017 * 37 / 40, 0.298 + (188 - 148) * 0.0017 * 37 / 40)
front_sp = set()
for f in find_pieces(*MOVE_PIECES[0]): front_sp.update(f.verts)
for v in moved:
    d = Vector((0, CHAIN_SHIFT, 0))
    if v in front_sp: d += Vector((SPROCKET_NEW[0] - 0.017, 0, SPROCKET_NEW[1] - 0.364))
    v.co = inv0 @ (MW @ v.co + d)
n_removed = len(dead)
bmesh.ops.delete(bm, geom=list(dead), context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')

# ---------------------------------------------------------------- 1b. swingarm pivot moves to the photo position
# (rear axle stays): swingarm-weighted vertices are sheared so the arm runs from the new pivot to the same axle
OLD_PIVOT, AXLE = Vector((-0.090, 0.405)), Vector((-0.544, 0.248))
NEW_PIVOT = Vector((0.017 + (25 - 15) * 0.0017, 0.298 + (265 - 228) * 0.0017))
dl0 = bm.verts.layers.deform.verify(); SW = ob.vertex_groups["swingarm"].index; n_sw = 0
for v in bm.verts:
    if v[dl0].get(SW, 0) < 0.5: continue
    p = MW @ v.co; q = Vector((p.x, p.z))
    t = max(0.0, min(1.0, (q - OLD_PIVOT).dot(AXLE - OLD_PIVOT) / (AXLE - OLD_PIVOT).length_squared))
    d = (NEW_PIVOT - OLD_PIVOT) * (1 - t)
    v.co = MW.inverted() @ Vector((p.x + d.x, p.y, p.z + d.y)); n_sw += 1
print("ELECTRIC swingarm verts moved", n_sw, "new pivot", tuple(round(c, 4) for c in NEW_PIVOT))
# both swingarm arms extended along their own line so their ends reach past and cover the rear axle
_si = slot_names.index("DR_Swingarm_UV"); arm_end = set()
for f in bm.faces:
    if f.material_index != _si: continue
    for v in f.verts:
        p = MW @ v.co
        if p.x < -0.50 and abs(p.y) > 0.06: arm_end.add(v)
_pos = {tuple(round(c, 4) for c in (MW @ v.co)) for v in arm_end}
print("ELECTRIC arm end positions", len(_pos)); assert len(_pos) == 8
ARM_DIR = (AXLE - NEW_PIVOT).normalized(); ARM_EXT = 0.060
for v in arm_end:
    p = MW @ v.co; v.co = MW.inverted() @ Vector((p.x + ARM_DIR.x * ARM_EXT, p.y, p.z + ARM_DIR.y * ARM_EXT))
# rear axle cut flush with the outer faces of the swingarm arms (was poking out to y +-0.097)
_axle = faces_in("Trim_b1aeaf_UV", (-0.553, -0.098, 0.240), (-0.535, 0.098, 0.256))
assert len(_axle) == 20, len(_axle)
_ax_v = {v for f in _axle for v in f.verts}
for v in _ax_v:
    p = MW @ v.co
    if abs(p.y) > 0.0865: v.co = MW.inverted() @ Vector((p.x, math.copysign(0.0865, p.y), p.z))
# Harvey's black chain-guide block under the left arm is removed (Boss, 19 Nov)
_guide = faces_in("Trim_3b3c45_UV", (-0.474, 0.039, 0.135), (-0.321, 0.072, 0.199))
assert len(_guide) == 12, len(_guide)
bmesh.ops.delete(bm, geom=_guide, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')

# ---------------------------------------------------------------- 2. new parts (built in world space, stored in object space)
inv = MW.inverted()
mat = bpy.data.materials.get("DR2_Electric_UV") or bpy.data.materials["Frame_UV"].copy()
mat.name = "DR2_Electric_UV"
if "DR2_Electric_UV" not in [s.name for s in ob.material_slots]:
    me.materials.append(mat)
new_mi = [s.name for s in ob.material_slots].index("DR2_Electric_UV")
uvl = bm.loops.layers.uv.active; dl = bm.verts.layers.deform.verify()
chassis = ob.vertex_groups["chassis"].index
GI = {g.name: g.index for g in ob.vertex_groups}
WEIGHT = None          # None: 100% chassis; otherwise fn(world point) -> {group name: weight}
added = []

def face(pts, colour, uvs=None):
    vs = []
    for p in pts:
        v = bm.verts.new(inv @ Vector(p))
        for g, w in (WEIGHT(p) if WEIGHT else {"chassis": 1.0}).items():
            if w > 0: v[dl][GI[g]] = w
        vs.append(v)
    f = bm.faces.new(vs); f.material_index = new_mi; f.smooth = False
    n = len(vs)
    for i, l in enumerate(f.loops):
        if uvs: l[uvl].uv = uvs[i]
        else:                                                 # tiny non-zero UV polygon inside the colour patch
            cu, cv = PATCH_UV[colour]; ang = 2 * math.pi * i / n
            l[uvl].uv = (cu + HALF * math.cos(ang), cv + HALF * math.sin(ang))
    added.append(f); return f

def ccw(profile):
    s = sum(profile[i][0] * profile[(i + 1) % len(profile)][1] - profile[(i + 1) % len(profile)][0] * profile[i][1] for i in range(len(profile)))
    return profile if s > 0 else list(reversed(profile))

def prism(profile, y0, y1, colour, side_colour=None, edge_uv=None):
    """Extrude a polygon in XZ between y0 < y1. edge_uv: {edge index: fn(point list) -> uv list} for special faces."""
    profile = ccw(profile); n = len(profile)
    face([(x, y0, z) for x, z in profile], side_colour or colour)
    face([(x, y1, z) for x, z in reversed(profile)], side_colour or colour)
    for i in range(n):
        (xa, za), (xb, zb) = profile[i], profile[(i + 1) % n]
        pts = [(xa, y1, za), (xb, y1, zb), (xb, y0, zb), (xa, y0, za)]
        face(pts, colour, edge_uv(i, pts) if edge_uv else None)
    return profile

def box(x0, x1, y0, y1, z0, z1, colour):
    prism([(x0, z0), (x1, z0), (x1, z1), (x0, z1)], y0, y1, colour)

def bar(p0, p1, h, y0, y1, colour):
    """A straight tube of height h in the XZ plane from p0 to p1 (square ends), between y0 and y1."""
    dx, dz = p1[0] - p0[0], p1[1] - p0[1]; L = math.hypot(dx, dz); nx, nz = -dz / L * h / 2, dx / L * h / 2
    prism([(p0[0] - nx, p0[1] - nz), (p1[0] - nx, p1[1] - nz), (p1[0] + nx, p1[1] + nz), (p0[0] + nx, p0[1] + nz)], y0, y1, colour)

def taper_bar(p0, p1, h, ya, yb, colour):
    """Like bar(), but its y range changes from ya=(y0, y1) at p0 to yb at p1 (rails converging onto the head tube)."""
    dx, dz = p1[0] - p0[0], p1[1] - p0[1]; L = math.hypot(dx, dz); nx, nz = -dz / L * h / 2, dx / L * h / 2
    c = [((p0[0] - nx, p0[1] - nz), ya), ((p1[0] - nx, p1[1] - nz), yb), ((p1[0] + nx, p1[1] + nz), yb), ((p0[0] + nx, p0[1] + nz), ya)]
    if ccw([q for q, _ in c]) != [q for q, _ in c]: c = list(reversed(c))
    n = len(c)
    face([(x, y[0], z) for (x, z), y in c], colour)
    face([(x, y[1], z) for (x, z), y in reversed(c)], colour)
    for i in range(n):
        ((xa, za), ya_), ((xb, zb), yb_) = c[i], c[(i + 1) % n]
        face([(xa, ya_[1], za), (xb, yb_[1], zb), (xb, yb_[0], zb), (xa, ya_[0], za)], colour)

def cylinder_y(cx, cz, r, y0, y1, colour, cap=None, seg=18):
    prism([(cx + r * math.cos(2 * math.pi * k / seg), cz + r * math.sin(2 * math.pi * k / seg)) for k in range(seg)], y0, y1, colour, cap)

# ------------------------------------------------------------------ traced from Boss's side-on photos
# Right photo bike2-motor-right-side.png (225 x 324 px): X = 0.017 + (px - 15) * S,  Z = 0.298 + (265 - py) * S
# Left  photo bike2-motor-left-side.png  (217 x 225 px): X = 0.017 + (183 - px) * SL, Z = 0.298 + (188 - py) * SL
# Both anchored on the footpeg centre (the pegs stay where they are). S fits the photo's engine bay between the peg
# and this bike's front tyre; SL from the peg-to-pivot height, which is the same on both sides.
S = 0.0017; SL = S * 37 / 40
def R(px, py): return (0.017 + (px - 15) * S, 0.298 + (265 - py) * S)
def L(px, py): return (0.017 + (183 - px) * SL, 0.298 + (188 - py) * SL)
PIVOT = R(25, 228)                      # swingarm pivot (left photo agrees: L(170, 148))
MOTOR, MOTOR_R = R(80, 228), 32 * S     # round motor cover
SPROCKET, SPROCKET_R = L(145, 148), 15 * SL
print("ELECTRIC pivot %.3f %.3f motor %.3f %.3f sprocket %.3f %.3f" % (PIVOT + MOTOR + SPROCKET))

# (battery is built after the frame points below, so it can follow them exactly)
# motor: drum, black cover and hub on the right
cylinder_y(MOTOR[0], MOTOR[1], MOTOR_R * 1.05, -0.080, 0.050, "black")
cylinder_y(MOTOR[0], MOTOR[1], MOTOR_R, -0.094, -0.080, "black", "cover")
cylinder_y(MOTOR[0], MOTOR[1], MOTOR_R * 0.80, -0.096, -0.094, "ring", "cover")
cylinder_y(MOTOR[0], MOTOR[1], MOTOR_R * 0.30, -0.101, -0.096, "ring", "ring", seg=10)

# frame (same both sides): rear upright from under the seat, down behind the pivot to the footpeg mount; main spar from
# the upright forward over the motor, bending up at the front to the steering head; top rail under the tank.
U = [R(42, 105), R(30, 140), R(20, 180), R(14, 215), R(12, 250), (0.012, 0.292)]   # last: down to the peg mount
SP = [R(20.5, 178), R(100, 172), R(155, 165), R(172, 152), R(185, 125)]           # starts on the upright
# HEAD_TOP: the top rail carries straight on (same line) until it meets the steering head tube's axis (head tube runs
# (0.2755, 0.724) -> (0.2215, 0.834), radius 0.026), so the top front joins the head (Boss)
HEAD_LOW, TOP_REAR, HEAD_TOP = (0.272, 0.725), (-0.040, 0.700), (0.2508, 0.7742)
SUB_LOWER = (-0.076, 0.502)             # front end of the lower subframe rail
FH = 14 * S; FY0, FY1 = 0.064, 0.090
# rear upright (Boss, marked): from the top-rail junction under the seat straight down behind the battery to just
# above the swingarm/chain, then forward and down to the footpeg bracket. The lower leg runs outboard of the swingarm
# arms and the chain (y 0.098..0.120); the bend joint ties the two together. The spar now starts on this upright.
G_BEND = (-0.062, 0.405); G_FOOT = (0.015, 0.296)
G_SP = (TOP_REAR[0] + (G_BEND[0] - TOP_REAR[0]) * (TOP_REAR[1] - SP[0][1]) / (TOP_REAR[1] - G_BEND[1]), SP[0][1])
GY0, GY1 = 0.062, 0.088   # lower leg in the upright's own plane (not outboard, clear of the chain at 0.0895+)
def on_upright(z): return (TOP_REAR[0] + (G_BEND[0] - TOP_REAR[0]) * (TOP_REAR[1] - z) / (TOP_REAR[1] - G_BEND[1]), z)
G_SUB = on_upright(SUB_LOWER[1])        # the lower subframe rail now ends on the upright (was out to the old one)
# spar runs straight from the upright to SP[1] (no joint knob at SP[0] - Boss); the subframe's lower rail carries on
# along its own line, at its own width, into the upright (no knob at its end - Boss)
# the last spar segment and the top rail are built separately below: they converge onto the head tube (no knobs)
paths = [[G_SP] + SP[1:], [TOP_REAR, G_BEND]]
SUB_END, SUB_DIR = Vector((-0.0805, 0.508)), Vector((0.2585, -0.211)).normalized()
_sub = SUB_END
for _ in range(40):                                               # walk along the rail line to the upright's centre
    if _sub.x >= on_upright(_sub.y)[0] + 0.010: break         # on through the upright, so the end is hidden in it
    _sub = _sub + SUB_DIR * 0.001
G_SUB = (_sub.x, _sub.y)
for s_ in (-1, 1):
    y0, y1 = sorted((s_ * FY0, s_ * FY1))
    for path in paths:
        for p0, p1 in zip(path, path[1:]):
            bar(p0, p1, FH, y0, y1, "rail")
        for p in path[1:-1]:
            cylinder_y(p[0], p[1], FH * 0.52, y0, y1, "rail", seg=10)
    ya, yb = sorted((s_ * 0.032, s_ * (FY1 + 0.002)))      # joints reach in to the subframe rails (y 0.034..0.067)
    for p in (TOP_REAR, G_SP):
        cylinder_y(p[0], p[1], FH * 0.62, ya, yb, "rail", seg=12)
    sa, sb = sorted((s_ * 0.038, s_ * 0.070))                  # overlaps the upright (y 0.064..0.090)
    bar((SUB_END.x - SUB_DIR.x * 0.006, SUB_END.y - SUB_DIR.y * 0.006), G_SUB, 0.015, sa, sb, "rail")
    # front ends: spar SP[4] -> HEAD_LOW and the top rail converge from y 0.064..0.090 onto the head tube (y 0..0.026)
    hy = tuple(sorted((0.0, s_ * 0.026)))
    # top rail: full width, then only the last 45 mm turns in to the head tube
    L_ = math.hypot(HEAD_TOP[0] - TOP_REAR[0], HEAD_TOP[1] - TOP_REAR[1]); t_ = 1 - 0.045 / L_
    q_ = (TOP_REAR[0] + (HEAD_TOP[0] - TOP_REAR[0]) * t_, TOP_REAR[1] + (HEAD_TOP[1] - TOP_REAR[1]) * t_)
    bar(TOP_REAR, q_, FH, y0, y1, "rail"); taper_bar(q_, HEAD_TOP, FH, (y0, y1), hy, "rail")
    # front down tube (Boss, marked): carries on up its own line past the head tube's bottom, full width to z 0.745,
    # then turns in and finishes inside the head tube at z 0.800 - so it visibly runs up to the head
    def on_down(z): return (SP[-1][0] + (HEAD_LOW[0] - SP[-1][0]) * (z - SP[-1][1]) / (HEAD_LOW[1] - SP[-1][1]), z)
    # tuck=<z>: the tube turns in lower down so the fork legs clear it when the bars turn (Boss: tuck it in)
    TZ = float(OPT.get("tuck", 0.745))
    bar(SP[-1], on_down(TZ), FH, y0, y1, "rail"); taper_bar(on_down(TZ), on_down(0.800), FH, (y0, y1), hy, "rail")
    # solid elbow where the spar turns up into the down tube (no round knob, no gap): fills between both tube ends
    d1 = Vector((SP[-1][0] - SP[-2][0], SP[-1][1] - SP[-2][1])).normalized(); d2 = Vector((HEAD_LOW[0] - SP[-1][0], HEAD_LOW[1] - SP[-1][1])).normalized()
    n1, n2 = Vector((-d1.y, d1.x)) * FH / 2, Vector((-d2.y, d2.x)) * FH / 2; P_ = Vector(SP[-1])
    prism([tuple(P_ + n1), tuple(P_ + n2), tuple(P_ - n1), tuple(P_ - n2)], y0, y1, "rail")
    # lower leg in line with the upright down to the peg bracket - not outside / in the footpeg (Boss)
    ga, gb = sorted((s_ * GY0, s_ * FY1))                      # upright bend
    cylinder_y(G_BEND[0], G_BEND[1], FH * 0.62, ga, gb, "rail", seg=12)
    la, lb = sorted((s_ * GY0, s_ * GY1))
    bar(G_BEND, G_FOOT, FH, la, lb, "rail")
    fa, fb = sorted((s_ * GY0, s_ * GY1))                      # its foot, on the peg bracket
    box(-0.004, 0.034, fa, fb, 0.284, 0.308, "rail")
    pa, pb = sorted((s_ * FY0, s_ * 0.100))                    # footpeg bracket: upright foot to the peg (y 0.083 on)
    box(-0.010, 0.044, pa, pb, 0.284, 0.308, "rail")
# battery: fills the whole bay the frame encloses - bottom on the spar, rear on the upright, front on the front tube,
# top under the top rail - so no gaps show between battery, frame and plastics
BY = 0.074
# rear edge extended back to the new upright (Boss): front and top unchanged, the back now runs down the upright to
# where the spar meets it
BAT = [G_SP, SP[0], SP[1], SP[2], SP[3], SP[4], HEAD_LOW, (0.200, 0.745), (-0.035, 0.690), on_upright(0.690)]
prism(BAT, -BY, BY, "black")
# fins: same pitch, from the old bottom fin all the way up to the battery top (Boss), each from the upright (or the
# sloping top edge) to the front edge
def bat_front(z):
    if z <= SP[4][1]: return R(160, 0)[0]
    return min(R(160, 0)[0], SP[4][0] + (HEAD_LOW[0] - SP[4][0]) * (z - SP[4][1]) / (HEAD_LOW[1] - SP[4][1]) - 0.012)
def bat_rear(z):
    x = on_upright(z)[0] + 0.014
    if z > 0.690: x = max(x, -0.035 + (0.200 + 0.035) * (z - 0.690) / (0.745 - 0.690) + 0.012)
    return x
n_fin = 0
for k in range(20):
    z = R(0, 162 - k * 10)[1]
    if z + 0.0045 > 0.740: break
    x0, x1 = bat_rear(z + 0.0045), bat_front(z + 0.0045)
    if x1 - x0 < 0.03: break
    for s_ in (-1, 1):
        y0, y1 = sorted((s_ * BY, s_ * (BY + 0.008)))
        box(x0, x1, y0, y1, z - 0.0045, z + 0.0045, "fin"); n_fin += 1
print("ELECTRIC fins", n_fin // 2, "per side")
cylinder_y(PIVOT[0], PIVOT[1], 10 * S, -0.098, 0.098, "rail", seg=12)          # swingarm pivot boss
cylinder_y(PIVOT[0], PIVOT[1], 5 * S, -0.104, 0.104, "ring", "ring", seg=8)    # pivot bolt

# STOMP plate: front face from under the head down to the bottom front (STOMP on it), round under the motor back to
# the peg mounts, up behind the motor to the spar (right photo outline)
CY = 0.088
COVER = [SP[4], R(150, 232), R(135, 250), R(112, 266), R(30, 268), U[5], U[4], U[3], SP[0], SP[1], SP[2], SP[3]]
D0, D1 = COVER[0], COVER[1]
def stomp_uv(i, pts):
    p = ccw(COVER); a_, b_ = p[i], p[(i + 1) % len(p)]
    if {a_, b_} != {D0, D1}: return None
    u0, v0, u1, v1 = DECAL_UV
    # turned 180 degrees from bottom-to-top, so STOMP reads from the top down (Boss)
    return [(u0 + (1 - (z - D1[1]) / (D0[1] - D1[1])) * (u1 - u0), v0 + ((y + CY) / (CY + CIN)) * (v1 - v0)) for x, y, z in pts]
# right outline from the right side to y 0.040 (its left face is the inner face seen through the open part of the
# left side), left outline (left photo: front band with the lights + bottom strip, open over the motor) from 0.040 out
CIN = 0.040
prism(COVER, -CY, CIN, "plate", edge_uv=stomp_uv)
LCOVER = [SP[4], SP[3], SP[2], SP[1], L(78, 150), L(100, 176), (0.020, 0.317), U[5], R(30, 268), R(112, 266), R(135, 250), R(150, 232)]
prism(LCOVER, CIN, CY, "plate")
for q in ((58, 156), (68, 168), (78, 180)):                          # left slots along the front band
    a_, b_ = L(q[0] + 6, q[1] + 3), L(q[0] - 6, q[1] - 3); box(a_[0], b_[0], CY, CY + 0.002, a_[1], b_[1], "slot")
for s_ in (-1,):                                                     # right slots along the front edge and the bottom
    y0, y1 = sorted((s_ * CY, s_ * (CY + 0.002)))
    for px_ in (45, 65, 85, 105):
        a_, b_ = R(px_, 262), R(px_ + 10, 258); box(a_[0], b_[0], y0, y1, a_[1], b_[1], "slot")
    for py_ in (195, 212, 229):
        a_, b_ = R(140 - (py_ - 195) * 0.5, py_ + 4), R(155 - (py_ - 195) * 0.5, py_); box(a_[0], b_[0], y0, y1, a_[1], b_[1], "slot")
# left side (left photo): controller box with two red lights, sprocket on its own shaft
c0, c1 = L(112, 160), L(88, 133); box(c0[0], c1[0], CIN, CIN + 0.030, c0[1], c1[1], "fin")      # controller on the motor face
for q in (L(55, 130), L(70, 145)):
    box(q[0] - 0.005, q[0] + 0.005, CY, CY + 0.004, q[1] - 0.006, q[1] + 0.006, "red")
cylinder_y(SPROCKET[0], SPROCKET[1], 0.014, 0.030, 0.064, "black", seg=10)

# chain: on both sprockets (moved out to y 0.089..0.098), clear of the swingarm arm. Front wrap moves with the chassis,
# rear wrap with the swingarm, the runs blend between them.
F, Rr = Vector(SPROCKET), Vector((-0.544, 0.248)); RF, RR = SPROCKET_R + 0.004, 0.088
CY0, CY1 = 0.0515, 0.0585
uvec = (F - Rr).normalized(); dist = (F - Rr).length; phi = math.acos((RR - RF) / dist)
def rot(v, a_): return Vector((v.x * math.cos(a_) - v.y * math.sin(a_), v.x * math.sin(a_) + v.y * math.cos(a_)))
n_top, n_bot = rot(uvec, phi), rot(uvec, -phi)
if n_top.y < n_bot.y: n_top, n_bot = n_bot, n_top
def arc(c, r, a0, a1, through, steps):
    a0, a1 = math.atan2(a0.y, a0.x), math.atan2(a1.y, a1.x); t = math.atan2(through.y, through.x)
    def norm(a_): return (a_ - a0) % (2 * math.pi)
    span = norm(a1)
    if norm(t) > span: span = span - 2 * math.pi
    return [c + Vector((math.cos(a0 + span * k / steps), math.sin(a0 + span * k / steps))) * r for k in range(steps + 1)]
loop = arc(F, RF, n_top, n_bot, uvec, 8) + arc(Rr, RR, n_bot, n_top, -uvec, 14)
def chain_w(p):
    t = max(0.0, min(1.0, (Vector((p[0], p[2])) - Rr).dot(uvec) / dist))
    t = 1.0 if t > 0.92 else 0.0 if t < 0.12 else (t - 0.12) / 0.8
    return {"chassis": t, "swingarm": 1 - t}
WEIGHT = chain_w
for i in range(len(loop)):
    a_, b_ = loop[i], loop[(i + 1) % len(loop)]
    if (b_ - a_).length < 1e-4: continue
    bar((a_.x, a_.y), (b_.x, b_.y), 0.010, CY0, CY1, "chain")
WEIGHT = lambda p: {"rear_wheel": 1.0}
WEIGHT = None

# rear shock: forward of the old one, just behind the frame upright (left photo: spring behind the upright, above the
# swingarm). Top eye up in the box under the seat (chassis), bottom eye on a cross tube between the swingarm arms.
def tube(p0, p1, r, colour, r_in=None, seg=12):
    """Round tube between two points in the y = 0 plane; r_in makes it a ring (open in the middle)."""
    p0, p1 = Vector(p0), Vector(p1); ax = (p1 - p0).normalized()
    n1 = Vector((-ax.z, 0, ax.x)); n2 = Vector((0, 1, 0))
    def circ(c, rr): return [tuple(c + (n1 * math.cos(2 * math.pi * k / seg) + n2 * math.sin(2 * math.pi * k / seg)) * rr) for k in range(seg)]
    A, B = circ(p0, r), circ(p1, r)
    for k in range(seg):
        face([A[k], A[(k + 1) % seg], B[(k + 1) % seg], B[k]], colour)
    if r_in is None:
        face(list(reversed(A)), colour); face(B, colour)
    else:
        Ai, Bi = circ(p0, r_in), circ(p1, r_in)
        for k in range(seg):
            face([Ai[(k + 1) % seg], Ai[k], Bi[k], Bi[(k + 1) % seg]], colour)
            face([A[(k + 1) % seg], A[k], Ai[k], Ai[(k + 1) % seg]], colour)
            face([B[k], B[(k + 1) % seg], Bi[(k + 1) % seg], Bi[k]], colour)
# shock moved back to Boss's mark (19 Nov 16:01): top eye inside the box under the seat (x -0.279..-0.059,
# z 0.567..0.686), bottom eye 15 mm above the swingarm centreline
# tilted forward along Boss's blue line (19 Nov 16:34): bottom eye on the swingarm, top eye forward inside the seat box
# bottom (Boss 16:58 "block showing out the top of the swingarm, shock hanging half out the block"): Harvey's mount
# block is gone. A cross bar sits between the two arms inside their height, two clevis tabs stand on it, and the
# shock's bottom eye is bolted between the tabs just under the arms' top edge.
# n = height across the arm from the pivot-axle line; the arm itself spans n -0.0285 .. +0.0185 here (measured)
_ARM_U = (REAR_AXLE - NEW_PIVOT).normalized(); _ARM_N = Vector((-_ARM_U.y, _ARM_U.x))
if _ARM_N.y < 0: _ARM_N = -_ARM_N
_bx = -0.192; _on_arm = NEW_PIVOT + _ARM_U * ((_bx - NEW_PIVOT.x) / _ARM_U.x)
def _arm(u, n): q = _on_arm + _ARM_U * u + _ARM_N * n; return (q.x, q.y)
EYE_N = 0.016
_eye = _on_arm + _ARM_N * EYE_N
SH_B, SH_T = Vector((_eye.x, 0, _eye.y)), Vector((-0.100, 0, 0.598))
print("ELECTRIC shock eyes", tuple(round(c, 4) for c in SH_B), tuple(round(c, 4) for c in SH_T))
SH_AX = SH_T - SH_B
def sh(t): return tuple(SH_B + SH_AX * t)
def shock_w(p):
    t = (Vector(p) - SH_B).dot(SH_AX) / SH_AX.length_squared
    c = max(0.0, min(1.0, (t - 0.18) / 0.55))                 # spring stretches between the seats
    return {"chassis": c, "swingarm": 1 - c}
WEIGHT = lambda p: {"swingarm": 1.0}
prism([_arm(-0.016, -0.022), _arm(0.016, -0.022), _arm(0.016, 0.006), _arm(-0.016, 0.006)], -0.060, 0.060, "black")   # cross bar, ends in the arms
_tab = [_arm(-0.012, 0.0), _arm(0.012, 0.0)] + [_arm(0.012 * math.cos(math.pi * k / 6), EYE_N + 0.012 * math.sin(math.pi * k / 6)) for k in range(0, 7)]
for _y0, _y1 in ((-0.0295, -0.0235), (0.0235, 0.0295)):
    prism(_tab, _y0, _y1, "black")                                                 # clevis tabs, round tops round the eye
cylinder_y(SH_B.x, SH_B.z, 0.005, -0.033, 0.033, "ring", "ring", seg=8)          # bolt through tabs and eye
cylinder_y(SH_B.x, SH_B.z, 0.0105, -0.0225, 0.0225, "black", seg=12)              # the shock's bottom eye round the bolt
# Harvey's shock moved onto Louie's eyes: along its axis it is scaled to fit, across it keeps Harvey's size; black (Boss)
_ha = Vector((H_T.x - H_B.x, H_T.z - H_B.z)); _hl = _ha.length; _ha /= _hl; _hn = Vector((-_ha.y, _ha.x))
_na = Vector((SH_AX.x, SH_AX.z)); _nl = _na.length; _na /= _nl; _nn = Vector((-_na.y, _na.x))
def _to_louie(q):
    d = Vector((q[0] - H_B.x, q[2] - H_B.z)); a, n = d.dot(_ha) * _nl / _hl, d.dot(_hn)
    return (SH_B.x + _na.x * a + _nn.x * n, q[1], SH_B.z + _na.y * a + _nn.y * n)
WEIGHT = shock_w
for colour, pts in HARVEY_SHOCK:
    if colour == "mount": continue
    face([_to_louie(q) for q in pts], colour)
WEIGHT = None
# mud flap behind the shock: hangs from under the seat box, curving back towards the rear tyre
FLAP = [(-0.255, 0.567), (-0.272, 0.490), (-0.290, 0.415)]   # hangs from the underside of the box, flush (Boss's red mark)
for p0, p1 in zip(FLAP, FLAP[1:]):
    bar(p0, p1, 0.005, -0.050, 0.050, "black")
cylinder_y(FLAP[1][0], FLAP[1][1], 0.0025, -0.050, 0.050, "black", seg=6)
bar(FLAP[-1], (FLAP[-1][0] - 0.004, FLAP[-1][1] - 0.010), 0.008, -0.052, 0.052, "black")   # stiff bottom edge

# cap the open back end of the kept tank front (x 0.135): outline from its own vertices there, per side of y = 0
_pi2 = slot_names.index("Plastics_UV"); _cap = {}; _cuv = None
for f in bm.faces:
    if f.material_index != _pi2 or f in dead or not f.is_valid: continue
    W_ = [MW @ v.co for v in f.verts]
    if not all(abs(w.y) <= 0.1145 and 0.13 < w.x < 0.26 and 0.62 < w.z < 0.79 for w in W_): continue   # tank only
    for l in f.loops:
        w = MW @ l.vert.co
        if abs(w.x - 0.135) < 0.002:
            _cap[(round(w.y, 3), round(w.z, 3))] = w; _cuv = _cuv or l[uvl].uv.copy()
if _cap:
    pts = list(_cap.values()); cy_ = sum(p.y for p in pts) / len(pts); cz_ = sum(p.z for p in pts) / len(pts)
    pts.sort(key=lambda p: math.atan2(p.z - cz_, p.y - cy_))
    f = face([(0.135, p.y, p.z) for p in pts], "black"); f.material_index = _pi2
    for l in f.loops: l[uvl].uv = _cuv
print("ELECTRIC tank cap points", len(_cap))
# kept tank front + its cap take the shroud's own atlas spot (same colour as the shroud it sits against - Boss)
def _shroud_uv(sy):
    best = None
    for f in bm.faces:
        if f.material_index != _pi2: continue
        W_ = [MW @ v.co for v in f.verts]
        if not all(abs(w.y) > 0.113 and w.y * sy > 0 for w in W_): continue
        c = sum(W_, Vector()) / len(W_); d = (c - Vector((0.15, sy * 0.14, 0.65))).length
        if best is None or d < best[0]: best = (d, f)
    us = [l[uvl].uv for l in best[1].loops]
    return Vector((sum(u.x for u in us) / len(us), sum(u.y for u in us) / len(us)))
_suv = _shroud_uv(1); n_tf = 0
for f in bm.faces:
    if f.material_index != _pi2: continue
    W_ = [MW @ v.co for v in f.verts]
    if not all(abs(w.y) <= 0.1145 and 0.13 < w.x < 0.26 and 0.62 < w.z < 0.79 for w in W_): continue
    n = len(f.loops); n_tf += 1
    for i, l in enumerate(f.loops):
        l[uvl].uv = (_suv.x + 0.0005 * math.cos(2 * math.pi * i / n), _suv.y + 0.0005 * math.sin(2 * math.pi * i / n))
print("ELECTRIC tank front faces on shroud colour", n_tf, tuple(round(c, 4) for c in _suv))

bm.to_mesh(me); bm.free()

# ---------------------------------------------------------------- 3. drop the now empty material slots (remap indices)
used = {p.material_index for p in me.polygons}
keep = [i for i in range(len(ob.material_slots)) if i in used]
remap = {old: new for new, old in enumerate(keep)}
mi = np.zeros(len(me.polygons), np.int32); me.polygons.foreach_get("material_index", mi)
mi = np.array([remap[i] for i in mi], np.int32)
mats = [me.materials[i] for i in keep]
me.materials.clear()
for m in mats: me.materials.append(m)
me.polygons.foreach_set("material_index", mi); me.update()

# ---------------------------------------------------------------- 4. atlas: colour patches, STOMP decal, frame black
src = bpy.data.images.load(ATLAS_IN); W, H = src.size
px = np.array(src.pixels[:], np.float32).reshape(H, W, 4)
for k, (cu, cv) in PATCH_UV.items():
    x0, x1 = int((cu - 0.01) * W), int((cu + 0.01) * W); y0, y1 = int((cv - 0.01) * H), int((cv + 0.01) * H)
    px[y0:y1, x0:x1, :3] = hx(PATCH[k]); px[y0:y1, x0:x1, 3] = 1
dec = bpy.data.images.load(DECAL); dw, dh = dec.size
dp = np.array(dec.pixels[:], np.float32).reshape(dh, dw, 4)
u0, v0, u1, v1 = DECAL_UV; X0, X1, Y0, Y1 = int(u0 * W), int(u1 * W), int(v0 * H), int(v1 * H)
yy = (np.arange(Y1 - Y0) * dh / (Y1 - Y0)).astype(int); xx = (np.arange(X1 - X0) * dw / (X1 - X0)).astype(int)
px[Y0:Y1, X0:X1] = dp[yy][:, xx]; px[Y0:Y1, X0:X1, 3] = 1
uv = me.uv_layers[0].data; _sn = [s.name for s in ob.material_slots]
# frame -> rail colour; all plastics (fenders, shrouds, side panels, plates, fork guards) and the swingarm -> black (Boss)
PAINT = {"Frame_UV": "rail", "Plastics_UV": "plastic", "Plate_UV": "plastic", "DR_ForkGuard_UV": "plastic",
         "DR_Swingarm_UV": "swingarm", "Trim_eaebeb_UV": "plastic_hi"}   # eaebeb = the shroud inset panels
PAINT_MI = {_sn.index(k): hx(PATCH[c]) for k, c in PAINT.items() if k in _sn}
print("ELECTRIC painted materials", [k for k in PAINT if k in _sn])
# triple clamps (Trim_b1aeaf faces around the fork tubes between the bottom and top clamp) -> black (Boss)
_ca, _cb = Vector((0.43, 0.49)), Vector((0.235, 0.895)); _cd = (_cb - _ca); _cl = _cd.length; _cd = _cd / _cl
_bi = _sn.index("Trim_b1aeaf_UV"); CLAMP = set()
for p in me.polygons:
    if p.material_index != _bi: continue
    c = MW @ p.center; q = Vector((c.x, c.z)) - _ca
    if abs(q.dot(Vector((-_cd.y, _cd.x)))) < 0.05 and 0.55 < q.dot(_cd) / _cl < 1.05 and abs(c.y) > 0.02: CLAMP.add(p.index)
print("ELECTRIC triple clamp faces", len(CLAMP)); assert 30 <= len(CLAMP) <= 50, len(CLAMP)
for p in me.polygons:
    if p.index in CLAMP: col = hx(PATCH["black"])
    elif p.material_index in PAINT_MI: col = PAINT_MI[p.material_index]
    else: continue
    q = np.array([uv[l].uv for l in p.loop_indices]) * [W, H]
    c = q.mean(0); q = q + np.sign(q - c) * 3                 # grow 3 px, like the island padding
    for k in range(1, len(q) - 1):
        A, B, C = q[0], q[k], q[k + 1]
        x0, y0 = np.maximum(np.floor(np.min([A, B, C], 0)).astype(int), 0); x1, y1 = np.minimum(np.ceil(np.max([A, B, C], 0)).astype(int) + 1, [W, H])
        xs, ys = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
        d = (B[1] - C[1]) * (A[0] - C[0]) + (C[0] - B[0]) * (A[1] - C[1])
        if abs(d) < 1e-9: continue
        l1 = ((B[1] - C[1]) * (xs - C[0]) + (C[0] - B[0]) * (ys - C[1])) / d
        l2 = ((C[1] - A[1]) * (xs - C[0]) + (A[0] - C[0]) * (ys - C[1])) / d
        ins = (l1 >= 0) & (l2 >= 0) & (1 - l1 - l2 >= 0)
        sub = px[y0:y1, x0:x1]; sub[ins, :3] = col
# fork tubes: copper with red top caps (Boss's photo): each tube face is painted per pixel by its position along the
# fork axis, so the top of the tube (above the top clamp) and its end cap come out red
FORK_A, FORK_B, T_RED = Vector((0.43, 0.49)), Vector((0.235, 0.895)), 0.885
_fd = FORK_B - FORK_A; _fl2 = _fd.length_squared
_fgi = _sn.index("Trim_fork_gold_UV"); COPPER, FRED = np.array(hx("a8735a")), np.array(hx("c42a34")); n_fork = 0
for p in me.polygons:
    if p.material_index != _fgi: continue
    n_fork += 1
    tv = np.array([(Vector(((MW @ me.vertices[v].co).x, (MW @ me.vertices[v].co).z)) - FORK_A).dot(_fd) / _fl2 for v in p.vertices])
    q = np.array([uv[l].uv for l in p.loop_indices]) * [W, H]
    c = q.mean(0); q = q + np.sign(q - c) * 3
    for k in range(1, len(q) - 1):
        A, B, C = q[0], q[k], q[k + 1]; tA, tB, tC = tv[0], tv[k], tv[k + 1]
        x0, y0 = np.maximum(np.floor(np.min([A, B, C], 0)).astype(int), 0); x1, y1 = np.minimum(np.ceil(np.max([A, B, C], 0)).astype(int) + 1, [W, H])
        xs, ys = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
        d = (B[1] - C[1]) * (A[0] - C[0]) + (C[0] - B[0]) * (A[1] - C[1])
        if abs(d) < 1e-9: continue
        l1 = ((B[1] - C[1]) * (xs - C[0]) + (C[0] - B[0]) * (ys - C[1])) / d
        l2 = ((C[1] - A[1]) * (xs - C[0]) + (A[0] - C[0]) * (ys - C[1])) / d
        l3 = 1 - l1 - l2; ins = (l1 >= -0.05) & (l2 >= -0.05) & (l3 >= -0.05)
        tp = l1 * tA + l2 * tB + l3 * tC
        sub = px[y0:y1, x0:x1]; red = ins & (tp > T_RED)
        sub[ins & ~red, :3] = COPPER; sub[red, :3] = FRED
print("ELECTRIC fork faces painted", n_fork)
# white race number 11 on the three number plates (same Inter Black "11" as the rider's back - Boss): the outer
# faces of each plate are planar-projected onto their own black patch in the free strip, the number stamped in it
NUM_MASK = os.path.join(os.path.dirname(DECAL), "plate11_mask.png")
_nm = bpy.data.images.load(NUM_MASK); nw, nh = _nm.size
nmask = np.array(_nm.pixels[:], np.float32).reshape(nh, nw, 4)[:, :, 0]
_pli = _sn.index("Plate_UV"); MW3 = MW.to_3x3()
UPF = Vector((-0.44, 0, 0.90)).normalized()
PLATES = [  # name, face test, (s axis, t axis) in world space as seen from outside (s = viewer's right), uv u0
    ("left", lambda c, n: n.y > 0.9 and c.y > 0 and c.x < 0, (Vector((-1, 0, 0)), Vector((0, 0, 1))), 0.52),
    ("right", lambda c, n: n.y < -0.9 and c.y < 0 and c.x < 0, (Vector((1, 0, 0)), Vector((0, 0, 1))), 0.60),
    ("front", lambda c, n: n.x > 0.85, (Vector((0, 1, 0)), UPF), 0.69)]
# side plates: number just under the plate's top edge and close to its front edge (Boss's marks); world box x, z
SIDE_C, SIDE_SCL = (-0.2665, 0.6645), 0.077 / 291   # centre (x, z) and metres per mask pixel; slanted 22 deg with the
# plate's front edge and tilted 9.6 deg so the number's top runs parallel to the plate's top edge (Boss's marks)
def _load_mask(fn):
    im_ = bpy.data.images.load(os.path.join(os.path.dirname(DECAL), fn)); w_, h_ = im_.size
    a_ = np.array(im_.pixels[:], np.float32).reshape(h_, w_, 4)[:, :, 0]; bpy.data.images.remove(im_); return a_
SIDE_MASK = {"right": _load_mask("plate11_mask_side_r.png"), "left": _load_mask("plate11_mask_side_l.png")}
RV0, RH = 0.957, 0.040                       # patch bottom and height (uv)
# GizMX Racing logo under the number on the front plate (Boss, 19 Nov 2026); sizes as parts of the plate's height
LOGO_PATH = os.path.join(os.path.dirname(DECAL), "GizMX_Racing.png")
NUM_FRONT_H, NUM_FRONT_UP = float(OPT.get("numh", 0.44)), float(OPT.get("numup", 0.10))
LOGO_H, LOGO_GAP = float(OPT.get("logoh", 0.22)), float(OPT.get("logogap", 0.03))
LOGO = None
if os.path.exists(LOGO_PATH) and OPT.get("logo", "1") != "0":
    _lg = bpy.data.images.load(LOGO_PATH); _lg.colorspace_settings.name = "sRGB"; _w, _h = _lg.size
    LOGO = np.array(_lg.pixels[:], np.float32).reshape(_h, _w, 4); bpy.data.images.remove(_lg)
for name, test, (sa, ta), u0 in PLATES:
    fs = [p for p in me.polygons if p.material_index == _pli and test(MW @ p.center, (MW3 @ p.normal).normalized())]
    pts = {p.index: [(MW @ me.vertices[v].co) for v in p.vertices] for p in fs}
    S = [q.dot(sa) for v in pts.values() for q in v]; T = [q.dot(ta) for v in pts.values() for q in v]
    s0, s1, t0, t1 = min(S), max(S), min(T), max(T)
    aw = sum(p.area for p in fs); cs = sum((p.center @ MW.transposed() if False else (MW @ p.center)).dot(sa) * p.area for p in fs) / aw
    ct = sum((MW @ p.center).dot(ta) * p.area for p in fs) / aw
    rw = RH * (s1 - s0) / (t1 - t0)
    print("ELECTRIC plate", name, "faces", len(fs), "size %.3f x %.3f" % (s1 - s0, t1 - t0), "uv", round(u0, 3), round(u0 + rw, 3))
    assert len(fs) >= 4 and u0 + rw < {"left": 0.60, "right": 0.69, "front": 0.99}[name], (name, len(fs), rw)
    for p in fs:
        for li, v in zip(p.loop_indices, p.vertices):
            q = MW @ me.vertices[v].co
            uv[li].uv = (u0 + (q.dot(sa) - s0) / (s1 - s0) * rw, RV0 + (q.dot(ta) - t0) / (t1 - t0) * RH)
    X0, X1, Y0, Y1 = int(u0 * W), int((u0 + rw) * W), int(RV0 * H), int((RV0 + RH) * H)
    px[Y0 - 2:Y1 + 2, X0 - 2:X1 + 2, :3] = hx(PATCH["plastic"]); px[Y0 - 2:Y1 + 2, X0 - 2:X1 + 2, 3] = 1
    nh_px = 0.50 * (Y1 - Y0); nw_px = nh_px * nw / nh                         # number: half the plate height
    cx = X0 + (cs - s0) / (s1 - s0) * (X1 - X0); cy = Y0 + (ct - t0) / (t1 - t0) * (Y1 - Y0)
    if name == "front" and LOGO is not None:            # number moved up a little to make room for the logo under it
        nh_px = NUM_FRONT_H * (Y1 - Y0); nw_px = nh_px * nw / nh; cy += NUM_FRONT_UP * (Y1 - Y0)
    bx0, by0 = int(cx - nw_px / 2), int(cy - nh_px / 2); bw, bh = int(nw_px), int(nh_px)
    if name != "front":
        _mh, _mw = SIDE_MASK[name].shape
        x0n, x1n = SIDE_C[0] - _mw * SIDE_SCL / 2, SIDE_C[0] + _mw * SIDE_SCL / 2
        z0n, z1n = SIDE_C[1] - _mh * SIDE_SCL / 2, SIDE_C[1] + _mh * SIDE_SCL / 2
        ss = [Vector((x_, 0, 0)).dot(sa) for x_ in (x0n, x1n)]
        bx0 = int(X0 + (min(ss) - s0) / (s1 - s0) * (X1 - X0)); bw = int((x1n - x0n) / (s1 - s0) * (X1 - X0))
        by0 = int(Y0 + (z0n - t0) / (t1 - t0) * (Y1 - Y0)); bh = int((z1n - z0n) / (t1 - t0) * (Y1 - Y0))
    mk = SIDE_MASK.get(name, nmask); mh, mw = mk.shape
    # anti-aliased: each texel is the average of 6 x 6 samples of the mask (it was one sample, which left the number's
    # edges stepped - Boss)
    SS = 6
    yy = ((np.arange(bh * SS) + 0.5) * mh / (bh * SS)).astype(int).clip(0, mh - 1)
    xx = ((np.arange(bw * SS) + 0.5) * mw / (bw * SS)).astype(int).clip(0, mw - 1)
    m = mk[yy][:, xx].reshape(bh, SS, bw, SS).mean(axis=(1, 3))[:, :, None]
    sub = px[by0:by0 + bh, bx0:bx0 + bw, :3]; px[by0:by0 + bh, bx0:bx0 + bw, :3] = sub * (1 - m) + m * 1.0
    if name == "front" and LOGO is not None:
        # GizMX Racing logo (Boss's own artwork) centred under the number, same anti-aliasing as the number
        lmh, lmw = LOGO.shape[:2]
        lh = int(LOGO_H * (Y1 - Y0)); lw = int(lh * lmw / lmh)
        lx0 = int(cx - lw / 2); ly0 = int(by0 - LOGO_GAP * (Y1 - Y0)) - lh
        yy = ((np.arange(lh * SS) + 0.5) * lmh / (lh * SS)).astype(int).clip(0, lmh - 1)
        xx = ((np.arange(lw * SS) + 0.5) * lmw / (lw * SS)).astype(int).clip(0, lmw - 1)
        smp = LOGO[yy][:, xx]; a_ = smp[:, :, 3:4]
        rgb = (smp[:, :, :3] * a_).reshape(lh, SS, lw, SS, 3).mean(axis=(1, 3)); al = a_.reshape(lh, SS, lw, SS, 1).mean(axis=(1, 3))
        sub = px[ly0:ly0 + lh, lx0:lx0 + lw, :3]; px[ly0:ly0 + lh, lx0:lx0 + lw, :3] = sub * (1 - al) + rgb
        print("ELECTRIC front plate logo", lw, "x", lh, "px at", lx0, ly0, "plate", X0, Y0, X1, Y1, "number", bx0, by0, bw, bh)
out = bpy.data.images.new(os.path.splitext(os.path.basename(ATLAS_OUT))[0], W, H, alpha=True)
out.pixels.foreach_set(px.ravel()); out.filepath_raw = ATLAS_OUT; out.file_format = "PNG"; out.save(); out.pack()
old = set()
for s in ob.material_slots:
    for n in s.material.node_tree.nodes:
        if n.type == 'TEX_IMAGE':
            old.add(n.image); n.image = out
for im in [src, dec, _nm] + list(old):
    if im and im != out and im.name in bpy.data.images and im.users == 0: bpy.data.images.remove(im)
print("ELECTRIC removed %d faces, added %d faces, slots %d, verts %d" % (n_removed, len(added), len(ob.material_slots), len(me.vertices)))
# swingarm bone: head to the new pivot (tail stays on the rear axle)
rig = bpy.data.objects["bike_rig"]
for o in bpy.context.view_layer.objects: o.select_set(False)
bpy.context.view_layer.objects.active = rig; rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb = rig.data.edit_bones["swingarm"]
eb.head = rig.matrix_world.inverted() @ Vector((NEW_PIVOT.x, 0, NEW_PIVOT.y))
bpy.ops.object.mode_set(mode='OBJECT')
print("ELECTRIC swingarm bone", tuple(round(c, 4) for c in rig.matrix_world @ rig.data.bones["swingarm"].head_local))
# steering clearance (Boss: tuck it in behind the forks): near the steering head the new electric parts are narrowed
# towards the middle so the fork legs clear them with the bars turned 28 deg (the Whip). Side view unchanged.
if OPT.get("clear", "1") != "0":
    _ob = bpy.data.objects["race_bike_youth"]; _MW = _ob.matrix_world; _IM = _MW.inverted()
    _sb = rig.data.bones["steer"]; _A = rig.matrix_world @ _sb.head_local; _T = rig.matrix_world @ _sb.tail_local
    _ax = Vector((_T.x - _A.x, _T.z - _A.z)).normalized(); _nb = Vector((-_ax.y, _ax.x))
    if _nb.x > 0: _nb = -_nb                                   # points back, behind the steering axis
    _S0, _S1, _Y0, _Y1 = float(OPT.get("s0", 0.085)), float(OPT.get("s1", 0.115)), 0.040, 0.090
    _ei = [i for i, sl in enumerate(_ob.material_slots) if sl.name == "DR2_Electric_UV"]
    _vs = {v for p in _ob.data.polygons if p.material_index in _ei for v in p.vertices}
    _n = 0
    for i in _vs:
        v = _ob.data.vertices[i]; w = _MW @ v.co
        sb = Vector((w.x - _A.x, w.z - _A.z)).dot(_nb)
        if sb >= _S1 or abs(w.y) < 1e-6: continue
        lim = _Y0 + (_Y1 - _Y0) * max(0.0, sb - _S0) / (_S1 - _S0)
        f = min(1.0, lim / _Y1)
        if f < 1.0:
            v.co = _IM @ Vector((w.x, w.y * f, w.z)); _n += 1
    print("ELECTRIC steering clearance: narrowed", _n, "points")
if SAVE_AS:
    bpy.ops.wm.save_as_mainfile(filepath=SAVE_AS, copy=True, compress=True); print("SAVED copy", SAVE_AS)
else:
    bpy.ops.wm.save_mainfile(compress=True); print("SAVED")
