"""Close-up renders of the bike (read only). -- <out png prefix> <atlas png or 'keep'> <cx> <cz> <ortho scale> [hide_rider]"""
import bpy, sys, os, math
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
pre, atlas, cx, cz, osc = a[0], a[1], float(a[2]), float(a[3]), float(a[4]); hide = "hide_rider" in a
sc = bpy.context.scene; sc.frame_set(1)
bike = bpy.data.objects["race_bike_youth"]
if atlas != "keep":
    im = bpy.data.images.load(atlas)
    for s in bike.material_slots:
        for n in s.material.node_tree.nodes:
            if n.type == 'TEX_IMAGE': n.image = im
if hide: bpy.data.objects["race_rider"].hide_render = True
sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 1400; sc.render.resolution_y = 1000; sc.render.film_transparent = True
sc.view_settings.view_transform = 'Standard'
cam = bpy.data.objects["Camera"]; sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = osc
for o in [o for o in sc.objects if o.type == 'LIGHT']: o.hide_render = True
sun = bpy.data.objects.new("S", bpy.data.lights.new("S", 'SUN')); sc.collection.objects.link(sun); sun.data.energy = 3
w = sc.world; w.node_tree.nodes["Background"].inputs[0].default_value = (1, 1, 1, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.35
ctr = Vector((cx, 0, cz))
for name, d in {"right": (0, -1, 0), "left": (0, 1, 0), "top": (0.001, 0, 1), "front34r": (0.7, -1, 0.3), "rear34r": (-0.8, -1, 0.3)}.items():
    d = Vector(d).normalized(); cam.location = ctr + d * 4
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sun.rotation_euler = cam.rotation_euler.copy(); sun.rotation_euler.x -= 0.6; sun.rotation_euler.z += 0.5
    sc.render.filepath = pre + "_" + name + ".png"; bpy.ops.render.render(write_still=True)
