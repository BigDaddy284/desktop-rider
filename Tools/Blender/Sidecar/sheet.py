import bpy, math, json, sys, os
sys.path.insert(0,'/tmp/claude-0/sc/anim')
from mathutils import Vector
import props
W='/tmp/claude-0/sc/work/'
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
names=args[0].split(',') if args else []
per=int(args[1]) if len(args)>1 else 8
view=args[2] if len(args)>2 else 'chair'
bpy.ops.wm.open_mainfile(filepath=W+'sidecar_anim.blend')
D=json.load(open(W+'clips.json'))
s=bpy.context.scene; s.render.engine='BLENDER_EEVEE'; s.render.film_transparent=False; s.view_settings.view_transform='Standard'
s.eevee.taa_render_samples=8; s.render.resolution_x=420; s.render.resolution_y=340
w=bpy.data.worlds.new('W'); s.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(0.55,0.6,0.65,1); w.node_tree.nodes['Background'].inputs[1].default_value=1.0
sun=bpy.data.lights.new('S','SUN'); sun.energy=3.5; so=bpy.data.objects.new('S',sun); s.collection.objects.link(so); so.rotation_euler=(0.6,0.3,0.8)
cam=bpy.data.cameras.new('C'); cam.type='ORTHO'; cam.ortho_scale=float(os.environ.get('SCALE','3.6'))
co=bpy.data.objects.new('Cam',cam); s.collection.objects.link(co); s.camera=co
P=props.build(D)
for o in P.values(): o.location=(0,0,-50)
view0=view[:-3] if view.endswith('air') and view!='chair' else view; off={'chair':Vector((3.5,6,3.2)),'right':Vector((3.0,-6,3.0)),'side':Vector((0,-8,1.2)),'front':Vector((8,1.5,2.5))}[view0]
out=[]
from PIL import Image
for c in D['clips']:
    if names and c['name'] not in names: continue
    for o in P.values(): o.location=(0,0,-50)
    for p in c['props']: P[p['prop']].location=(p['x'],0,0)
    tiles=[]
    for k in range(per):
        fr=c['info'].get('take_off_t'); i=round((fr*24 + k*(c['info']['air_time']*24)/(per-1))) if (fr is not None and view.endswith('air') and view!='chair') else round(k*c['frames']/(per-1)); f=c['start']+i; s.frame_set(f)
        SRo=bpy.data.objects['sidecar_rig']; ch=SRo.matrix_world@SRo.pose.bones['chassis'].head; x=c['travel'][i]; tgt=Vector((x,0.45,0.75+max(0,ch.z-0.0)))
        co.location=tgt+off; co.rotation_euler=(tgt-co.location).to_track_quat('-Z','Y').to_euler()
        fn=W+'tile.png'; s.render.filepath=fn; bpy.ops.render.render(write_still=True)
        tiles.append(Image.open(fn).convert('RGB').copy())
    row=Image.new('RGB',(420*per,340),(0,0,0))
    for k,t in enumerate(tiles): row.paste(t,(420*k,0))
    from PIL import ImageDraw; ImageDraw.Draw(row).text((6,4),c['name'],fill=(255,255,255))
    out.append(row)
sheet=Image.new('RGB',(420*per,340*len(out)))
for r,row in enumerate(out): sheet.paste(row,(0,340*r))
sheet.save(W+'sheet_%s.jpg'%view,quality=80)
print('sheet rows',len(out))
