"""Exports the Desktop Rider bike + rider for Unity from the working .blend, without changing that file.

    blender --background <desktop_rider_3d.blend> --python export_models.py -- <Models dir> <bike atlas png> <rider atlas png> <master.json>

Everything happens in memory in this background session; the .blend is never saved.
  * the rider's helmet corrections (hooks + Seam Fix) are baked into the mesh, the Armature modifier stays live;
  * helper vertex groups / attributes are stripped;
  * the rigs are exported through plain copies that follow the animated originals bone for bone, so IK and the
    original actions are baked to plain keys, and each clip in DR_animations.json becomes its own take;
  * textures are copied beside the FBX (not embedded) and a manifest is written.
"""
import bpy, sys, os, json, shutil, hashlib
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
MODELS, BIKE_PNG, RIDER_PNG, MASTER_JSON = argv[:4]
# optional key=value settings after the four paths:
#   apply=0            do not use the exporter's "apply transform"
#   tricks=<dir>       key the trick animations from <dir>/trick_animations.py first (in memory) and export those clips
#   side=Right|Left    which side the tricks are keyed for (their gestures use the hand nearest the camera)
#   name=<file stem>   output name (default DesktopRider)
#   meshes=0           export the two rigs only (an animation-only file)
#   out=<dir>          write the FBX/manifest here instead of the Models dir (textures are still looked up in Models)
OPT = dict(a.split("=", 1) for a in argv[4:] if "=" in a)
APPLY_TRANSFORM = OPT.get("apply", "1") != "0" and "noapply" not in argv[4:]
TRICKS = OPT.get("tricks"); SIDE = OPT.get("side", "Right"); NAME = OPT.get("name", "DesktopRider")
WITH_MESHES = OPT.get("meshes", "1") != "0"
OUTDIR = OPT.get("out", MODELS)
TEX = os.path.join(MODELS, "Textures")
os.makedirs(TEX, exist_ok=True); os.makedirs(OUTDIR, exist_ok=True)
FBX = os.path.join(OUTDIR, NAME + ".fbx")
sc = bpy.context.scene
src_file = os.path.basename(bpy.data.filepath)        # file name only, so no local folder ends up in the manifest
clips = json.loads(bpy.data.texts["DR_animations.json"].as_string())
bike = bpy.data.objects["race_bike_youth"]; rider = bpy.data.objects["race_rider"]
brig = bpy.data.objects["bike_rig"]; rrig = bpy.data.objects["rider_rig"]
HELPER_GROUPS = ("Helmet", "MouthGuard", "HelmetRim", "ChinEndFlush", "LowerSides", "ChinSidesFlush")
HELPER_ATTRS = ("rest_pos", "rest_nrm", "DR_seam_offset")

# ---------------------------------------------------------------- 1. bake the helmet corrections into the rider mesh
sc.frame_set(1)
arm = next(m for m in rider.modifiers if m.type == 'ARMATURE')
helmet_idx = sorted(v.index for v in rider.data.vertices if any(g.group == rider.vertex_groups["Helmet"].index for g in v.groups))
arm.show_viewport = False
bpy.context.view_layer.update()
ev = rider.evaluated_get(bpy.context.evaluated_depsgraph_get()); me = ev.to_mesh()
assert len(me.vertices) == len(rider.data.vertices)
baked = [v.co.copy() for v in me.vertices]; ev.to_mesh_clear()
orig = [v.co.copy() for v in rider.data.vertices]
for m in [m for m in rider.modifiers if m.type != 'ARMATURE']:
    rider.modifiers.remove(m)
for v, co in zip(rider.data.vertices, baked):
    v.co = co
rider.data.update()
arm.show_viewport = False; bpy.context.view_layer.update()
ev = rider.evaluated_get(bpy.context.evaluated_depsgraph_get()); me = ev.to_mesh()
bake_dev = max((a.co - b).length for a, b in zip(me.vertices, baked)); ev.to_mesh_clear()
arm.show_viewport = True
moved = [i for i, (a, b) in enumerate(zip(orig, baked)) if (a - b).length > 1e-7]
print("EXPORT bake: rider verts %d, moved %d (all inside the helmet group: %s), baked-vs-live max deviation %.6f mm" % (len(baked), len(moved), set(moved) <= set(helmet_idx), bake_dev * 1000))
for ob in (bike, rider):
    for n in HELPER_GROUPS:
        g = ob.vertex_groups.get(n)
        if g: ob.vertex_groups.remove(g)
    for n in HELPER_ATTRS:
        a = ob.data.attributes.get(n)
        if a: ob.data.attributes.remove(a)
for n in ("Helmet Size", "Mouth Guard", "Helmet Rim Flush", "Chin End Flush"):
    e = bpy.data.objects.get(n)
    if e: bpy.data.objects.remove(e)

# ---------------------------------------------------------------- 1b. (tricks only) key the trick animations on this in-memory copy
trick_table = None
if TRICKS:
    sys.path.insert(0, TRICKS)
    import trick_animations as TA
    trick_table = TA.build(SIDE)                       # replaces DR_bike / DR_rider in this throwaway session only
    clips = [dict(name=t["name"], start=t["start"], end=t["end"], loop=False) for t in trick_table]
    print("EXPORT tricks keyed for side %s: %d clips, frames %d-%d" % (SIDE, len(clips), clips[0]["start"], clips[-1]["end"]))

# ---------------------------------------------------------------- 2. export rigs: plain copies that follow the animated originals
def follower(src, name):
    src.name = "SRC_" + name
    dup = src.copy(); dup.data = src.data.copy(); dup.name = name; dup.data.name = name
    dup.animation_data_clear()
    for c in src.users_collection: c.objects.link(dup)
    return dup
xb = follower(brig, "bike_rig"); xr = follower(rrig, "rider_rig")
# In the .blend the rider rig hangs off the bike's "chassis" bone. An armature nested under another armature's bone
# cannot be read back by Blender's own FBX importer (so it could not be verified), so here the rider rig is exported
# beside the bike rig and its own transform is keyed every frame to follow the chassis bone exactly.
from mathutils import Matrix
xr.parent = None; xr.parent_bone = ""; xr.matrix_parent_inverse = Matrix.Identity(4)
oc = xr.constraints.new('COPY_TRANSFORMS'); oc.target = rrig; oc.target_space = 'WORLD'; oc.owner_space = 'WORLD'
for dup, src in ((xb, brig), (xr, rrig)):
    dup.hide_set(False); dup.hide_viewport = False; dup.hide_select = False
    for pb in dup.pose.bones:
        for c in list(pb.constraints): pb.constraints.remove(c)
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
        c = pb.constraints.new('COPY_TRANSFORMS'); c.target = src; c.subtarget = pb.name; c.target_space = 'WORLD'; c.owner_space = 'WORLD'
for mesh, rig in ((bike, xb), (rider, xr)):
    mesh.parent = rig; mesh.matrix_parent_inverse = mesh.matrix_parent_inverse  # same local relation as before (parent was the source rig object)
    next(m for m in mesh.modifiers if m.type == 'ARMATURE').object = rig
    mesh.hide_set(False); mesh.hide_render = False
bpy.context.view_layer.update()
nondef = {rig.name: [b.name for b in rig.data.bones if not b.use_deform] for rig in (xb, xr)}
print("EXPORT non-deform bones:", nondef)

# one take per clip: marker strips on the exported bike rig (they only name the takes and give their frame ranges)
xb["dr_clip"] = 0.0
ad = xb.animation_data_create(); track = ad.nla_tracks.new(); track.name = "DR_clips"
for c in clips:
    act = bpy.data.actions.new("DRclip_" + c["name"])
    ad.action = act
    xb["dr_clip"] = 0.0; xb.keyframe_insert('["dr_clip"]', frame=c["start"]); xb.keyframe_insert('["dr_clip"]', frame=c["end"])
    ad.action = None
    st = track.strips.new(c["name"], int(c["start"]), act); st.name = c["name"]
    st.frame_start = c["start"]; st.frame_end = c["end"]; st.action_frame_start = c["start"]; st.action_frame_end = c["end"]
print("EXPORT strips:", [(s.name, s.frame_start, s.frame_end) for s in track.strips])

# ---------------------------------------------------------------- 3. textures beside the FBX
tex = {"race_bike_youth": ("DesktopRider_Bike.png", BIKE_PNG), "race_rider": ("DesktopRider_Rider.png", RIDER_PNG)}
for ob in (bike, rider):
    name, srcp = tex[ob.name]; dst = os.path.join(TEX, name)
    if not TRICKS and OUTDIR == MODELS:
        shutil.copyfile(srcp, dst)                     # the tricks file re-uses the textures already there
    imgs = {s.material.node_tree.nodes["DR_ATLAS"].image for s in ob.material_slots}
    assert len(imgs) == 1
    img = imgs.pop()
    if img.packed_file: img.unpack(method='REMOVE')
    img.filepath = dst; img.name = os.path.splitext(name)[0]; img.reload()

# ---------------------------------------------------------------- 4. FBX
for o in bpy.context.view_layer.objects: o.select_set(False)
for o in ((xb, xr, bike, rider) if WITH_MESHES else (xb, xr)): o.select_set(True)
bpy.context.view_layer.objects.active = xb
bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types=({'ARMATURE', 'MESH'} if WITH_MESHES else {'ARMATURE'}), axis_forward='-Z', axis_up='Y',
                         apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=APPLY_TRANSFORM, mesh_smooth_type='OFF', use_mesh_modifiers=True,
                         add_leaf_bones=False, use_armature_deform_only=True, primary_bone_axis='Y', secondary_bone_axis='X',
                         bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                         bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
                         path_mode='RELATIVE', embed_textures=False)
print("EXPORT wrote", FBX, os.path.getsize(FBX), "bytes")

# ---------------------------------------------------------------- 5. manifest
master = json.load(open(MASTER_JSON))
def md5(a): return hashlib.md5(np.asarray(a, dtype=np.float32).tobytes()).hexdigest()
if TRICKS:
    tm = dict(source_blend=src_file, fbx=NAME + ".fbx", fps=sc.render.fps, units="metres", keyed_for_side=SIDE, contains_meshes=WITH_MESHES,
              note="Animation clips for the rigs in DesktopRider.fbx (same bone names and hierarchy). rider_rig is exported beside bike_rig and its transform is keyed to follow the chassis bone. "
                   "Frames are the positions in the keyed sequence; each clip is its own take starting at 0. Props and their positions are in Props/props_layout.json.",
              rigs={rig.name: [dict(name=b.name, parent=b.parent.name if b.parent else None) for b in rig.data.bones] for rig in (xb, xr)},
              clips=[dict(name=t["name"], start=t["start"], end=t["end"], frames=t["frames"], loop=t.get("loop", False), stage=t.get("stage"), fps=sc.render.fps, info=t.get("info")) for t in trick_table])
    json.dump(tm, open(os.path.join(OUTDIR, NAME + "_manifest.json"), "w"), indent=1)
    print("EXPORT tricks manifest written; session file still", bpy.data.filepath, "(not saved)")
    sys.exit(0)
man = dict(source_blend=src_file, fbx="DesktopRider.fbx", fps=sc.render.fps, units="metres", up_axis="Y (converted on export; Blender scene is Z up, +X forward, +Y = bike's left)",
           textures={"race_bike_youth": "Textures/DesktopRider_Bike.png", "race_rider": "Textures/DesktopRider_Rider.png"}, meshes={}, rigs={}, clips=[],
           helmet_vertex_indices_changed=len(helmet_idx), expected={})
for ob in (bike, rider):
    me = ob.data
    man["meshes"][ob.name] = dict(vertices=len(me.vertices), faces=len(me.polygons), triangles=sum(len(p.vertices) - 2 for p in me.polygons), uv_layers=[u.name for u in me.uv_layers],
                                  materials=[s.material.name for s in ob.material_slots], vertex_groups=[g.name for g in ob.vertex_groups], parent=ob.parent.name,
                                  object_scale=[round(v, 5) for v in ob.scale], rest_position_md5=md5([c for v in me.vertices for c in v.co]))
    mm = master["objects"][ob.name]
    man["expected"][ob.name] = dict(master_vertices=mm["verts"], master_faces=mm["faces"], master_rest_position_md5=mm["co_md5"],
                                    rest_positions_equal_master=(md5([c for v in me.vertices for c in v.co]) == mm["co_md5"]))
for rig in (xb, xr):
    man["rigs"][rig.name] = dict(bones=[dict(name=b.name, parent=b.parent.name if b.parent else None, deform=b.use_deform) for b in rig.data.bones],
                                 parent=rig.parent.name if rig.parent else None, parent_bone=rig.parent_bone if rig.parent else None)
    man["expected"][rig.name] = dict(master_bones=[[b[0], b[1]] for b in master["objects"][rig.name]["bones"]])
for c in clips:
    man["clips"].append(dict(name=c["name"], start=c["start"], end=c["end"], frames=c["end"] - c["start"], loop=c.get("loop", False), fps=sc.render.fps))
man["expected"]["helmet_vertices"] = len(helmet_idx)
man["expected"]["rider_vertices_moved_from_master"] = len(moved)
json.dump(man, open(os.path.join(MODELS, "DesktopRider_model_manifest.json"), "w"), indent=1)
json.dump(dict(helmet=helmet_idx), open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "helmet_vertex_indices.json"), "w"))
print("EXPORT manifest written; session file still", bpy.data.filepath, "(not saved)")
