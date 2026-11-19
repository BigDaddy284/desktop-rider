
import bpy, sys, math
from mathutils import Matrix, Vector
ang=float(sys.argv[sys.argv.index("--")+1]); out=sys.argv[sys.argv.index("--")+2]
rig=bpy.data.objects["bike_rig"]
if rig.animation_data: rig.animation_data.action=None; print("CLEARED", [ (tr.name,len(tr.strips)) for tr in rig.animation_data.nla_tracks]); [setattr(tr,"mute",True) for tr in rig.animation_data.nla_tracks]
pb=rig.pose.bones["swingarm"]
pv=rig.matrix_world.inverted() @ Vector((0.034,0,0.361))
ax=(rig.matrix_world.inverted().to_3x3() @ Vector((0,1,0))).normalized()
M=Matrix.Translation(pv) @ Matrix.Rotation(math.radians(ang),4,ax) @ Matrix.Translation(-pv)
pb.matrix = M @ pb.bone.matrix_local
bpy.context.view_layer.update(); print("TAIL", tuple(round(c,4) for c in rig.matrix_world @ pb.tail)); print("HEAD", tuple(round(c,4) for c in rig.matrix_world @ pb.bone.head_local))
bpy.ops.wm.save_as_mainfile(filepath=out, copy=True)
