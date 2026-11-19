import bpy, bmesh, json, sys
from mathutils import Vector
ob = bpy.data.objects["race_bike_youth"]; MW = ob.matrix_world
names = [s.name for s in ob.material_slots]
me = ob.data
out = {}
def grab(mname, lo, hi):
    mi = names.index(mname); res = []
    for p in me.polygons:
        if p.material_index != mi: continue
        P = [MW @ me.vertices[i].co for i in p.vertices]
        if all(lo[k] <= q[k] <= hi[k] for q in P for k in range(3)):
            res.append([[round(c, 5) for c in q] for q in P])
    return res
out["shock"] = grab("Trim_3b3c45_UV", (-0.27, -0.025, 0.31), (-0.09, 0.025, 0.64))
out["mount"] = grab("Trim_b1aeaf_UV", (-0.20, -0.06, 0.345), (-0.14, 0.06, 0.40))
json.dump(out, open(sys.argv[sys.argv.index("--") + 1], "w"))
print("PROBE", len(out["shock"]), len(out["mount"]))
