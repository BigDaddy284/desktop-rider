"""Puts a new atlas on the rider or bike of desktop_rider_2.blend, renders previews, and saves only when asked.
    -- <object> <atlas png> <preview dir> <prefix> [save]"""
import bpy, sys, os
a = sys.argv[sys.argv.index("--") + 1:]
obname, png, outdir, pre = a[:4]; save = "save" in a[4:]
ob = bpy.data.objects[obname]
old = {n.image for s in ob.material_slots for n in s.material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image}
stem = os.path.splitext(os.path.basename(png))[0]
im = bpy.data.images.get(stem) or bpy.data.images.load(png); im.name = stem; im.filepath = png; im.reload()
for s in ob.material_slots:
    for n in s.material.node_tree.nodes:
        if n.type == 'TEX_IMAGE': n.image = im
if save:
    im.pack()
    for o in old:
        if o != im and o.users == 0: bpy.data.images.remove(o)
    bpy.ops.wm.save_mainfile(compress=True)
    print("SAVED with", stem)
sys.argv = [sys.argv[0], "--", outdir, pre]
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "render_preview.py")).read())
