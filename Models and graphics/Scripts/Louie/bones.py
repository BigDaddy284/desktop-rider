import bpy, sys, json
out = sys.argv[sys.argv.index("--") + 1]
bpy.context.scene.frame_set(1)
r = {}
for rn in ("bike_rig",):
    rig = bpy.data.objects[rn]
    r[rn] = {pb.name: [list((rig.matrix_world @ pb.head).to_tuple(4)), list((rig.matrix_world @ pb.tail).to_tuple(4))] for pb in rig.pose.bones}
    r[rn + "_rest"] = {b.name: [list((rig.matrix_world @ b.head_local).to_tuple(4)), list((rig.matrix_world @ b.tail_local).to_tuple(4))] for b in rig.data.bones}
json.dump(r, open(out, "w"), indent=1)
