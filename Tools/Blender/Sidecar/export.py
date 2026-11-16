"""Exports the sidecar outfit, driver and passenger with every clip as its own take, plus the props and a manifest.
    python: exec with OUT set to the Unity folder (Assets/DesktopRider/Sidecar)."""
import bpy, json, os, shutil, sys
sys.path.insert(0,'/tmp/claude-0/sc/anim')
from mathutils import Matrix
W='/tmp/claude-0/sc/work/'
OUT=W+'unity/'
os.makedirs(OUT+'Models/Textures',exist_ok=True); os.makedirs(OUT+'Props',exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=W+'sidecar_anim.blend')
sc=bpy.context.scene
D=json.load(open(W+'clips.json'))
SRC={n:bpy.data.objects[n] for n in ('sidecar_rig','driver_rig','passenger_rig')}
MESH={'sidecar_rig':bpy.data.objects['Sidecar'],'driver_rig':bpy.data.objects['Driver'],'passenger_rig':bpy.data.objects['Passenger']}

def follower(src,name):
    src.name='SRC_'+name
    dup=src.copy(); dup.data=src.data.copy(); dup.name=name; dup.data.name=name
    dup.animation_data_clear()
    for c in list(dup.constraints): dup.constraints.remove(c)
    for c in src.users_collection: c.objects.link(dup)
    dup.parent=None; dup.matrix_world=Matrix.Identity(4)
    oc=dup.constraints.new('COPY_TRANSFORMS'); oc.target=src
    for pb in dup.pose.bones:
        for c in list(pb.constraints): pb.constraints.remove(c)
        pb.location=(0,0,0); pb.rotation_quaternion=(1,0,0,0); pb.scale=(1,1,1)
        c=pb.constraints.new('COPY_TRANSFORMS'); c.target=src; c.subtarget=pb.name; c.target_space='WORLD'; c.owner_space='WORLD'
    return dup
X={n:follower(o,n) for n,o in SRC.items()}
sc.frame_set(1)
for n,me in MESH.items():
    # every mesh is modelled in its rig's own space (rigs rest at the origin)
    me.parent=X[n]; me.matrix_parent_inverse=Matrix.Identity(4); me.matrix_basis=Matrix.Identity(4)
    next(m for m in me.modifiers if m.type=='ARMATURE').object=X[n]
bpy.context.view_layer.update()

xb=X['sidecar_rig']; xb['sc_clip']=0.0
ad=xb.animation_data_create(); tr=ad.nla_tracks.new(); tr.name='SC_clips'
for c in D['clips']:
    act=bpy.data.actions.new('SCclip_'+c['name']); ad.action=act
    xb['sc_clip']=0.0; xb.keyframe_insert('["sc_clip"]',frame=c['start']); xb.keyframe_insert('["sc_clip"]',frame=c['end'])
    ad.action=None
    st=tr.strips.new(c['name'],int(c['start']),act); st.name=c['name']
    st.frame_start=c['start']; st.frame_end=c['end']

for img in bpy.data.images:
    if img.name.startswith('SC41_'):
        dst=OUT+'Models/Textures/'+img.name+'.png'; shutil.copyfile(W+'unity/Textures/'+img.name+'.png',dst)
        img.filepath=dst; img.reload()
bpy.ops.object.select_all(action='DESELECT')
for o in list(X.values())+list(MESH.values()): o.select_set(True)
bpy.context.view_layer.objects.active=xb
FBX=OUT+'Models/Sidecar41.fbx'
bpy.ops.export_scene.fbx(filepath=FBX,use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',
    apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,mesh_smooth_type='OFF',use_mesh_modifiers=False,
    add_leaf_bones=False,use_armature_deform_only=False,primary_bone_axis='Y',secondary_bone_axis='X',
    bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,bake_anim_step=1.0,bake_anim_simplify_factor=0.0,path_mode='RELATIVE',embed_textures=False)
print('EXPORT wrote',FBX,os.path.getsize(FBX))

# props: one FBX each, origin at the start of the piece on the ground, in the outfit's space
import props
P=props.build(D)
for name,o in P.items():
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    bpy.ops.export_scene.fbx(filepath=OUT+'Props/'+name+'.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
        apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,mesh_smooth_type='OFF',bake_anim=False)
man=dict(name='Sidecar 41',fbx='Sidecar41.fbx',fps=D['fps'],units='metres',
    note='Second character set (sidecar outfit 41). One take per clip, each starting at 0. sidecar_rig/root carries the travel along the '
         'outfit\'s forward axis (Blender +X); driver_rig and passenger_rig are keyed to follow sidecar_rig/chassis. The crew\'s Left / Right / Jump '
         'blend shapes are the owner\'s own poses (Left: passenger hangs out over the chair; Right: passenger swings over; Jump: standing). '
         'Props: x is metres along the travel from the outfit\'s position at the first frame.',
    rigs={n:[dict(name=b.name,parent=b.parent.name if b.parent else None) for b in o.data.bones] for n,o in X.items()},
    clips=[dict(name=c['name'],frames=c['frames'],loop=c['loop'],kind=c['kind'],props=c['props'],travel=c['travel'][-1],info=c['info']) for c in D['clips']])
json.dump(man,open(OUT+'Models/Sidecar41_manifest.json','w'),indent=1)
print('manifest clips',len(man['clips']))
