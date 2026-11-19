import bpy, json, sys
from collections import Counter
ob = bpy.data.objects["race_bike_youth"]; MW = ob.matrix_world; me = ob.data
names = [s.name for s in ob.material_slots]
lo, hi = (-0.30, -0.07, 0.30), (-0.05, 0.07, 0.66)
res = {}
for p in me.polygons:
    P = [MW @ me.vertices[i].co for i in p.vertices]
    if all(lo[k] <= q[k] <= hi[k] for q in P for k in range(3)):
        res.setdefault(names[p.material_index], []).append([[round(c, 4) for c in q] for q in P])
json.dump(res, open(sys.argv[sys.argv.index("--") + 1], "w"))
print("PROBE", {k: len(v) for k, v in res.items()})
