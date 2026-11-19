# Louie: does the front end hit anything when the bars are turned? Run in background on desktop_rider_2.blend.
#   -- <angle,angle,...> [saveas=<path> pose=<angle>]
import bpy, sys, json, math
from mathutils import Quaternion
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
angles = [float(x) for x in a[0].split(",")] if a else [0, 28, -28, 40, -40]
opt = dict(x.split("=", 1) for x in a[1:] if "=" in x)
rig = bpy.data.objects["bike_rig"]; ob = bpy.data.objects["race_bike_youth"]
STEER = {"steer", "fork_lower", "front_wheel"}
gi = {g.index: g.name for g in ob.vertex_groups}
me0 = ob.data; mats = [s.name for s in ob.material_slots]
def moving(v):
    w = {gi[g.group]: g.weight for g in v.groups if g.group in gi}
    return sum(x for k, x in w.items() if k in STEER) > 0.5
mv = [moving(v) for v in me0.vertices]
if "shift" in opt:   # move the steering axis back by dx (world x), e.g. onto the head tube's centre line
    from mathutils import Vector
    bpy.context.view_layer.objects.active = rig; bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones["steer"]; IM = rig.matrix_world.inverted().to_3x3(); dv = IM @ Vector((float(opt["shift"]), 0, 0))
    eb.head += dv; eb.tail += dv
    bpy.ops.object.mode_set(mode="OBJECT")
pb = rig.pose.bones["steer"]; pb.rotation_mode = "QUATERNION"
def trees(deg):
    pb.rotation_quaternion = Quaternion((0, 1, 0), math.radians(deg))
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get(); e = ob.evaluated_get(dg); m = e.to_mesh()
    V = [e.matrix_world @ v.co for v in m.vertices]
    A, B, ia, ib = [], [], [], []
    for p in m.polygons:
        k = sum(mv[i] for i in p.vertices) / len(p.vertices)
        (A if k > 0.5 else B).append(list(p.vertices)); (ia if k > 0.5 else ib).append(p.index)
    ta = BVHTree.FromPolygons(V, A, all_triangles=False, epsilon=0.0)
    tb = BVHTree.FromPolygons(V, B, all_triangles=False, epsilon=0.0)
    pairs = {(ia[x], ib[y]) for x, y in ta.overlap(tb)}
    info = {pi: (mats[m.polygons[pi].material_index], tuple(round(c, 3) for c in e.matrix_world @ m.polygons[pi].center)) for pr in pairs for pi in pr}
    e.to_mesh_clear(); return pairs, info
base, _ = trees(0)
out = {"rest_pairs": len(base)}
for d in angles:
    pr, info = trees(d); new = pr - base
    summ = {}
    for x, y in new:
        k = info[x][0] + " -> " + info[y][0]; s = summ.setdefault(k, [0, info[y][1]]); s[0] += 1
    out[str(d)] = {"pairs": len(pr), "new": len(new), "by_part": summ}
print("STEER", json.dumps(out))
if "saveas" in opt:
    pb.rotation_quaternion = Quaternion((0, 1, 0), math.radians(float(opt.get("pose", 40))))
    bpy.ops.wm.save_as_mainfile(filepath=opt["saveas"], copy=True); print("SAVED copy", opt["saveas"])
if opt.get("detail"):
    import bmesh
    d = float(opt["detail"]); pr, info = trees(d); new = pr - base
    hitB = {y for x, y in new if info[y][0] == "DR2_Electric_UV"}; hitA = {x for x, y in new if info[y][0] == "DR2_Electric_UV"}
    bm = bmesh.new(); bm.from_mesh(me0); bm.faces.ensure_lookup_table()
    def islands(seed):
        seen, out = set(), []
        for s in seed:
            if s in seen: continue
            st = [bm.faces[s]]; isl = set()
            while st:
                f = st.pop()
                if f.index in isl: continue
                isl.add(f.index)
                for e in f.edges:
                    for g in e.link_faces:
                        if g.index not in isl and g.material_index == f.material_index: st.append(g)
            seen |= isl
            vs = [ob.matrix_world @ v.co for i in isl for v in bm.faces[i].verts]
            out.append({"faces": len(isl), "hit": len(isl & seed), "mat": mats[bm.faces[s].material_index],
                        "min": [round(min(v[k] for v in vs), 3) for k in range(3)], "max": [round(max(v[k] for v in vs), 3) for k in range(3)]})
        return out
    print("DETAIL", json.dumps({"static": islands(hitB), "moving": islands(hitA)}))
