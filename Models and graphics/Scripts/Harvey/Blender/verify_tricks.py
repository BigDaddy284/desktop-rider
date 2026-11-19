"""Checks DesktopRider_Tricks.fbx against the trick animations keyed fresh on an in-memory copy of the working .blend.

    blender --background --factory-startup --python verify_tricks.py -- <rigs-only fbx> <same export with meshes fbx> <working .blend> <Tools/Blender dir> <side> <out dir>

Nothing is saved. The working .blend is opened, the tricks are keyed in memory (as the export did) and sampled; then
each FBX is imported into an empty scene and compared.
"""
import bpy, sys, os, json, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
FBX_RIGS, FBX_FULL, WORK, TOOLS, SIDE, OUT = argv[:6]
os.makedirs(OUT, exist_ok=True)
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


def ks(n):
    return sorted(set(range(0, n + 1, 4)) | {n})       # every 4th frame of every clip, plus the last


# ------------------------------------------------------------------ reference: key the tricks in memory and sample
bpy.ops.wm.open_mainfile(filepath=WORK)
sys.path.insert(0, TOOLS)
import trick_animations as TA
table = TA.build(SIDE)
sc = bpy.context.scene
ref_bones = {r: [(b.name, b.parent.name if b.parent else None) for b in bpy.data.objects[r].data.bones] for r in RIGS}
samples = {}
for t in table:
    for k in ks(t["frames"]):
        sc.frame_set(t["start"] + k)
        samples[(t["name"], k)] = dict(bones={r: bone_world(bpy.data.objects[r]) for r in RIGS}, mesh={m: world_mesh(bpy.data.objects[m]) for m in MESHES})
check("reference: 18 trick animations keyed in memory", len(table) == 18, "%d clips, frames %d-%d, side %s" % (len(table), table[0]["start"], table[-1]["end"], SIDE))


def load(fbx):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    ob = {o.name: o for o in bpy.data.objects}
    for r_ in [o for o in bpy.data.objects if o.type == 'ARMATURE']:
        for o_ in bpy.context.view_layer.objects: o_.select_set(False)
        r_.select_set(True); bpy.context.view_layer.objects.active = r_
        bpy.ops.object.mode_set(mode='EDIT')
        for eb in r_.data.edit_bones: eb.use_connect = False        # see verify_models.py: importer-side "connected" flag
        bpy.ops.object.mode_set(mode='OBJECT')
    return ob, {a.name: a for a in bpy.data.actions}


def run(tag, fbx, with_mesh):
    ob, acts = load(fbx)
    sc = bpy.context.scene
    def find(rig, clip):
        return acts.get("%s|%s" % (rig, clip)) or next((a for n, a in acts.items() if n.split("|")[0] == rig and clip in n.split("|")), None)
    rigs = [o for o in bpy.data.objects if o.type == 'ARMATURE']; meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    check("%s: objects" % tag, len(rigs) == 2 and len(meshes) == (2 if with_mesh else 0), "rigs %s meshes %s" % (sorted(r.name for r in rigs), sorted(m.name for m in meshes)))
    for r in RIGS:
        got = [(b.name, b.parent.name if b.parent else None) for b in ob[r].data.bones]
        check("%s: %s bones and hierarchy" % (tag, r), sorted(got) == sorted(ref_bones[r]), "%d bones (expected %d)" % (len(got), len(ref_bones[r])))
    missing = [(r, t["name"]) for t in table for r in RIGS if find(r, t["name"]) is None]
    check("%s: 18 clips present for both rigs" % tag, not missing, "%d actions imported; missing %s" % (len(acts), missing))
    bad = [(r, t["name"], round(find(r, t["name"]).frame_range[1] - find(r, t["name"]).frame_range[0]), t["frames"]) for t in table for r in RIGS
           if find(r, t["name"]) is not None and round(find(r, t["name"]).frame_range[1] - find(r, t["name"]).frame_range[0]) != t["frames"]]
    check("%s: clip lengths equal the script's table" % tag, not bad and not missing, "all %d match" % len(table) if not bad else "mismatches %s" % bad)
    wp = wa = wm = 0.0; where = {}; big_rot = 0.0
    for t in table:
        acts_ = {r: find(r, t["name"]) for r in RIGS}
        if None in acts_.values(): continue
        for r in RIGS:
            ad = ob[r].animation_data or ob[r].animation_data_create(); ad.action = acts_[r]
        f0 = min(a.frame_range[0] for a in acts_.values()); first = None
        for k in ks(t["frames"]):
            sc.frame_set(int(round(f0 + k)))
            ref = samples[(t["name"], k)]; cur = {r: bone_world(ob[r]) for r in RIGS}
            if first is None: first = (cur, samples[(t["name"], 0)]["bones"])
            for r in RIGS:
                for b, M in cur[r].items():
                    p = (M.to_translation() - ref["bones"][r][b].to_translation()).length
                    qa = (M.to_quaternion().normalized() @ first[0][r][b].to_quaternion().normalized().inverted()).normalized()
                    qb = (ref["bones"][r][b].to_quaternion().normalized() @ first[1][r][b].to_quaternion().normalized().inverted()).normalized()
                    ang = math.degrees(2 * math.acos(max(-1.0, min(1.0, abs(qa.rotation_difference(qb).normalized().w)))))
                    big_rot = max(big_rot, math.degrees(2 * math.acos(max(-1.0, min(1.0, abs(qb.w))))))
                    if p > wp: wp = p; where["pos"] = (t["name"], k, r, b)
                    if ang > wa: wa = ang; where["ang"] = (t["name"], k, r, b)
            if with_mesh:
                for m in MESHES:
                    dm = max((x - y).length for x, y in zip(world_mesh(ob[m]), ref["mesh"][m]))
                    if dm > wm: wm = dm; where["mesh"] = (t["name"], k, m)
    nsmp = sum(len(ks(t["frames"])) for t in table)
    check("%s: bone positions at every 4th frame of every clip within 1 mm" % tag, wp < 0.001, "%d frames sampled; worst %.4f mm at %s" % (nsmp, wp * 1000, where.get("pos")))
    check("%s: bone rotations at every 4th frame of every clip within 0.5 deg" % tag, wa < 0.5, "worst %.4f deg at %s (largest rotation actually present in the samples: %.1f deg)" % (wa, where.get("ang"), big_rot))
    if with_mesh:
        check("%s: skinned mesh vertices at every 4th frame of every clip within 1 mm" % tag, wm < 0.001, "worst %.4f mm at %s" % (wm * 1000, where.get("mesh")))
    # curve fingerprint, to show the rigs-only file carries the same animation as the checked full one
    curves = {}
    for n in sorted(acts):
        if n.split("|")[0] not in RIGS: continue
        for layer in acts[n].layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for fc in bag.fcurves:
                        curves[(n, fc.data_path, fc.array_index)] = [kp.co[1] for kp in fc.keyframe_points]
    return curves


c_full = run("full export (with meshes, scratch copy)", FBX_FULL, True)
c_rigs = run("DesktopRider_Tricks.fbx (rigs only)", FBX_RIGS, False)
# Note: the two files are separate export runs and their local curve values are not byte-identical (a rotation can be
# written as slightly different local values that give the same world result), so the files are compared the way that
# matters: world-space bone positions and rotations against the freshly keyed reference, above.
check("both files have the same set of animation curves", set(c_full) == set(c_rigs), "%d curves in each" % len(c_rigs))
json.dump(results, open(os.path.join(OUT, "verify_tricks_report.json"), "w"), indent=1)
print("VERIFY SUMMARY %d PASS, %d FAIL" % (sum(1 for r in results if r["result"] == "PASS"), sum(1 for r in results if r["result"] == "FAIL")))
sys.exit(1 if any(r["result"] == "FAIL" for r in results) else 0)
