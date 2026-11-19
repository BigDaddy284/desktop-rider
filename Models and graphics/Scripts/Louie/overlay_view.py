"""Side-on ortho render framed to match a reference photo pixel grid (read only).
    -- <out png> <side R|L> <photo w> <photo h> <m per photo px> <peg px x> <peg px y> <k upscale>
The model's right footpeg centre (x 0.017, z 0.298) lands on the photo's peg pixel."""
import bpy, sys, math
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
out, side = a[0], a[1]; W, H, s, px, py, k = int(a[2]), int(a[3]), float(a[4]), float(a[5]), float(a[6]), int(a[7])
sc = bpy.context.scene; sc.frame_set(1)
bpy.data.objects["race_rider"].hide_render = True
sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = W * k; sc.render.resolution_y = H * k; sc.render.film_transparent = True
sc.view_settings.view_transform = 'Standard'
cam = bpy.data.objects["Camera"]; sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = max(W, H) * s
cam.data.shift_x = cam.data.shift_y = 0
PEG = Vector((0.017, 0.298))
cxp, cyp = W / 2, H / 2
if side == "R":      # camera on -Y looking +Y: image right = +X
    cx = PEG.x + (cxp - px) * s; loc = Vector((cx, -4, 0)); rot = (math.radians(90), 0, 0)
else:                # camera on +Y looking -Y: image right = -X
    cx = PEG.x - (cxp - px) * s; loc = Vector((cx, 4, 0)); rot = (math.radians(90), 0, math.radians(180))
cz = PEG.y + (py - cyp) * s
loc.z = cz; cam.location = loc; cam.rotation_euler = rot
for o in [o for o in sc.objects if o.type == 'LIGHT']: o.hide_render = True
sun = bpy.data.objects.new("S", bpy.data.lights.new("S", 'SUN')); sc.collection.objects.link(sun); sun.data.energy = 3
sun.rotation_euler = (math.radians(60), 0, math.radians(20 if side == "R" else 200))
sc.world.node_tree.nodes["Background"].inputs[1].default_value = 0.4
sc.render.filepath = out; bpy.ops.render.render(write_still=True)
