"""Louie's bike with the bars turned (read only). -- <out prefix> <angle,...>"""
import bpy, sys, math
from mathutils import Vector, Quaternion
a = sys.argv[sys.argv.index("--") + 1:]
pre, angles = a[0], [float(x) for x in a[1].split(",")]
sc = bpy.context.scene; sc.frame_set(1)
rig = bpy.data.objects["bike_rig"]
if rig.animation_data: rig.animation_data.action = None
bpy.data.objects["race_rider"].hide_render = True
sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 800; sc.render.resolution_y = 800; sc.render.film_transparent = False
sc.view_settings.view_transform = 'Standard'
cam = bpy.data.objects["Camera"]; sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.55
for o in [o for o in sc.objects if o.type == 'LIGHT']: o.hide_render = True
sun = bpy.data.objects.new("S", bpy.data.lights.new("S", 'SUN')); sc.collection.objects.link(sun); sun.data.energy = 3
w = sc.world; w.node_tree.nodes["Background"].inputs[0].default_value = (1, 1, 1, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.35
pb = rig.pose.bones["steer"]; pb.rotation_mode = "QUATERNION"
ctr = Vector(tuple(float(x) for x in a[2].split(","))) if len(a) > 2 else Vector((0.26, 0.0, 0.68))
if len(a) > 3: cam.data.ortho_scale = float(a[3])
VIEWS = [(v.split(":")[0], tuple(float(t) for t in v.split(":")[1].split(","))) for v in a[4:]] or [("top", (-0.35, 0, 1)), ("q", (-0.5, -1, 0.7))]
for ang in angles:
    pb.rotation_quaternion = Quaternion((0, 1, 0), math.radians(ang)); bpy.context.view_layer.update()
    for name, d in VIEWS:
        d = Vector(d).normalized(); cam.location = ctr + d * 4
        cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sun.rotation_euler = cam.rotation_euler.copy(); sun.rotation_euler.x -= 0.6; sun.rotation_euler.z += 0.5
        sc.render.filepath = "%s_%s_%d.png" % (pre, name, ang); bpy.ops.render.render(write_still=True)
