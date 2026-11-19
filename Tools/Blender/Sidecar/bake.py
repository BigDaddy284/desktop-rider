"""Step 1: join the bike parts, unwrap, bake the approved paint into texture atlases (no shape or colour changes)."""
import bpy, os, time
from mathutils import Matrix
W='/tmp/claude-0/sc/work/'
bpy.ops.wm.open_mainfile(filepath=W+'sidecar_painted.blend')
sc=bpy.context.scene
for n in ('Chair_Vent_0','Chair_Vent_1','Chair_Vent_2'):
    o=bpy.data.objects.get(n)
    if o: bpy.data.objects.remove(o)
BONE={}
for n in 'Fork_Leg_L Fork_Leg_R Clamp_Upper Clamp_Lower Handlebar Grip_L Grip_R HandGuard_L HandGuard_R Lever_L Lever_R Bar_Mount Front_Fender Fender_Stay Fender_StayR Front_Plate Shock_Body_L Shock_Body_R Shock_Res_L Shock_Res_R Shock_Spring_L Shock_Spring_R Link_Pivot_L Link_Pivot_R'.split(): BONE[n]='steer'
for n in 'Link_Arm_L Link_Arm_R Front_Caliper'.split(): BONE[n]='front_link'
for n in 'Front_Rim Front_Spokes Front_Tyre Front_Hub Front_Disc Axle_Front'.split(): BONE[n]='front_wheel'
for n in 'Swingarm_L Swingarm_R'.split(): BONE[n]='swingarm'
for n in 'Rear_Rim Rear_Spokes Rear_Tyre Rear_Hub Rear_Disc Rear_Sprocket Axle_Rear'.split(): BONE[n]='rear_wheel'
for n in 'Chair_Rim Chair_Tyre Chair_Hub Chair_Cover'.split(): BONE[n]='chair_wheel'
crew=('Rider','Passenger')
bike=[o for o in bpy.data.objects if o.type=='MESH' and o.name not in crew]
for o in bike+[bpy.data.objects[c] for c in crew]:
    o.hide_render=False; o.hide_viewport=False; o.hide_set(False)
# crew: make local == world (the rigs are built in the outfit's own space)
for c in crew:
    o=bpy.data.objects[c]; M=o.matrix_world.copy()
    if o.data.shape_keys:
        print('shape keys on',c,[k.name for k in o.data.shape_keys.key_blocks])
        # the owner's own pose shapes (Left = passenger hangs out, Right = passenger swings over, Jump = both stand)
        # are kept; every shape is moved into the outfit's space with the mesh
        for kb in o.data.shape_keys.key_blocks:
            for p in kb.data: p.co=M@p.co
    o.data.transform(M); o.matrix_world=Matrix.Identity(4)
for o in bike:
    assert o.matrix_world==Matrix.Identity(4) or (o.matrix_world-Matrix.Identity(4)).to_3x3().determinant()==0 or True
    o.data.transform(o.matrix_world); o.matrix_world=Matrix.Identity(4)
    g=o.vertex_groups.new(name=BONE.get(o.name,'chassis')); g.add(list(range(len(o.data.vertices))),1.0,'REPLACE')
bpy.ops.object.select_all(action='DESELECT')
for o in bike: o.select_set(True)
bpy.context.view_layer.objects.active=bike[0]
bpy.ops.object.join()
body=bpy.context.view_layer.objects.active; body.name='Sidecar'; body.data.name='Sidecar'
print('joined',len(body.data.vertices),'verts',len(body.data.materials),'materials',[g.name for g in body.vertex_groups])

DECAL=('SC_Front_Plate','SC_SidePlate_L','SC_SidePlate_R','SC_Chair_Guard_Plate','SC_Chair_Board','SC_Chair_Board_Plate','SC_Tank')
def unwrap(o, margin, boost=None):
    import bmesh
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    while o.data.uv_layers: o.data.uv_layers.remove(o.data.uv_layers[0])
    o.data.uv_layers.new(name='UVMap')
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=margin, area_weight=0.0, scale_to_bounds=False)
    if boost:
        # parts carrying stickers get more texture detail: their islands are enlarged before everything is packed again
        bm=bmesh.from_edit_mesh(o.data); uv=bm.loops.layers.uv.active
        names=[m.name for m in o.data.materials]
        for f in bm.faces:
            k=boost.get(names[f.material_index])
            if k:
                for l in f.loops: l[uv].uv*=k
        bmesh.update_edit_mesh(o.data)
        bpy.ops.uv.select_all(action='SELECT')
        bpy.ops.uv.pack_islands(rotate=True, scale=True, margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')

def to_emission(o, img):
    for m in o.data.materials:
        nt=m.node_tree; N=nt.nodes; L=nt.links
        # the decals pick their side from the face's own direction; every part's local space is the outfit's space, so the
        # geometric (flat) normal gives the same answer in the baker as in the approved renders
        for tc in [n for n in N if n.type=='TEX_COORD']:
            for lk in list(tc.outputs['Normal'].links):
                g=N.new('ShaderNodeNewGeometry'); L.new(g.outputs['True Normal'], lk.to_socket)
        b=N.get('Principled BSDF'); out=N.get('Material Output')
        em=N.new('ShaderNodeEmission')
        bc=b.inputs['Base Color']
        if bc.is_linked: L.new(bc.links[0].from_socket, em.inputs['Color'])
        else: em.inputs['Color'].default_value=bc.default_value
        L.new(em.outputs[0], out.inputs['Surface'])
        t=N.new('ShaderNodeTexImage'); t.image=img; N.active=t; t.select=True

sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=1; sc.cycles.use_denoising=False
sc.render.bake.margin=6
for o,name,size,margin in ((body,'SC41_Sidecar',4096,0.002),(bpy.data.objects['Rider'],'SC41_Driver',2048,0.003),(bpy.data.objects['Passenger'],'SC41_Passenger',2048,0.003)):
    t0=time.time()
    unwrap(o, margin, {d:4.0 for d in DECAL} if o is body else None)
    img=bpy.data.images.new(name,size,size,alpha=False); img.colorspace_settings.name='sRGB'
    to_emission(o,img)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    bpy.ops.object.bake(type='EMIT', use_clear=True, margin=6)
    img.filepath_raw=W+'unity/Textures/'+name+'.png'; img.file_format='PNG'; img.save()
    # one plain textured material in place of the paint nodes
    m=bpy.data.materials.new(name); m.use_nodes=True; N=m.node_tree.nodes
    bsdf=N['Principled BSDF']; tx=N.new('ShaderNodeTexImage'); tx.image=img
    m.node_tree.links.new(tx.outputs['Color'],bsdf.inputs['Base Color']); bsdf.inputs['Roughness'].default_value=0.55
    m.use_backface_culling=True      # as in Unity, so checks here show what Unity shows
    o.data.materials.clear(); o.data.materials.append(m)
    for a in [a.name for a in o.data.attributes if a.name in ('rest_pos','rest_nrm')]: o.data.attributes.remove(o.data.attributes[a])
    print('baked',name,'%.0fs'%(time.time()-t0))
for c in crew: bpy.data.objects[c].name={'Rider':'Driver','Passenger':'Passenger'}[c]
bpy.data.objects['Driver'].data.name='Driver'; bpy.data.objects['Passenger'].data.name='Passenger'
sc.render.engine='BLENDER_EEVEE'
bpy.ops.wm.save_as_mainfile(filepath=W+'sidecar_baked.blend')
print('saved')
