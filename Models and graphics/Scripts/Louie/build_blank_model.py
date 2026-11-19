"""Builds the second rider + bike as a copy of Harvey's working .blend with a blank (neutral) colour scheme.

    blender --background <Harvey desktop_rider_3d.blend> --python build_blank_model.py -- <out .blend> <textures dir>

Harvey's .blend is only read. The copy keeps every object name, mesh, UV, rig, bone, vertex group, modifier and the
DR_animations.json clip list exactly as they are, so the existing exporter and trick scripts work on it unchanged.
Only the two texture atlases are swapped for new blank ones:
  * every material zone is filled with one flat neutral tone (no graphics, logos, names or numbers);
  * a zone guide (each zone a distinct colour) is saved beside it, so zones can be named when choosing colours.
"""
import bpy, sys, os, json
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
OUT_BLEND, TEX_DIR = argv[:2]
os.makedirs(TEX_DIR, exist_ok=True)
SIZE = 4096
PAD = 8   # pixels of edge padding around each UV island, against seams when mip-mapped

def hexrgb(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))

NEUTRAL = hexrgb("d9d9d9")   # the blank canvas tone for everything that will get a colour
# zones that stay a functional neutral, darker tone (rubber, lenses, metal) so the shapes still read
RIDER_TONES = {
    "Visor_UV": "2b2b2b", "Trim_lens_UV": "3a3a3a", "Trim_sole_UV": "2f2f2f", "Trim_buckle_UV": "9a9a9a",
    "Trim_helmet_dark_UV": "4a4a4a", "DR_GoggleFrame_UV": "5a5a5a", "Trim_neck_UV": "5a5a5a",
}
BIKE_TONES = {
    "Trim_3b3c45_UV": "3b3b3b", "Trim_4b4c57_UV": "4c4c4c", "Trim_918e93_UV": "8f8f8f", "Trim_a39997_UV": "9b9b9b",
    "Trim_b1aeaf_UV": "afafaf", "Trim_exhaust_UV": "a8a8a8", "Trim_radiator_UV": "5c5c5c", "DR_Grip_UV": "2f2f2f",
    "DR_BarBlack_UV": "333333", "DR_Silencer_UV": "b5b5b5", "DR_Ignition_UV": "3a3a3a",
    "Trim_rim_blue_UV": "7a7a7a", "Trim_fork_gold_UV": "8a8a8a", "DR_HubGold_UV": "8a8a8a",
    "DR_PegBlue_UV": "7a7a7a", "DR_HeadGold_UV": "8a8a8a", "Trim_ecbe2a_UV": "8a8a8a",
}
GUIDE = ["e6194b", "3cb44b", "ffe119", "4363d8", "f58231", "911eb4", "46f0f0", "f032e6", "bcf60c", "fabebe",
         "008080", "e6beff", "9a6324", "fffac8", "800000", "aaffc3", "808000", "ffd8b1", "000075", "808080",
         "ffffff", "000000", "a9a9a9"]


def raster(ob, tones, guide):
    """Paint each polygon's UV triangles with its material's tone. Returns (colour img, guide img, zone report)."""
    me = ob.data; uv = me.uv_layers[0].data
    img = np.zeros((SIZE, SIZE, 3), np.float32); gimg = np.zeros((SIZE, SIZE, 3), np.float32)
    mask = np.zeros((SIZE, SIZE), bool)
    report = {}
    for p in me.polygons:
        mname = ob.material_slots[p.material_index].name
        col = np.array(hexrgb(tones.get(mname, "d9d9d9")), np.float32)
        gi = list(ob.material_slots.keys()).index(mname) % len(GUIDE)
        gcol = np.array(hexrgb(GUIDE[gi]), np.float32)
        report.setdefault(mname, {"tone": "#" + tones.get(mname, "d9d9d9"), "guide": "#" + GUIDE[gi], "faces": 0})
        report[mname]["faces"] += 1
        q = [uv[li].uv * SIZE for li in p.loop_indices]
        for k in range(1, len(q) - 1):
            a, b, c = q[0], q[k], q[k + 1]
            x0 = max(int(min(a.x, b.x, c.x)) - 1, 0); x1 = min(int(max(a.x, b.x, c.x)) + 2, SIZE)
            y0 = max(int(min(a.y, b.y, c.y)) - 1, 0); y1 = min(int(max(a.y, b.y, c.y)) + 2, SIZE)
            if x1 <= x0 or y1 <= y0: continue
            xs, ys = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
            d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y)
            if abs(d) < 1e-12:
                continue
            l1 = ((b.y - c.y) * (xs - c.x) + (c.x - b.x) * (ys - c.y)) / d
            l2 = ((c.y - a.y) * (xs - c.x) + (a.x - c.x) * (ys - c.y)) / d
            l3 = 1 - l1 - l2
            e = -0.75 / max(abs(d), 1)  # tiny tolerance so thin slivers still get pixels
            inside = (l1 >= e) & (l2 >= e) & (l3 >= e)
            sub = (slice(y0, y1), slice(x0, x1))
            img[sub][inside] = col; gimg[sub][inside] = gcol; mask[sub] |= inside
    # edge padding: grow each island outwards by PAD pixels
    for _ in range(PAD):
        grow = np.zeros_like(mask); src = np.zeros_like(img); gsrc = np.zeros_like(gimg)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            m = np.roll(mask, (dy, dx), (0, 1)) & ~mask & ~grow
            src[m] = np.roll(img, (dy, dx), (0, 1))[m]; gsrc[m] = np.roll(gimg, (dy, dx), (0, 1))[m]; grow |= m
        img[grow] = src[grow]; gimg[grow] = gsrc[grow]; mask |= grow
    img[~mask] = NEUTRAL
    return img, gimg, report


def save_png(arr, name, path):
    rgba = np.concatenate([arr, np.ones((SIZE, SIZE, 1), np.float32)], 2)
    im = bpy.data.images.get(name) or bpy.data.images.new(name, SIZE, SIZE, alpha=True)
    im.colorspace_settings.name = "sRGB"
    # values are sRGB-encoded already; write them straight through
    im.pixels.foreach_set(rgba.ravel())
    im.filepath_raw = path; im.file_format = "PNG"; im.save()
    im.pack()
    return im


summary = {}
for obname, old, tones, stem in (("race_rider", "DR_rider_atlas_v5", RIDER_TONES, "DR2_rider_atlas_blank"),
                                 ("race_bike_youth", "DR_bike_atlas_kit_v7", BIKE_TONES, "DR2_bike_atlas_blank")):
    ob = bpy.data.objects[obname]
    img, gimg, rep = raster(ob, tones, GUIDE)
    new = save_png(img, stem, os.path.join(TEX_DIR, stem + ".png"))
    g = save_png(gimg, stem.replace("blank", "zone_guide"), os.path.join(TEX_DIR, stem.replace("blank", "zone_guide") + ".png"))
    bpy.data.images.remove(g)  # the guide is only a file, not part of the model
    swapped = 0
    for slot in ob.material_slots:
        for n in slot.material.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image and n.image.name == old:
                n.image = new; swapped += 1
    summary[obname] = {"atlas": stem + ".png", "replaced_atlas": old, "nodes_swapped": swapped, "zones": rep}

# drop Harvey's old materials and images (including fake-user ones), so none of his graphics travel with the copy
in_use = {s.material.name for o in bpy.data.objects if o.type == 'MESH' for s in o.material_slots if s.material}
for m in list(bpy.data.materials):
    if m.name not in in_use:
        bpy.data.materials.remove(m)
keep = {"DR2_rider_atlas_blank", "DR2_bike_atlas_blank", "Render Result", "Viewer Node"}
for im in list(bpy.data.images):
    if im.name not in keep:
        bpy.data.images.remove(im)
summary["images_in_file"] = sorted(im.name for im in bpy.data.images)

t = bpy.data.texts.get("DR2_model_notes.json") or bpy.data.texts.new("DR2_model_notes.json")
t.from_string(json.dumps({"based_on": "desktop_rider_3d.blend (Harvey)", "changes": "texture atlases only", **summary}, indent=1))
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND, copy=False, compress=True)
json.dump(summary, open(os.path.join(TEX_DIR, "build_summary.json"), "w"), indent=1)
print("BUILD done", OUT_BLEND)
