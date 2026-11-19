import bpy, sys, json
import numpy as np
out = sys.argv[sys.argv.index("--") + 1]
sc = bpy.context.scene; sc.frame_set(1)
ob = bpy.data.objects["race_rider"]; dg = bpy.context.evaluated_depsgraph_get()
ev = ob.evaluated_get(dg); me = ev.to_mesh()
r = {"eval_polys": len(me.polygons), "orig_polys": len(ob.data.polygons), "eval_loops": len(me.loops), "orig_loops": len(ob.data.loops), "zones": {}}
M = ev.matrix_world
for i, s in enumerate(ob.material_slots):
    vs = {v for p in me.polygons if p.material_index == i for v in p.vertices}
    if not vs: continue
    P = np.array([list(M @ me.vertices[v].co) for v in vs])
    r["zones"][s.name] = [P.min(0).round(3).tolist(), P.max(0).round(3).tolist()]
rig = bpy.data.objects["rider_rig"]
r["bones"] = {pb.name: [list((rig.matrix_world @ pb.head).to_tuple(3)), list((rig.matrix_world @ pb.tail).to_tuple(3))] for pb in rig.pose.bones}
ev.to_mesh_clear()
json.dump(r, open(out, "w"), indent=1)
