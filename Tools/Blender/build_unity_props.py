"""Builds clean, real-time versions of the Desktop Rider trick props and exports them for Unity.

Run in a background Blender with no file open:

    blender --background --factory-startup --python build_unity_props.py -- <Tools/Blender dir> <output dir> [check dir] [--plain]

Dirt props get real-track detail from prop_detail.py (References/Terrain); --plain rebuilds the version 1 shapes.

It imports trick_animations.py / trick_core.py from the first folder (it only uses their pure-maths terrain
builders; it needs no rig and changes no .blend), rebuilds each distinct terrain piece as one closed mesh with
the same outline, writes one FBX per prop plus props_layout.json into the output folder, and, if a check folder
is given, re-imports the FBX files and renders two check pictures there.

The sprite-version props are stacks of overlapping boxes (fine for a side-on sprite render, but they flicker in a
real-time engine). Here every dirt piece is a single closed solid: dark top surface and top band, body colour in
the middle of the sides, a light strip along the bottom, exactly as the sprite version looks from the side.
"""
import bpy, bmesh, sys, os, json, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
TOOLS, OUT = argv[0], argv[1]
_rest = [a for a in argv[2:] if not a.startswith('--')]
CHECK = _rest[0] if _rest else None
sys.path.insert(0, TOOLS)
import trick_animations as TA   # noqa: E402
import trick_core as TC         # noqa: E402
import prop_detail as PD        # noqa: E402
try:
    import dirt_texture as DT   # noqa: E402  (needs numpy, which Blender ships)
except ImportError:
    DT = None

# Real-track detail (References/Terrain): sloping sides, tyre groove, damp and dry dirt, clods, a dirt bank at the
# foot of the steel ramp. False rebuilds the plain version 1 shapes.
DETAIL = '--plain' not in argv

DIRT_HALF_WIDTH = 0.55
DIRT_STEP = 0.03
BAND = 0.035      # dark packed band under the surface
STRIP = 0.03      # light strip along the bottom
STEEL_HALF_WIDTH = 0.5
STEEL_STEP = 0.04
DECK_T = 0.04


def new_object(name, bm, mats):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    open_edges = sum(1 for e in bm.edges if len(e.link_faces) != 2)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for m in mats:
        me.materials.append(m)
    # a trivial UV map (flat colours only; this just keeps Unity's importer quiet)
    uv = me.uv_layers.new(name="UVMap")
    xs = [v.co.x for v in me.vertices]; zs = [v.co.z for v in me.vertices]
    lx = max(max(xs) - min(xs), 1e-6); lz = max(max(zs) - min(zs), 1e-6)
    for li, loop in enumerate(me.loops):
        co = me.vertices[loop.vertex_index].co
        uv.data[li].uv = ((co.x - min(xs)) / lx, (co.z - min(zs)) / lz)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob, open_edges


class Builder:
    def __init__(self):
        self.bm = bmesh.new(); self.cache = {}

    def v(self, x, y, z):
        k = (round(x, 6), round(y, 6), round(z, 6))
        if k not in self.cache:
            self.cache[k] = self.bm.verts.new(k)
        return self.cache[k]

    def face(self, pts, mat):
        vs = []
        for p in pts:
            b = self.v(*p)
            if not vs or (b is not vs[-1]):
                vs.append(b)
        if len(vs) > 1 and vs[0] is vs[-1]:
            vs.pop()
        if len(set(vs)) < 3:
            return
        try:
            f = self.bm.faces.new(vs)
        except ValueError:
            return
        f.material_index = mat

    def prism(self, poly_xz, y0, y1, mat):
        """A closed prism: the (x, z) polygon swept from y0 to y1."""
        n = len(poly_xz)
        self.face([(x, y0, z) for x, z in poly_xz], mat)
        self.face([(x, y1, z) for x, z in reversed(poly_xz)], mat)
        for i in range(n):
            a, b = poly_xz[i], poly_xz[(i + 1) % n]
            self.face([(a[0], y0, a[1]), (a[0], y1, a[1]), (b[0], y1, b[1]), (b[0], y0, b[1])], mat)


def levels(h):
    """Side-wall heights at a profile height h: ground, top of the light strip, bottom of the dark band, surface."""
    d = max(0.0, h - BAND)
    return (0.0, min(STRIP, d), d, h)


def build_dirt(name, length, fn):
    DIRT, DARK, LIGHT = 0, 1, 2
    mats = [TC.material('DR_Dirt', (0.42, 0.26, 0.14)), TC.material('DR_DirtDark', (0.27, 0.16, 0.085)), TC.material('DR_DirtLight', (0.52, 0.35, 0.2))]
    n = max(2, int(round(length / DIRT_STEP)))
    xs = [length * i / n for i in range(n + 1)]
    hs = [max(0.0, fn(x)) for x in xs]
    # Unlike the sprite version (which drops slices under 2 mm and so leaves slits between the whoops), keep every
    # slice that has any height at all, so valleys that only touch the ground stay joined up.
    hs = [0.0 if h <= 1e-5 else h for h in hs]
    active = [hs[i] > 0 or hs[i + 1] > 0 for i in range(n)]
    B = Builder(); w = DIRT_HALF_WIDTH
    region = (LIGHT, DIRT, DARK)

    def cap(i):
        lv = levels(hs[i])
        for k in range(3):
            B.face([(xs[i], -w, lv[k]), (xs[i], w, lv[k]), (xs[i], w, lv[k + 1]), (xs[i], -w, lv[k + 1])], region[k])

    for i in range(n):
        if not active[i]:
            continue
        a, b = xs[i], xs[i + 1]; la, lb = levels(hs[i]), levels(hs[i + 1])
        B.face([(a, -w, la[3]), (a, w, la[3]), (b, w, lb[3]), (b, -w, lb[3])], DARK)       # riding surface
        B.face([(a, -w, 0), (b, -w, 0), (b, w, 0), (a, w, 0)], DIRT)                        # underside
        for y in (-w, w):
            for k in range(3):
                B.face([(a, y, la[k]), (b, y, lb[k]), (b, y, lb[k + 1]), (a, y, la[k + 1])], region[k])
        if (i == 0 or not active[i - 1]) and hs[i] > 0:
            cap(i)
        if (i == n - 1 or not active[i + 1]) and hs[i + 1] > 0:
            cap(i + 1)
    return new_object(name, B.bm, mats), (xs, hs)


TEXTURE_SIZE = 1024
TILES = {}              # dirt kind -> (colour, normal) tile, made once per run


def dirt_tiles():
    """The three kinds of dirt (dirt_texture.py), made once and blended per prop by dirt_bake.py."""
    if not TILES and DT is not None:
        for kind in DT.KINDS:
            TILES[kind] = DT.make_maps(kind, TEXTURE_SIZE, seed=1)
    return TILES


def save_image(name, rgb, data):
    """Saves an image to OUT/Textures (where the FBX files point) and returns the Blender image."""
    folder = os.path.join(OUT, 'Textures'); os.makedirs(folder, exist_ok=True)
    h, w = rgb.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=False, is_data=data)
    rgba = DT.np.ones((h, w, 4), dtype=DT.np.float32)
    rgba[..., :3] = rgb[::-1]                     # Blender stores the bottom row first
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = os.path.join(folder, name + '.png'); img.file_format = 'PNG'; img.save()
    if data:
        img.colorspace_settings.name = 'Non-Color'
    return img


def baked_dirt_material(name, verts, xs, hs, half_top, groove_depth, seed):
    """One material for a prop's dirt, with its own colour and normal textures blended from the three kinds of dirt
    by how each spot is worn (dirt_bake.py). Unity treats files ending _normal as normal maps (ProofModelImport.cs).
    Returns (material, footprint) or (plain material, None) without numpy."""
    if DT is None:
        return TC.material('DR_Dirt', (0.42, 0.26, 0.14)), None
    import dirt_bake as DB
    margin = 0.05
    foot = DB.Footprint(min(v[0] for v in verts) - margin, min(v[1] for v in verts) - margin,
                        max(v[0] for v in verts) + margin, max(v[1] for v in verts) + margin)
    weights = DB.wear_weights(foot, xs, hs, half_top, groove_depth, seed)
    colour, normal = DB.bake(foot, weights, dirt_tiles())
    short = name.replace('DR_Prop_', '')
    m = TC.material('DR_Dirt_' + short, (1.0, 1.0, 1.0))
    nodes = m.node_tree.nodes; bsdf = nodes.get('Principled BSDF')
    tex = nodes.new('ShaderNodeTexImage'); tex.image = save_image('DR_Dirt_' + short, colour, False)
    m.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    ntex = nodes.new('ShaderNodeTexImage'); ntex.image = save_image('DR_Dirt_' + short + '_normal', normal, True)
    nmap = nodes.new('ShaderNodeNormalMap')
    m.node_tree.links.new(ntex.outputs['Color'], nmap.inputs['Color'])
    m.node_tree.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    bsdf.inputs['Roughness'].default_value = 0.95
    return m, foot


def footprint_uvs(ob, foot, only=None):
    """Lays the prop's own dirt texture over it from above (U along the track)."""
    if foot is None:
        return
    me = ob.data; uv = me.uv_layers.active
    for poly in me.polygons:
        if only is not None and poly.material_index not in only:
            continue
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = foot.uv(co.x, co.y)


def build_dirt_detailed(name, length, fn, seed=7):
    half_top, groove = 0.45, 0.006
    verts, faces = PD.dirt_mesh(length, fn, half_top=half_top, groove_depth=groove, seed=seed)
    xs, hs = PD.outline(length, fn)
    mat, foot = baked_dirt_material(name, verts, xs, hs, half_top, groove, seed)
    bm = bmesh.new()
    add_dirt_faces(bm, verts, [(idx, 0) for idx, _ in faces], 0)
    n = max(2, int(round(length / DIRT_STEP)))
    gx = [length * i / n for i in range(n + 1)]
    ob, open_edges = new_object(name, bm, [mat])
    footprint_uvs(ob, foot)
    return (ob, open_edges), (gx, [max(0.0, fn(x)) for x in gx])


def add_dirt_faces(bm, verts, faces, first_material):
    """Adds a dirt mesh from prop_detail to bm, smooth shaded so the mound reads as soft dirt rather than flat
    facets. The underside and the end caps stay flat and their edges sharp, so they do not darken the sides."""
    bv = [bm.verts.new(v) for v in verts]
    for idx, mat in faces:
        try:
            f = bm.faces.new([bv[i] for i in idx])
        except ValueError:
            continue
        f.material_index = first_material + mat
        f.normal_update()
        flat = abs(f.normal.z) > 0.999 and f.calc_center_median().z < 1e-6 or abs(f.normal.x) > 0.999
        f.smooth = not flat
        if flat:
            for e in f.edges:
                e.smooth = False


def seed_for(name):
    return sum(ord(c) for c in name) % 997


def build_steel(name, length, fn):
    DECK, TUBE = 0, 1
    mats = [TC.material('DR_RampDeck', (0.07, 0.07, 0.08), 0.6, 0.2), TC.material('DR_RampTube', (0.62, 0.64, 0.67), 0.45, 0.6)]
    n = max(2, int(round(length / STEEL_STEP)))
    xs = [length * i / n for i in range(n + 1)]
    B = Builder(); w = STEEL_HALF_WIDTH
    # the deck: one curved slab
    top = [(x, fn(x)) for x in xs]; bot = [(x, fn(x) - DECK_T) for x in xs]
    for i in range(n):
        a, b = xs[i], xs[i + 1]
        B.face([(a, -w, top[i][1]), (a, w, top[i][1]), (b, w, top[i + 1][1]), (b, -w, top[i + 1][1])], DECK)
        B.face([(a, -w, bot[i][1]), (b, -w, bot[i + 1][1]), (b, w, bot[i + 1][1]), (a, w, bot[i][1])], DECK)
        for y in (-w, w):
            B.face([(a, y, bot[i][1]), (b, y, bot[i + 1][1]), (b, y, top[i + 1][1]), (a, y, top[i][1])], DECK)
    for i in (0, n):
        B.face([(xs[i], -w, bot[i][1]), (xs[i], w, bot[i][1]), (xs[i], w, top[i][1]), (xs[i], -w, top[i][1])], DECK)
    # the tube frame, one on each side, same lay-out as the sprite version
    yo = w + 0.01

    def bar(p, q, wd=0.028):
        d = Vector((q[0] - p[0], q[1] - p[1])); L = d.length
        if L < 1e-4:
            return
        nrm = Vector((-d.y, d.x)) / L * (wd / 2)
        pts = [(p[0] - nrm.x, p[1] - nrm.y), (q[0] - nrm.x, q[1] - nrm.y), (q[0] + nrm.x, q[1] + nrm.y), (p[0] + nrm.x, p[1] + nrm.y)]
        for y0, y1 in ((-yo, -yo + 0.03), (yo - 0.03, yo)):
            Bb = Builder(); Bb.bm = B.bm; Bb.cache = {}        # separate closed boxes inside the same mesh
            Bb.prism(pts, y0, y1, TUBE)

    posts = [length * k for k in (0.42, 0.62, 0.80, 0.985)]
    start = length * 0.2
    bar((start, 0.014), (length, 0.014))
    prev = (start, 0.014)
    for x in posts:
        tp = (x, fn(x) - DECK_T - 0.01)
        bar((x, 0.0), tp); bar(prev, tp)
        prev = (x, 0.014)
    # the rail under the deck: one continuous strip each side
    rx = [x for x in xs if x >= start - 1e-9]
    mid = [(x, fn(x) - DECK_T - 0.012) for x in rx]
    up, dn = [], []
    for i, (x, z) in enumerate(mid):
        p0 = mid[max(i - 1, 0)]; p1 = mid[min(i + 1, len(mid) - 1)]
        d = Vector((p1[0] - p0[0], p1[1] - p0[1])).normalized(); nr = Vector((-d.y, d.x)) * 0.012
        up.append((x + nr.x, z + nr.y)); dn.append((x - nr.x, z - nr.y))
    for y0, y1 in ((-yo, -yo + 0.03), (yo - 0.03, yo)):
        Bb = Builder(); Bb.bm = B.bm; Bb.cache = {}
        Bb.prism(dn + up[::-1], y0, y1, TUBE)
    if DETAIL:
        # Dirt pushed up against the bottom of the deck, as at real freestyle events, so the bike rolls on from the dirt.
        # It sits just under the deck and a little wider, so it shows along both sides of the lower half of the ramp.
        bank_len = length * 0.45
        bank = lambda x: max(0.0, fn(x) - 0.012 + 0.02 * (1 - TC.sm(x, 0, 0.15))) * (1 - TC.sm(x, bank_len * 0.6, bank_len)) if x <= bank_len else 0.0
        verts, faces = PD.dirt_mesh(bank_len, bank, half_top=w + 0.06, seed=seed_for(name), groove_depth=0.0, clods=6, back=False)
        bx, bh = PD.outline(bank_len, bank, back=False)
        dirt, foot = baked_dirt_material(name, verts, bx, bh, w + 0.06, 0.0, seed_for(name))
        mats.append(dirt)
        add_dirt_faces(B.bm, verts, [(idx, 0) for idx, _ in faces], 2)
        ob, open_edges = new_object(name, B.bm, mats)
        footprint_uvs(ob, foot, only={2})
        return (ob, open_edges), (xs, [fn(x) for x in xs])
    return new_object(name, B.bm, mats), (xs, [fn(x) for x in xs])


def prop_name(anim, index, kind):
    if kind == 'steel':
        return 'DR_Prop_SteelRamp'
    if anim == 'Whoops':
        return 'DR_Prop_Whoops'
    if anim in ('Jump', 'Whip', 'Scrub'):
        return 'DR_Prop_%s%s' % (anim, 'Ramp' if index == 0 else 'Landing')
    return 'DR_Prop_TrickLanding'


def export_fbx(ob, path):
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        if o is not None:
            o.select_set(False)
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, mesh_smooth_type='FACE',
                             use_mesh_modifiers=True, bake_anim=False, add_leaf_bones=False, path_mode='RELATIVE')


def main():
    os.makedirs(OUT, exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    TA.configure('Left')
    built = {}; layout = dict(units='metres', fps=TC.FPS, note='Props are static. x is along the direction of travel, measured from the bike rig origin at the first frame of the animation; '
                              'travel[i] is how far the bike has moved forward at frame i (the sprite version moved the props back by this instead). Prop origin: start of the piece, on the ground, centre of the track.',
                  props={}, animations=[])
    f0 = 1; report = []
    for a in TA.animations():
        states, terr, info = a['build']()
        N = len(states) - 1
        entry = dict(name=a['name'], stage=a['stage'], start=f0, end=f0 + N, frames=N, props=[],
                     travel=[round(float(d['_s']), 4) for d in states],
                     info={k: (round(v, 4) if isinstance(v, float) else v) for k, v in info.items()})
        for i, (x0, x1, fn, kind) in enumerate(terr.pieces):
            nm = prop_name(a['name'], i, kind); L = x1 - x0
            if nm not in built:
                if kind == 'steel':
                    (ob, open_edges), prof = build_steel(nm, L, fn)
                elif DETAIL:
                    (ob, open_edges), prof = build_dirt_detailed(nm, L, fn, seed_for(nm))
                else:
                    (ob, open_edges), prof = build_dirt(nm, L, fn)
                # compare against the sprite-version prop made by the original script
                ref = (TC.steel_ramp_prop if kind == 'steel' else TC.dirt_prop)('REF_' + nm, L, fn)
                rv = [v.co.copy() for v in ref.data.vertices]; mv = [v.co.copy() for v in ob.data.vertices]
                def envelope(vs, top=True):
                    e = {}
                    for c in vs:
                        k = round(c.x, 4); e[k] = max(e.get(k, -9), c.z) if top else min(e.get(k, 9), c.z)
                    return e
                dev = 0.0
                # dirt: compare the top and bottom outlines; steel: the riding surface (the frame bars are copied one for one)
                for top in ((True,) if kind == 'steel' else (True, False)):
                    ea, eb = envelope(rv, top), envelope(mv, top)
                    xsb = sorted(eb)
                    samples = {round(x, 4) for x in prof[0]}
                    for x, z in ea.items():
                        if x not in samples or x not in eb:
                            continue                      # only compare at the profile's own sample positions
                        j = xsb.index(x)
                        if abs(xsb[j] - x) < 1e-3:
                            dev = max(dev, abs(eb[xsb[j]] - z))
                rb = [max(c[k] for c in rv) - min(c[k] for c in rv) for k in range(3)]; mb = [max(c[k] for c in mv) - min(c[k] for c in mv) for k in range(3)]
                rtris = sum(len(p.vertices) - 2 for p in ref.data.polygons)
                me = ref.data; bpy.data.objects.remove(ref); bpy.data.meshes.remove(me)
                path = os.path.join(OUT, nm + '.fbx'); export_fbx(ob, path)
                built[nm] = ob
                layout['props'][nm] = dict(file=nm + '.fbx', kind=kind, length=round(mb[0], 4), width=round(mb[1], 4), height=round(mb[2], 4),
                                           triangles=len(ob.data.polygons), materials=[m.name for m in ob.data.materials])
                report.append('%-22s %s  size %.3f x %.3f x %.3f (script %.3f x %.3f x %.3f)  tris %d (script %d)  open edges %d  outline deviation %.2f mm  file %d bytes' % (
                    nm, kind, mb[0], mb[1], mb[2], rb[0], rb[1], rb[2], len(ob.data.polygons), rtris, open_edges, dev * 1000, os.path.getsize(path)))
            entry['props'].append(dict(prop=nm, x=round(x0, 4), length=round(L, 4)))
        layout['animations'].append(entry)
        f0 += N + 1 + 4
    json.dump(layout, open(os.path.join(OUT, 'props_layout.json'), 'w'), indent=1)
    for r in report:
        print('BUILT', r)
    for e in layout['animations']:
        print('ANIM %-18s frames %4d-%4d  props %s' % (e['name'], e['start'], e['end'], [(p['prop'], p['x']) for p in e['props']]))
    if CHECK:
        check(layout)


def check(layout):
    os.makedirs(CHECK, exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    z = 0.0; maxl = 0.0
    for nm, p in layout['props'].items():
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, p['file']))
        new = [o for o in bpy.data.objects if o not in before]
        for o in new:
            bpy.context.view_layer.update()
            bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
            d = [max(c[k] for c in bb) - min(c[k] for c in bb) for k in range(3)]
            per = [sum(1 for f in o.data.polygons if f.material_index == i) for i in range(len(o.data.materials))]
            print('REIMPORT %-22s size %.3f x %.3f x %.3f  tris %d  uv %d  materials %s' % (nm, d[0], d[1], d[2], sum(len(f.vertices) - 2 for f in o.data.polygons), len(o.data.uv_layers),
                                                                                            list(zip([m.name.split('.')[0] for m in o.data.materials], per))))
            o.location.z += z
            maxl = max(maxl, d[0])
        z -= p['height'] + 0.45
    sc = bpy.context.scene
    w = bpy.data.worlds.new('w'); sc.world = w; w.color = (0.75, 0.75, 0.75)
    w.use_nodes = True; next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND').inputs[0].default_value = (0.75, 0.75, 0.75, 1)
    ld = bpy.data.lights.new('sun', 'SUN'); ld.energy = 3.0; lo = bpy.data.objects.new('sun', ld); sc.collection.objects.link(lo); lo.rotation_euler = (math.radians(50), 0, math.radians(30))
    cd = bpy.data.cameras.new('c'); cd.type = 'ORTHO'; cam = bpy.data.objects.new('c', cd); sc.collection.objects.link(cam); sc.camera = cam
    total = -z
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.image_settings.file_format = 'PNG'
    c = Vector((maxl / 2, 0, z / 2 + 0.6))
    for nm, off in (('side', Vector((0, -8, 0))), ('threequarter', Vector((5, -6, 4)))):
        cam.location = c + off; cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        cd.ortho_scale = max(maxl, total) * 1.2
        sc.render.resolution_x = 1600; sc.render.resolution_y = 1600
        sc.render.filepath = os.path.join(CHECK, 'props_check_%s.png' % nm); bpy.ops.render.render(write_still=True)

    # As they will be seen on the desktop: laid out in a grid on the ground, looked down on at 45 degrees like the
    # Unity camera, with plain colours (no film-style tone curve) so the dirt shades can be judged.
    sc.view_settings.view_transform = 'Standard'
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    col_w = maxl + 0.6; row = 0; x = 0.0; y = 0.0; row_depth = 0.0
    for k, o in enumerate(meshes):
        if k and k % 3 == 0:
            y -= row_depth + 0.6; x = 0.0; row_depth = 0.0
        o.location = (0, 0, 0); bpy.context.view_layer.update()
        bb = [o.matrix_world @ Vector(cc) for cc in o.bound_box]
        depth = max(cc.y for cc in bb) - min(cc.y for cc in bb)
        o.location = (x - min(cc.x for cc in bb), y - max(cc.y for cc in bb), 0)
        x += col_w; row_depth = max(row_depth, depth)
    y -= row_depth
    width = 3 * col_w; c = Vector((width / 2, y / 2, 0.0))
    cam.rotation_euler = (math.radians(45), 0, 0)
    cam.location = c - (cam.rotation_euler.to_matrix() @ Vector((0, 0, -1))) * 30
    sc.render.resolution_x = 1800; sc.render.resolution_y = 1400
    cd.ortho_scale = max(width, -y * 0.75 * 1800 / 1400) * 1.05
    lo.rotation_euler = (math.radians(40), 0, math.radians(-35))
    sc.render.filepath = os.path.join(CHECK, 'props_check_desktop.png'); bpy.ops.render.render(write_still=True)

main()
