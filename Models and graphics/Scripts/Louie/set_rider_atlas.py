"""Swap the rider's kit texture for another atlas file (packed into the .blend) and save it.  -- <atlas png>"""
import bpy, sys, os
p = sys.argv[sys.argv.index("--") + 1]
old = [im for im in bpy.data.images if "DR2_rider_atlas_kit" in im.name or (im.filepath and "DR2_rider_atlas_kit" in os.path.basename(im.filepath))]
print("RIDER old", [(im.name, im.filepath, bool(im.packed_file)) for im in old])
new = bpy.data.images.load(p); new.pack()
n = 0
for m in bpy.data.materials:
    if not m.node_tree: continue
    for nd in m.node_tree.nodes:
        if nd.type == 'TEX_IMAGE' and nd.image in old: nd.image = new; n += 1
for im in old:
    if im.users == 0: bpy.data.images.remove(im)
new.name = os.path.splitext(os.path.basename(p))[0]
print("RIDER texture nodes swapped", n); assert n >= 1
bpy.ops.wm.save_mainfile(compress=True); print("SAVED")
