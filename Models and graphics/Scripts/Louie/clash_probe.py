import bpy, json, sys
ob = bpy.data.objects["race_bike_youth"]; MW = ob.matrix_world; me = ob.data
names = [s.name for s in ob.material_slots]
res = {}
for p in me.polygons:
    if names[p.material_index] == "DR2_Electric_UV": continue
    P = [MW @ me.vertices[i].co for i in p.vertices]
    c = sum(P, P[0] * 0) / len(P)
    # along the new shock axis, inside its radius (0.04) + a little
    import mathutils
    B = mathutils.Vector((-0.195, 0.331)); T = mathutils.Vector((-0.100, 0.598)); a = (T - B); L = a.length; a /= L; n = mathutils.Vector((-a.y, a.x))
    for q in P:
        d = mathutils.Vector((q.x - B.x, q.z - B.y)); t = d.dot(a) / L
        if 0.05 < t < 0.95 and abs(d.dot(n)) < 0.04 and abs(q.y) < 0.045:
            res.setdefault(names[p.material_index], []).append([round(c.x, 3), round(c.y, 3), round(c.z, 3)]); break
print("CLASH", json.dumps({k: (len(v), v[:4]) for k, v in res.items()}))
