"""Checks every clip for the crew passing through the bike or each other (ray-parity inside test on the deformed meshes).
Writes work/bodycheck.json. Contact the model has in its own pose (hands round the rail, the driver on the seat) is the baseline."""
import bpy, json, sys
from mathutils import Vector as V
from mathutils.bvhtree import BVHTree
W='/tmp/claude-0/sc/work/'
bpy.ops.wm.open_mainfile(filepath=W+'sidecar_anim.blend')
D=json.load(open(W+'clips.json')); s=bpy.context.scene
SM=bpy.data.objects['Sidecar']; DM=bpy.data.objects['Driver']; PM=bpy.data.objects['Passenger']
DIRS=[V((0.31,0.17,0.93)).normalized(),V((-0.5,0.8,0.2)).normalized(),V((0.7,-0.6,-0.3)).normalized()]
def hits(bv,p,d):
    n=0; o=p.copy()
    for _ in range(60):
        loc,nor,idx,dist=bv.ray_cast(o,d)
        if loc is None: break
        n+=1; o=loc+d*1e-4
    return n
def inside(a,b,dg,step=3):
    ea=a.evaluated_get(dg); eb=b.evaluated_get(dg); ma=ea.to_mesh()
    bv=BVHTree.FromObject(eb,dg); Mi=b.matrix_world.inverted(); Ma=a.matrix_world; n=0
    vs=list(ma.vertices)[::step]
    for v in vs:
        p=Mi@(Ma@v.co)
        if sum(hits(bv,p,d)%2 for d in DIRS)>=2: n+=1
    ea.to_mesh_clear(); return n
names=sys.argv[sys.argv.index('--')+1].split(',') if '--' in sys.argv else None
out={}
for c in D['clips']:
    if names and c['name'] not in names: continue
    worst={'pas_in_bike':(0,0),'pas_in_driver':(0,0),'drv_in_pas':(0,0),'drv_in_bike':(0,0)}
    for i in range(0,c['frames']+1,3):
        s.frame_set(c['start']+i); dg=bpy.context.evaluated_depsgraph_get()
        r={'pas_in_bike':inside(PM,SM,dg),'pas_in_driver':inside(PM,DM,dg),'drv_in_pas':inside(DM,PM,dg),'drv_in_bike':inside(DM,SM,dg)}
        for k,v in r.items():
            if v>worst[k][0]: worst[k]=(v,i)
    out[c['name']]=worst; print('BODY',c['name'],worst,flush=True)
json.dump(out,open(W+'bodycheck.json','w'),indent=1)
