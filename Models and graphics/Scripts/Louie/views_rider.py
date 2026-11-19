"""Close-up renders around a point from named directions (read only, rider shown).
   -- <out prefix> <cx> <cy> <cz> <ortho scale> name:dx,dy,dz [name:dx,dy,dz ...]"""
import bpy, sys
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
pre, cx, cy, cz, osc = a[0], float(a[1]), float(a[2]), float(a[3]), float(a[4])
views = [(v.split(":")[0], tuple(float(t) for t in v.split(":")[1].split(","))) for v in a[5:]]
sc = bpy.context.scene; sc.frame_set(1)
sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 1400; sc.render.resolution_y = 1000; sc.render.film_transparent = True
sc.view_settings.view_transform = 'Standard'
cam = bpy.data.objects["Camera"]; sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = osc
for o in [o for o in sc.objects if o.type == 'LIGHT']: o.hide_render = True
sun = bpy.data.objects.new("S", bpy.data.lights.new("S", 'SUN')); sc.collection.objects.link(sun); sun.data.energy = 3
w = sc.world; w.node_tree.nodes["Background"].inputs[0].default_value = (1, 1, 1, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.35
ctr = Vector((cx, cy, cz))
for name, d in views:
    d = Vector(d).normalized(); cam.location = ctr + d * 4
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sun.rotation_euler = cam.rotation_euler.copy(); sun.rotation_euler.x -= 0.6; sun.rotation_euler.z += 0.5
    sc.render.filepath = pre + "_" + name + ".png"; bpy.ops.render.render(write_still=True)
