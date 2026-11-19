"""Renders preview stills of the model in its rest pose (read only, nothing saved). -- <out dir> <prefix>"""
import bpy, sys, os, math
from mathutils import Vector
OUT, PRE = sys.argv[sys.argv.index("--") + 1:][:2]
os.makedirs(OUT, exist_ok=True)
sc = bpy.context.scene; sc.frame_set(1)
sc.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE_NEXT'
sc.render.resolution_x = 1200; sc.render.resolution_y = 900; sc.render.film_transparent = True
sc.view_settings.view_transform = 'Standard'
bike = bpy.data.objects["race_bike_youth"]; rider = bpy.data.objects["race_rider"]
dg = bpy.context.evaluated_depsgraph_get()
pts = []
for ob in (bike, rider):
    ev = ob.evaluated_get(dg); pts += [ev.matrix_world @ Vector(c) for c in ev.bound_box]
lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
ctr = (lo + hi) / 2; size = (hi - lo).length
cam = bpy.data.objects.get("Camera") or bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
if cam.name not in sc.objects: sc.collection.objects.link(cam)
sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = size * 1.05
for o in [o for o in sc.objects if o.type == 'LIGHT']: o.hide_render = True
sun = bpy.data.objects.new("PreviewSun", bpy.data.lights.new("PreviewSun", 'SUN')); sc.collection.objects.link(sun)
sun.data.energy = 3.0; sun.rotation_euler = (math.radians(50), 0, math.radians(30))
w = sc.world or bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
bg = w.node_tree.nodes.get("Background"); bg.inputs[0].default_value = (1, 1, 1, 1); bg.inputs[1].default_value = 0.6
views = {"left": (0, 1, 0.15), "right": (0, -1, 0.15), "front34": (1, -0.8, 0.35), "rear34": (-1, 0.8, 0.35)}
for name, d in views.items():
    d = Vector(d).normalized(); cam.location = ctr + d * size * 2
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, "%s_%s.png" % (PRE, name)); bpy.ops.render.render(write_still=True)
print("PREVIEW done")
