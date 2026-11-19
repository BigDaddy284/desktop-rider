"""Writes a fingerprint of the rider/bike geometry, UVs, weights and rigs of a .blend (read only), for comparison."""
import bpy, sys, json, hashlib
import numpy as np
out = sys.argv[sys.argv.index("--") + 1]
def h(a): return hashlib.md5(np.ascontiguousarray(a).tobytes()).hexdigest()
r = {}
for n in ("race_bike_youth", "race_rider"):
    ob = bpy.data.objects[n]; me = ob.data
    co = np.zeros(len(me.vertices) * 3, np.float32); me.vertices.foreach_get("co", co)
    uv = np.zeros(len(me.loops) * 2, np.float32); me.uv_layers[0].data.foreach_get("uv", uv)
    w = sorted((v.index, g.group, round(g.weight, 6)) for v in me.vertices for g in v.groups)
    mi = np.zeros(len(me.polygons), np.int32); me.polygons.foreach_get("material_index", mi)
    r[n] = dict(verts=len(me.vertices), faces=len(me.polygons), co=h(co), uv=h(uv), weights=h(np.array(w, np.float64)),
                groups=[g.name for g in ob.vertex_groups], mat_index=h(mi), slots=[s.name for s in ob.material_slots],
                modifiers=[m.type + ":" + m.name for m in ob.modifiers], parent=ob.parent.name if ob.parent else None,
                matrix=[round(x, 6) for row in ob.matrix_world for x in row],
                atlas=[nd.image.name for s in ob.material_slots for nd in s.material.node_tree.nodes if nd.type == 'TEX_IMAGE' and nd.image][:1])
for n in ("bike_rig", "rider_rig"):
    rig = bpy.data.objects[n]
    r[n] = dict(bones={b.name: [round(x, 6) for row in b.matrix_local for x in row] for b in rig.data.bones},
                parents={b.name: b.parent.name if b.parent else None for b in rig.data.bones},
                constraints={pb.name: [c.type for c in pb.constraints] for pb in rig.pose.bones if pb.constraints},
                matrix=[round(x, 6) for row in rig.matrix_world for x in row])
r["animations_json"] = hashlib.md5(bpy.data.texts["DR_animations.json"].as_string().encode()).hexdigest()
r["actions"] = sorted(a.name for a in bpy.data.actions)
json.dump(r, open(out, "w"), indent=1)
