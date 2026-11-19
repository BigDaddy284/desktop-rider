import bpy, json
ob = bpy.data.objects["race_bike_youth"]; MW = ob.matrix_world; me = ob.data
names = [s.name for s in ob.material_slots]
mi = names.index("DR_Swingarm_UV")
pts = set()
for p in me.polygons:
    if p.material_index != mi: continue
    for i in p.vertices:
        q = MW @ me.vertices[i].co
        if -0.35 < q.x < -0.05: pts.add((round(q.x, 4), round(q.y, 4), round(q.z, 4)))
pts = sorted(pts)
print("ARM", json.dumps(pts))
