"""Desktop copy of the approved sidecar set for the 3D Desktop Rider app (not for the Move Viewer).
Same poses and clips as Sidecar41.fbx, but held in place: sidecar_rig/root no longer carries the travel, because the
app moves the outfit itself (FreeRider while riding about, TrickRunner during tricks, from the travel in the layout).
Writes Models/Sidecar41_Desktop.fbx, its _manifest.json (looping clips for the importer) and
Models/sidecar_layout.json (the app's trick layout: props and travel per frame)."""
import bpy, json, os, sys
sys.path.insert(0,'/tmp/claude-0/sc/anim')
from mathutils import Matrix
W='/tmp/claude-0/sc/work/'
OUT=W+'desktop/'
os.makedirs(OUT+'Models',exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=W+'sidecar_anim.blend')
sc=bpy.context.scene
D=json.load(open(W+'clips.json'))
rig=bpy.data.objects['sidecar_rig']; root=rig.pose.bones['root']
assert rig.matrix_world==Matrix.Identity(4)

# 1. hold the outfit in place: take each clip's travel off the root, key by key on every frame
want={}
for c in D['clips']:
    if c['travel'][-1] < 1e-4 and max(c['travel']) < 1e-4: continue
    for t in range(c['frames']+1):
        f=int(c['start'])+t; sc.frame_set(f)
        want[f]=(root.location.x - c['travel'][t], c['travel'][t], (rig.matrix_world@root.head).x)
for f,(x,_,_) in want.items():
    root.location.x=x; root.keyframe_insert('location',index=0,frame=f)
worst=0
for f,(x,trav,before) in want.items():
    sc.frame_set(f); worst=max(worst,abs((rig.matrix_world@root.head).x-(before-trav)))
print('INPLACE frames',len(want),'worst error',worst)

# 2. export exactly as export.py does, to a new file
exec(open('/tmp/claude-0/sc/anim/export.py').read().split("X={n:follower")[0].split("D=json.load")[1].split('\n',1)[1])
X={n:follower(o,n) for n,o in SRC.items()}
sc.frame_set(1)
for n,me in MESH.items():
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
        os.makedirs(OUT+'Models/Textures',exist_ok=True); import shutil; shutil.copyfile(W+'unity/Models/Textures/'+img.name+'.png',OUT+'Models/Textures/'+img.name+'.png'); img.filepath=OUT+'Models/Textures/'+img.name+'.png'
bpy.ops.object.select_all(action='DESELECT')
for o in list(X.values())+list(MESH.values()): o.select_set(True)
bpy.context.view_layer.objects.active=xb
FBX=OUT+'Models/Sidecar41_Desktop.fbx'
bpy.ops.export_scene.fbx(filepath=FBX,use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',
    apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,mesh_smooth_type='OFF',use_mesh_modifiers=False,
    add_leaf_bones=False,use_armature_deform_only=False,primary_bone_axis='Y',secondary_bone_axis='X',
    bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,bake_anim_step=1.0,bake_anim_simplify_factor=0.0,path_mode='RELATIVE',embed_textures=False)
print('EXPORT wrote',FBX,os.path.getsize(FBX))

# 3. manifest (the importer reads which clips loop) and the trick layout
man=dict(name='Sidecar 41 (desktop)',fbx='Sidecar41_Desktop.fbx',fps=D['fps'],units='metres',
    note='Desktop copy of the approved Sidecar41.fbx: same clips and poses, held in place (no travel on sidecar_rig/root). '
         'The app moves the outfit by the travel in sidecar_layout.json.',
    ride_speed=D.get('ride_speed',3.0),turn_radius=D.get('turn_radius',3.2),
    clips=[dict(name=c['name'],frames=c['frames'],loop=c['loop'],kind=c['kind'],travel=c['travel'][-1]) for c in D['clips']])
json.dump(man,open(OUT+'Models/Sidecar41_Desktop_manifest.json','w'),indent=1)
import props
P=props.build(D)
dims={}
for name,o in P.items():
    xs=[(o.matrix_world@v.co) for v in o.data.vertices]
    dims[name]=dict(file=name+'.fbx',kind='dirt',length=round(max(v.x for v in xs)-min(v.x for v in xs),4),
                    width=round(max(v.y for v in xs)-min(v.y for v in xs),4),height=round(max(v.z for v in xs),4))
STAGE={'ground':'ground','trick':'ground','jump':'air'}
anims=[]
for c in D['clips']:
    if c['kind'] not in STAGE: continue
    anims.append(dict(name=c['name'],stage=STAGE[c['kind']],frames=c['frames'],
        props=[dict(prop=p['prop'],x=round(p['x'],4),length=round(p['length'],4)) for p in c['props']],
        travel=[round(v,4) for v in c['travel']]))
lay=dict(units='metres',fps=D['fps'],note='Sidecar 41 tricks for the desktop app. x is along the travel from the outfit at the first frame; '
    'travel[i] is how far the outfit has gone at frame i (frames+1 values). Props sit on the ground, origin at the start of the piece.',
    props=dims,animations=anims)
json.dump(lay,open(OUT+'Models/sidecar_layout.json','w'),indent=1)
print('LAYOUT',[(a['name'],a['stage'],a['frames'],a['travel'][-1]) for a in anims]); print('PROPS',dims)
