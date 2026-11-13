"""Checks DesktopRider.fbx (and the prop FBX files) against the working .blend and the master .blend.

    blender --background --factory-startup --python verify_models.py -- <Models dir> <working .blend> <master .blend> <out dir>

Nothing is saved; both .blend files are only read. Prints one PASS/FAIL line per check and writes verify_report.json
and a textured check render into the out dir. Exits with status 1 if any check fails.
"""
import bpy, sys, os, json, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
MODELS, WORK, MASTER, OUT = argv[:4]
os.makedirs(OUT, exist_ok=True)
FBX = os.path.join(MODELS, "DesktopRider.fbx")
man = json.load(open(os.path.join(MODELS, "DesktopRider_model_manifest.json")))
MESHES = ("race_bike_youth", "race_rider"); RIGS = ("bike_rig", "rider_rig")
results = []


def check(name, ok, detail):
    results.append(dict(check=name, result="PASS" if ok else "FAIL", detail=detail))
    print("VERIFY %s  %s  %s" % ("PASS" if ok else "FAIL", name, detail))


def world_mesh(ob):
    ev = ob.evaluated_get(bpy.context.evaluated_depsgraph_get()); me = ev.to_mesh()
    r = [ev.matrix_world @ v.co for v in me.vertices]; ev.to_mesh_clear(); return r


def bone_world(rig):
    return {pb.name: (rig.matrix_world @ pb.matrix).copy() for pb in rig.pose.bones}


def uv_zero_faces(me):
    """(faces whose UV area is zero although the face has area in 3D, faces with zero area in 3D too)"""
    uv = me.uv_layers[0].data; bad = 0; degenerate = 0; area = 0.0; mats = {}
    for p in me.polygons:
        q = [uv[li].uv for li in p.loop_indices]
        a = 0.5 * abs(sum(q[i][0] * q[(i + 1) % len(q)][1] - q[(i + 1) % len(q)][0] * q[i][1] for i in range(len(q))))
        if p.area < 1e-8: degenerate += 1          # under 0.01 mm2: a sliver with no visible area
        elif a < 1e-12:
            bad += 1; area += p.area
            nm = me.materials[p.material_index].name if me.materials else "?"; mats[nm] = mats.get(nm, 0) + 1
    return bad, degenerate, area, mats


# ------------------------------------------------------------------ reference 1: master rest shapes
bpy.ops.wm.open_mainfile(filepath=MASTER)
for r in RIGS: bpy.data.objects[r].data.pose_position = 'REST'
bpy.context.view_layer.update()
master_rest = {m: world_mesh(bpy.data.objects[m]) for m in MESHES}

# ------------------------------------------------------------------ reference 2: the working scene
bpy.ops.wm.open_mainfile(filepath=WORK)
sc = bpy.context.scene
clips = json.loads(bpy.data.texts["DR_animations.json"].as_string())
ref_bones = {r: [(b.name, b.parent.name if b.parent else None) for b in bpy.data.objects[r].data.bones] for r in RIGS}
rider = bpy.data.objects["race_rider"]
helmet = {v.index for v in rider.data.vertices if any(g.group == rider.vertex_groups["Helmet"].index for g in v.groups)}
ref_counts = {m: (len(bpy.data.objects[m].data.vertices), len(bpy.data.objects[m].data.polygons)) for m in MESHES}
for r in RIGS: bpy.data.objects[r].data.pose_position = 'REST'
bpy.context.view_layer.update()
work_rest = {m: world_mesh(bpy.data.objects[m]) for m in MESHES}
for r in RIGS: bpy.data.objects[r].data.pose_position = 'POSE'
samples = {}
for c in clips:
    n = c["end"] - c["start"]
    for k in sorted({0, n // 4, n // 2, (3 * n) // 4, n}):
        sc.frame_set(c["start"] + k)
        samples[(c["name"], k)] = dict(bones={r: bone_world(bpy.data.objects[r]) for r in RIGS}, mesh={m: world_mesh(bpy.data.objects[m]) for m in MESHES})

# ------------------------------------------------------------------ the FBX, in an empty scene
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
sc = bpy.context.scene
ob = {o.name: o for o in bpy.data.objects}
meshes = [o for o in bpy.data.objects if o.type == 'MESH']; rigs = [o for o in bpy.data.objects if o.type == 'ARMATURE']
# Blender's FBX importer marks a bone "connected" when its head sits on its parent's tail, and a connected bone
# ignores its position keys. That is an import-side convenience, not something in the file, so undo it before comparing.
for r_ in rigs:
    for o_ in bpy.context.view_layer.objects: o_.select_set(False)
    r_.select_set(True); bpy.context.view_layer.objects.active = r_
    bpy.ops.object.mode_set(mode='EDIT')
    for eb in r_.data.edit_bones: eb.use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
check("two meshes, two rigs", len(meshes) == 2 and len(rigs) == 2 and all(m in ob for m in MESHES) and all(r in ob for r in RIGS), "meshes %s rigs %s" % ([m.name for m in meshes], [r.name for r in rigs]))
for m in MESHES:
    me = ob[m].data
    check("%s vertex/face count" % m, (len(me.vertices), len(me.polygons)) == ref_counts[m], "fbx %d/%d, expected %d/%d" % (len(me.vertices), len(me.polygons), ref_counts[m][0], ref_counts[m][1]))
    z, dg, za, zm = uv_zero_faces(me) if me.uv_layers else (-1, -1, 0, {})
    check("%s UV map present, no zero-area UV faces" % m, len(me.uv_layers) >= 1 and z == 0,
          "uv layers %s; faces with zero UV area but real area in 3D: %d (total 3D area %.4f cm2, materials %s); sliver faces under 0.01 mm2 in 3D (not counted): %d" % ([u.name for u in me.uv_layers], z, za * 1e4, zm, dg))
for r in RIGS:
    got = [(b.name, b.parent.name if b.parent else None) for b in ob[r].data.bones]
    check("%s bones and hierarchy" % r, sorted(got) == sorted(ref_bones[r]), "%d bones (expected %d)%s" % (len(got), len(ref_bones[r]), "" if sorted(got) == sorted(ref_bones[r]) else " differences: %s" % (set(got) ^ set(ref_bones[r]))))
check("rider_rig is its own rig beside bike_rig (its transform is keyed to follow the chassis bone)", ob["rider_rig"].parent is None or ob["rider_rig"].parent.type != 'ARMATURE', "parent %s" % (ob["rider_rig"].parent.name if ob["rider_rig"].parent else None))

# rest shapes
for r in RIGS: ob[r].data.pose_position = 'REST'
bpy.context.view_layer.update()
fb_rest = {m: world_mesh(ob[m]) for m in MESHES}
d = max((a - b).length for a, b in zip(fb_rest["race_bike_youth"], master_rest["race_bike_youth"]))
check("bike rest shape identical to master", d < 2e-6, "max deviation %.6f mm" % (d * 1000))
dv = [(a - b).length for a, b in zip(fb_rest["race_rider"], master_rest["race_rider"])]
changed = {i for i, x in enumerate(dv) if x > 2e-6}
check("rider rest shape identical to master outside the helmet", changed <= helmet, "%d vertices differ from master, %d of them outside the %d helmet vertices; max deviation outside %.6f mm" % (
    len(changed), len(changed - helmet), len(helmet), 1000 * max([x for i, x in enumerate(dv) if i not in helmet] or [0])))
d = max((a - b).length for m in MESHES for a, b in zip(fb_rest[m], work_rest[m]))
check("both rest shapes equal the working scene (helmet corrections baked)", d < 2e-6, "max deviation %.6f mm" % (d * 1000))
for r in RIGS: ob[r].data.pose_position = 'POSE'

# clips
acts = {a.name: a for a in bpy.data.actions}
def find_action(rig, clip):
    for nm in ("%s|%s" % (rig, clip), "%s|%s|%s" % (rig, clip, "BaseLayer"), clip):
        if nm in acts: return acts[nm]
    c = [a for n, a in acts.items() if n.split("|")[0] == rig and clip in n.split("|")]
    return c[0] if c else None
missing = [(r, c["name"]) for c in clips for r in RIGS if find_action(r, c["name"]) is None]
check("15 clips present for both rigs", not missing, "%d actions imported; missing %s" % (len(acts), missing))
bad = []
for c in clips:
    for r in RIGS:
        a = find_action(r, c["name"])
        if a is None: continue
        n = round(a.frame_range[1] - a.frame_range[0])
        if n != c["end"] - c["start"]: bad.append((r, c["name"], n, c["end"] - c["start"]))
check("clip lengths equal DR_animations.json", not bad and not missing, "all %d clip lengths match" % len(clips) if not bad else "mismatches (rig, clip, fbx, expected): %s" % bad)

# animation samples: bone positions, bone rotation relative to the clip's first frame, and the skinned meshes
worst_p = worst_a = worst_m = 0.0; where = {}
for c in clips:
    ok = True
    for r in RIGS:
        a = find_action(r, c["name"])
        if a is None: ok = False; continue
        ad = ob[r].animation_data or ob[r].animation_data_create()
        ad.action = a
        try:
            if a.slots and ad.action_slot is None: ad.action_slot = a.slots[0]
        except Exception: pass
    if not ok: continue
    f0 = min(find_action(r, c["name"]).frame_range[0] for r in RIGS)
    n = c["end"] - c["start"]; first = None
    for k in sorted({0, n // 4, n // 2, (3 * n) // 4, n}):
        sc.frame_set(int(round(f0 + k)))
        ref = samples[(c["name"], k)]
        cur = {r: bone_world(ob[r]) for r in RIGS}
        if first is None: first = (cur, samples[(c["name"], 0)]["bones"])
        for r in RIGS:
            for b, M in cur[r].items():
                p = (M.to_translation() - ref["bones"][r][b].to_translation()).length
                qa = (M.to_quaternion().normalized() @ first[0][r][b].to_quaternion().normalized().inverted()).normalized()
                qb = (ref["bones"][r][b].to_quaternion().normalized() @ first[1][r][b].to_quaternion().normalized().inverted()).normalized()
                dq = qa.rotation_difference(qb).normalized()
                ang = math.degrees(2 * math.acos(max(-1.0, min(1.0, abs(dq.w)))))
                if p > worst_p: worst_p = p; where["pos"] = (c["name"], k, r, b)
                if ang > worst_a: worst_a = ang; where["ang"] = (c["name"], k, r, b)
        for m in MESHES:
            dm = max((x - y).length for x, y in zip(world_mesh(ob[m]), ref["mesh"][m]))
            if dm > worst_m: worst_m = dm; where["mesh"] = (c["name"], k, m)
check("bone positions at 5 frames per clip within 1 mm", worst_p < 0.001, "worst %.4f mm at %s" % (worst_p * 1000, where.get("pos")))
check("bone rotations at 5 frames per clip within 0.5 deg", worst_a < 0.5, "worst %.4f deg at %s" % (worst_a, where.get("ang")))
check("skinned mesh vertices at 5 frames per clip within 1 mm", worst_m < 0.001, "worst %.4f mm at %s" % (worst_m * 1000, where.get("mesh")))

# textures
tex = []
for m in MESHES:
    imgs = {n.image for s in ob[m].material_slots if s.material and s.material.node_tree for n in s.material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image}
    paths = {bpy.path.abspath(i.filepath) for i in imgs}
    tex.append((m, len(ob[m].material_slots), sorted(os.path.basename(p) for p in paths), all(os.path.exists(p) for p in paths) and len(paths) == 1 and all(i.size[0] == 4096 for i in imgs)))
check("textures found by the material import", all(t[3] for t in tex), "; ".join("%s: %d materials -> %s" % (t[0], t[1], t[2]) for t in tex))

# check render (textured)
try:
    a0 = {r: find_action(r, clips[0]["name"]) for r in RIGS}
    for r in RIGS:
        if a0[r]: ob[r].animation_data.action = a0[r]
    sc.frame_set(int(a0["bike_rig"].frame_range[0]) if a0["bike_rig"] else 1)
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND').inputs[0].default_value = (0.75, 0.75, 0.75, 1)
    ld = bpy.data.lights.new("l", 'POINT'); ld.energy = 1000; lo = bpy.data.objects.new("l", ld); sc.collection.objects.link(lo); lo.location = (4.076, 1.005, 5.904)
    cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = 2.4; cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 1600; sc.render.resolution_y = 1100; sc.render.image_settings.file_format = 'PNG'
    for nm, off in (("front_left", Vector((4.24, 4.24, 0))), ("rear_right", Vector((-4.24, -4.24, 0)))):
        t = Vector((0, 0, 0.85)); cam.location = t + off; cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(OUT, "fbx_check_%s.png" % nm); bpy.ops.render.render(write_still=True)
    check("check render of the re-imported FBX written", True, os.path.join(OUT, "fbx_check_front_left.png"))
except Exception as ex:
    check("check render of the re-imported FBX written", False, repr(ex))

# ------------------------------------------------------------------ props
pd = os.path.join(MODELS, "Props")
if os.path.exists(os.path.join(pd, "props_layout.json")):
    lay = json.load(open(os.path.join(pd, "props_layout.json")))
    badp = []
    for nm, p in lay["props"].items():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=os.path.join(pd, p["file"]))
        ms = [o for o in bpy.data.objects if o.type == 'MESH']
        if len(ms) != 1: badp.append((nm, "meshes %d" % len(ms))); continue
        o = ms[0]; bpy.context.view_layer.update()
        bb = [o.matrix_world @ Vector(c) for c in o.bound_box]; dd = [max(c[k] for c in bb) - min(c[k] for c in bb) for k in range(3)]
        tris = sum(len(f.vertices) - 2 for f in o.data.polygons)
        okp = tris == p["triangles"] and abs(dd[0] - p["length"]) < 1e-3 and abs(dd[1] - p["width"]) < 1e-3 and abs(dd[2] - p["height"]) < 1e-3 and len(o.data.uv_layers) == 1 and len(o.data.materials) == len(p["materials"])
        if not okp: badp.append((nm, tris, [round(x, 3) for x in dd]))
    check("9 prop FBX files re-import with expected size, triangles, UV and materials", not badp and len(lay["props"]) == 9, "%d props checked%s" % (len(lay["props"]), "" if not badp else " problems: %s" % badp))

json.dump(results, open(os.path.join(OUT, "verify_report.json"), "w"), indent=1)
fails = sum(1 for r in results if r["result"] == "FAIL")
print("VERIFY SUMMARY %d PASS, %d FAIL" % (len(results) - fails, fails))
sys.exit(1 if fails else 0)
