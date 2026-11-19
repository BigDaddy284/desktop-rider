"""Lists the bike's loose parts (connected pieces) with material, rest-pose world bbox and main vertex group (read only)."""
import bpy, sys, json, bmesh
from collections import Counter
out = sys.argv[sys.argv.index("--") + 1]
sc = bpy.context.scene; sc.frame_set(1)
ob = bpy.data.objects["race_bike_youth"]; me = ob.data; M = ob.matrix_world
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
seen = set(); parts = []
gnames = [g.name for g in ob.vertex_groups]
for v in bm.verts:
    if v.index in seen: continue
    stack = [v]; comp = []; seen.add(v.index)
    while stack:
        x = stack.pop(); comp.append(x.index)
        for e in x.link_edges:
            o = e.other_vert(x)
            if o.index not in seen: seen.add(o.index); stack.append(o)
    cs = set(comp)
    faces = [f for f in bm.faces if f.verts[0].index in cs]
    mats = Counter(ob.material_slots[f.material_index].name for f in faces)
    P = [M @ me.vertices[i].co for i in comp]
    lo = [round(min(p[k] for p in P), 3) for k in range(3)]; hi = [round(max(p[k] for p in P), 3) for k in range(3)]
    grp = Counter(gnames[max(me.vertices[i].groups, key=lambda g: g.weight).group] if me.vertices[i].groups else "-" for i in comp)
    parts.append(dict(id=len(parts), verts=len(comp), faces=len(faces), mats=dict(mats), lo=lo, hi=hi, group=dict(grp)))
json.dump(dict(scale=list(ob.scale), loc=list(ob.location), parts=parts), open(out, "w"), indent=0)
print("PARTS", len(parts))
