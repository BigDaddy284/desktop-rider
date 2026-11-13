"""Shared machinery for the Desktop Rider jump, trick and whoop animations.

Runs inside Blender's Python (the `bpy` module). It keys the owner's rigged bike and rider models; it never
changes their shapes, colours or graphics. See Documentation/SPRITE_SPECIFICATION.md for the conventions.
"""
import bpy, math, json
from mathutils import Vector, Quaternion

FPS = 24                      # keyed frames per second; sprites use every second frame (12 a second)
G = 2.6                       # gravity in the miniature world, m/s^2 (see the specification: real trick timing, small ramp)
V = 3.0                       # riding speed, m/s (the same as the existing riding loop)
RA = (-0.544, 0.248)          # rear axle (x, z) at rest
FA = (0.544, 0.263)           # front axle
RR, RF = 0.248, 0.263         # wheel radii
FREL = (FA[0] - RA[0], FA[1] - RA[1])
CREL = (0.50, 0.30)           # centre of mass of bike and rider, measured from the rear axle
PEG = {'L': Vector((0.05, 0.165, 0.31)), 'R': Vector((0.05, -0.165, 0.31))}
GRIP = {'L': Vector((0.17, 0.345, 1.0)), 'R': Vector((0.17, -0.345, 1.0))}
AX = {'X': Vector((1, 0, 0)), 'Y': Vector((0, 1, 0)), 'Z': Vector((0, 0, 1))}
PARK = Vector((0, 0, -50))    # where props wait when they are not in an animation


def sm(t, a, b):
    if b == a:
        return 1.0 if t >= b else 0.0
    x = min(1.0, max(0.0, (t - a) / (b - a)))
    return x * x * (3 - 2 * x)


def bump(t, a, b, c, d):
    return sm(t, a, b) * (1 - sm(t, c, d))


def lerp(a, b, w):
    return a + (b - a) * w


def rot(p, deg):
    """Rotate (x, z) about +Y by deg. Positive puts the nose down, as in the original animation script."""
    a = math.radians(deg)
    return (p[0] * math.cos(a) + p[1] * math.sin(a), -p[0] * math.sin(a) + p[1] * math.cos(a))


def shock(tau, rise=0.09, settle=0.75):
    """How a suspension stroke looks after a hit at tau = 0: fast in, slower out, one small overshoot."""
    if tau <= 0:
        return 0.0
    if tau < rise:
        return math.sin(0.5 * math.pi * tau / rise)
    x = (tau - rise) / (settle - rise)
    if x >= 1:
        return 0.0
    return math.exp(-3.4 * x) * math.cos(1.5 * math.pi * x) * (1 - x * x * x)


# ---------------------------------------------------------------- terrain
class Terrain:
    """A height profile h(X) along the world, built from pieces. Each piece is drawn as its own prop."""

    def __init__(self):
        self.pieces = []      # (x0, x1, fn, kind)

    def add(self, x0, x1, fn, kind='dirt'):
        self.pieces.append((x0, x1, fn, kind))
        return self

    def h(self, x):
        best = 0.0
        for x0, x1, fn, _ in self.pieces:
            if x0 <= x <= x1:
                best = max(best, fn(x - x0))
        return best

    def axle(self, x, r, floor=None):
        """Height of a wheel's axle when the wheel of radius r rests on the ground at x."""
        best = 0.0
        n = 12
        for i in range(-n, n + 1):
            dx = r * i / n
            hh = self.h(x + dx) if floor is None else max(self.h(x + dx), floor(x + dx))
            best = max(best, hh + math.sqrt(max(0.0, r * r - dx * dx)))
        return best


def ground_pose(terrain, xc, floor=None):
    """Pitch (degrees, + nose down) and rear axle position with both wheels on the ground and the centre of mass at xc."""
    def resid(phi):
        c = rot(CREL, phi); f = rot(FREL, phi)
        xr = xc - c[0]
        zr = terrain.axle(xr, RR, floor)
        return (zr + f[1]) - terrain.axle(xr + f[0], RF, floor), xr, zr
    lo, hi = -75.0, 75.0
    flo = resid(lo)[0]
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        fm = resid(mid)[0]
        if (fm > 0) == (flo > 0):
            lo, flo = mid, fm
        else:
            hi = mid
    phi = 0.5 * (lo + hi)
    _, xr, zr = resid(phi)
    return phi, xr, zr


# ---------------------------------------------------------------- props
def material(name, colour, rough=0.9, metallic=0.0):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*colour, 1)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metallic
    return m


def _mesh(name, quads_by_mat, mats):
    """quads_by_mat: list of (material index, [four (x, z) corners]) turned into slabs across the track."""
    old = bpy.data.objects.get(name)
    if old:
        me = old.data; bpy.data.objects.remove(old); bpy.data.meshes.remove(me)
    verts, faces, fm = [], [], []
    for mi, pts, y0, y1 in quads_by_mat:
        n = len(pts); base = len(verts)
        verts += [(x, y0, z) for x, z in pts] + [(x, y1, z) for x, z in pts]
        faces.append(tuple(range(base, base + n))); fm.append(mi)
        faces.append(tuple(range(base + 2 * n - 1, base + n - 1, -1))); fm.append(mi)
        for i in range(n):
            j = (i + 1) % n
            faces.append((base + i, base + n + i, base + n + j, base + j)); fm.append(mi)
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    for m in mats:
        me.materials.append(m)
    for p, mi in zip(me.polygons, fm):
        p.material_index = mi
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = PARK
    return ob


def dirt_prop(name, length, fn, half_width=0.55, step=0.03):
    """A mound of dirt with profile fn(x) for x in 0..length, standing on the ground."""
    dirt = material('DR_Dirt', (0.42, 0.26, 0.14)); dark = material('DR_DirtDark', (0.27, 0.16, 0.085))
    light = material('DR_DirtLight', (0.52, 0.35, 0.2))
    n = max(2, int(round(length / step)))
    xs = [length * i / n for i in range(n + 1)]
    quads = []
    for a, b in zip(xs[:-1], xs[1:]):
        ha, hb = fn(a), fn(b)
        if ha <= 0.002 and hb <= 0.002:
            continue
        # body, a darker packed band under the surface, and a lighter strip low down
        quads.append((0, [(a, 0), (b, 0), (b, hb), (a, ha)], -half_width, half_width))
        quads.append((1, [(a, max(0, ha - 0.035)), (b, max(0, hb - 0.035)), (b, hb), (a, ha)], -half_width - 0.004, half_width + 0.004))
        quads.append((2, [(a, 0), (b, 0), (b, min(hb, 0.03)), (a, min(ha, 0.03))], -half_width - 0.004, half_width + 0.004))
    return _mesh(name, quads, [dirt, dark, light])


def steel_ramp_prop(name, length, fn, half_width=0.5, step=0.04):
    """A freestyle take-off ramp: a curved deck on a tube frame."""
    deck = material('DR_RampDeck', (0.07, 0.07, 0.08), 0.6, 0.2); tube = material('DR_RampTube', (0.62, 0.64, 0.67), 0.45, 0.6)
    n = max(2, int(round(length / step)))
    xs = [length * i / n for i in range(n + 1)]
    quads = []
    t = 0.04
    for a, b in zip(xs[:-1], xs[1:]):
        quads.append((0, [(a, fn(a) - t), (b, fn(b) - t), (b, fn(b)), (a, fn(a))], -half_width, half_width))
    def bar(p, q, w=0.028, y=half_width + 0.01):
        d = Vector((q[0] - p[0], q[1] - p[1])); L = d.length
        if L < 1e-4:
            return
        nrm = Vector((-d.y, d.x)) / L * (w / 2)
        pts = [(p[0] - nrm.x, p[1] - nrm.y), (q[0] - nrm.x, q[1] - nrm.y), (q[0] + nrm.x, q[1] + nrm.y), (p[0] + nrm.x, p[1] + nrm.y)]
        quads.append((1, pts, -y, -y + 0.03)); quads.append((1, pts, y - 0.03, y))
    posts = [length * k for k in (0.42, 0.62, 0.80, 0.985)]
    start = length * 0.2
    bar((start, 0.014), (length, 0.014))
    prev = (start, 0.014)
    for x in posts:
        top = (x, fn(x) - t - 0.01)
        bar((x, 0.0), top)
        bar(prev, top)
        prev = (x, 0.014)
    # the rail under the deck
    for a, b in zip(xs[:-1], xs[1:]):
        if a >= start:
            bar((a, fn(a) - t - 0.012), (b, fn(b) - t - 0.012), 0.024)
    return _mesh(name, quads, [deck, tube])


# ---------------------------------------------------------------- keying
class Rig:
    def __init__(self):
        self.brig = bpy.data.objects['bike_rig']; self.rrig = bpy.data.objects['rider_rig']
        self.last = {}

    def reset(self):
        for rig, nm in ((self.brig, 'DR_bike'), (self.rrig, 'DR_rider')):
            if rig.animation_data and rig.animation_data.action:
                a = rig.animation_data.action; rig.animation_data.action = None; bpy.data.actions.remove(a)
            old = bpy.data.actions.get(nm)
            if old:
                bpy.data.actions.remove(old)
            rig.animation_data_create(); rig.animation_data.action = bpy.data.actions.new(nm)
            for pb in rig.pose.bones:
                pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0)
        self.last = {}

    def key(self, rig, bone, f, rots=(), loc=(0, 0, 0), local_loc=None):
        pb = rig.pose.bones[bone]; R = rig.data.bones[bone].matrix_local.to_3x3(); Ri = R.inverted()
        Q = Quaternion()
        for ax, deg in rots:
            Q = Quaternion(AX[ax], math.radians(deg)) @ Q
        q = (Ri @ Q.to_matrix() @ R).to_quaternion()
        k = (rig.name, bone)
        if k in self.last and self.last[k].dot(q) < 0:
            q.negate()
        self.last[k] = q.copy()
        pb.rotation_quaternion = q
        pb.location = Vector(local_loc) if local_loc is not None else Ri @ Vector(loc)
        pb.keyframe_insert('rotation_quaternion', frame=f); pb.keyframe_insert('location', frame=f)

    def wheel(self, bone, f, deg):
        pb = self.brig.pose.bones[bone]; pb.rotation_euler = (0, math.radians(deg), 0); pb.keyframe_insert('rotation_euler', frame=f)

    def apply(self, f, d):
        """Keys one frame from a state dictionary (see `state()`)."""
        b, r = self.brig, self.rrig
        air = d['air']                                   # 0 on the ground, 1 in the air
        extra = math.degrees(math.asin(max(-1, min(1, (d['dive'] * 0.9 - d['squat']) / 1.088))))
        pitch = d['pitch'] + extra * (1 - air)
        self.key(b, 'chassis', f, (('X', d['roll']), ('Y', pitch), ('Z', d['yaw'])), (d['dx'], d['dy'], d['dz'] - d['squat'] * (1 - air)))
        self.key(b, 'swingarm', f, (('Y', math.degrees(math.asin(max(-1, min(1, d['squat'] / 0.48))))),))
        self.key(b, 'steer', f, (), local_loc=(0, 0, 0)); pb = b.pose.bones['steer']
        pb.rotation_quaternion = Quaternion((0, 1, 0), math.radians(d['steer'])); pb.keyframe_insert('rotation_quaternion', frame=f)
        self.key(b, 'fork_lower', f, (), local_loc=(0, -d['dive'], 0))
        self.wheel('rear_wheel', f, d['wr']); self.wheel('front_wheel', f, d['wf'])
        for s in 'LR':
            self.key(b, 'grip.' + s, f, (), d['grip' + s]); self.key(b, 'peg.' + s, f, (), d['peg' + s])
        self.key(r, 'hips', f, _rots(d['hipsrot']), d['hips']); self.key(r, 'spine', f, _rots(d['spine'])); self.key(r, 'head', f, _rots(d['head']))


def _rots(v):
    return (('Y', v[1]), ('X', v[0]), ('Z', v[2]))


def state():
    z3 = (0.0, 0.0, 0.0)
    return dict(pitch=0.0, roll=0.0, yaw=0.0, dx=0.0, dy=0.0, dz=0.0, squat=0.0, dive=0.0, steer=0.0, wr=0.0, wf=0.0, air=0.0,
                gripL=z3, gripR=z3, pegL=z3, pegR=z3, hips=z3, hipsrot=z3, spine=z3, head=(0.0, 0.45, 0.0))


# ---------------------------------------------------------------- rider poses
RIDER_KEYS = ('gripL', 'gripR', 'pegL', 'pegR', 'hips', 'hipsrot', 'spine', 'head')


def pose(**kw):
    p = {k: (0.0, 0.0, 0.0) for k in RIDER_KEYS}
    p['head'] = (0.0, 0.45, 0.0)
    for k, v in kw.items():
        if k not in p:
            raise KeyError(k)
        p[k] = tuple(float(x) for x in v)
    return p


def mix(*weighted):
    """Weighted sum of poses: mix((pose, weight), ...). Weights should add up to 1."""
    out = {}
    for k in RIDER_KEYS:
        out[k] = tuple(sum(p[k][i] * w for p, w in weighted) for i in range(3))
    return out


def blend(a, b, w):
    return mix((a, 1 - w), (b, w))


REST = pose()
# Standing on the pegs, knees bent, elbows up, looking ahead: the neutral "attack" position.
ATTACK = pose(hips=(0.07, 0, 0.10), spine=(0, 20, 0), head=(0, -14, 0))
# Sitting forward for acceleration.
SEATED_FWD = pose(hips=(0.04, 0, 0.0), spine=(0, 12, 0), head=(0, -8, 0))
# Legs soaking up a hit: hips dropped and back a little, chest down.
ABSORB = pose(hips=(0.02, 0, 0.0), spine=(0, 26, 0), head=(0, -18, 0))
