"""Step 2: rigs for the outfit, the driver and the passenger. Shapes are not changed: the bones follow the model as it is.
Every bone rests with no rotation (pointing +Y), so a pose is just where each joint goes and how it turns."""
import bpy, json, math
import numpy as np
from mathutils import Vector, Matrix
W='/tmp/claude-0/sc/work/'
bpy.ops.wm.open_mainfile(filepath=W+'sidecar_baked.blend')
sc=bpy.context.scene

def armature(name, bones):
    """bones: list of (name, head, parent)."""
    arm=bpy.data.armatures.new(name); ob=bpy.data.objects.new(name,arm); sc.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active=ob
    bpy.ops.object.mode_set(mode='EDIT')
    for n,h,p in bones:
        e=arm.edit_bones.new(n); e.head=Vector(h); e.tail=Vector(h)+Vector((0,0.08,0)); e.roll=0
        if p: e.parent=arm.edit_bones[p]
    bpy.ops.object.mode_set(mode='OBJECT')
    for pb in ob.pose.bones: pb.rotation_mode='QUATERNION'
    arm.display_type='STICK'
    return ob

# ---------------------------------------------------------------- the outfit
SC=[('root',(0,0,0),None),('chassis',(0,0,0),'root'),('steer',(0.33,0,0.95),'chassis'),('front_link',(0.60,0,0.325),'steer'),
    ('front_wheel',(0.75,0,0.345),'front_link'),('swingarm',(-0.23,0,0.39),'chassis'),('rear_wheel',(-0.75,0,0.335),'swingarm'),
    ('chair_wheel',(-0.595,1.05,0.285),'chassis')]
srig=armature('sidecar_rig',SC)
body=bpy.data.objects['Sidecar']
body.parent=srig; m=body.modifiers.new('Armature','ARMATURE'); m.object=srig

# ---------------------------------------------------------------- the crew
# Driver joints, read off the model (outfit space, metres): see Documentation/SPRITE_SPECIFICATION.md (sidecar section)
J={'pelvis':(-0.15,0,0.97),'spine':(-0.10,0,1.10),'chest':(-0.04,0,1.28),'neck':(0.0,0,1.42),'crown':(0.08,0,1.66)}
for s,y in (('L',1),('R',-1)):
    J['shoulder.'+s]=(-0.02,0.24*y,1.38); J['elbow.'+s]=(0.10,0.38*y,1.28); J['wrist.'+s]=(0.20,0.36*y,1.16); J['tip.'+s]=(0.29,0.35*y,1.11)
    J['hip.'+s]=(-0.12,0.12*y,0.93); J['knee.'+s]=(0.20,0.20*y,0.78); J['ankle.'+s]=(0.0,0.18*y,0.40); J['toe.'+s]=(0.20,0.17*y,0.32)
# which parts of the model each joint is read from when it is found on the passenger (material numbers)
HEL=(0,1,2,3); NECK=(4,); PANTS=(5,); JER=(6,); GLO=(7,); KNEE=(8,); BOOT=(9,10,11,12)
JM={'pelvis':PANTS,'spine':JER+PANTS,'chest':JER,'neck':NECK+JER+HEL,'crown':HEL}
for s in 'LR':
    JM.update({'shoulder.'+s:JER,'elbow.'+s:JER,'wrist.'+s:GLO+JER,'tip.'+s:GLO,'hip.'+s:PANTS,'knee.'+s:PANTS+KNEE,'ankle.'+s:BOOT,'toe.'+s:BOOT})
corners=json.load(open(W+'crew_corners.json'))
D=np.array([c[:3] for c in corners['Rider']]); P=np.array([c[:3] for c in corners['Passenger']]); MAT=np.array([c[3] for c in corners['Rider']])
assert len(D)==len(P)
def kabsch(a,b):
    ca,cb=a.mean(0),b.mean(0); H=(a-ca).T@(b-cb); U,S,Vt=np.linalg.svd(H); d=np.sign(np.linalg.det(Vt.T@U.T))
    R=Vt.T@np.diag([1,1,d])@U.T; return R,ca,cb
def on_passenger(name):
    j=np.array(J[name]); side=name[-1] if name[-2]=='.' else None
    ok=np.isin(MAT,JM[name])
    if side: ok&=(np.sign(D[:,1])==(1 if side=='L' else -1))
    dist=np.linalg.norm(D-j,axis=1)
    for r in (0.07,0.1,0.14,0.2):
        sel=ok&(dist<r)
        if sel.sum()>=12: break
    R,ca,cb=kabsch(D[sel],P[sel]); return tuple(R@(j-ca)+cb), int(sel.sum())
JP={}
for n in J:
    JP[n],k=on_passenger(n)
print('passenger joints:',{n:tuple(round(c,3) for c in v) for n,v in JP.items()})

BONES=[('root','origin',None),('hips','pelvis','root'),('spine','spine','hips'),('chest','chest','spine'),('head','neck','chest')]
SEG={'hips':('pelvis','spine'),'spine':('spine','chest'),'chest':('chest','neck'),'head':('neck','crown')}
for s in 'LR':
    BONES+=[('upper_arm.'+s,'shoulder.'+s,'chest'),('forearm.'+s,'elbow.'+s,'upper_arm.'+s),('hand.'+s,'wrist.'+s,'forearm.'+s),
            ('thigh.'+s,'hip.'+s,'hips'),('shin.'+s,'knee.'+s,'thigh.'+s),('foot.'+s,'ankle.'+s,'shin.'+s)]
    SEG.update({'upper_arm.'+s:('shoulder.'+s,'elbow.'+s),'forearm.'+s:('elbow.'+s,'wrist.'+s),'hand.'+s:('wrist.'+s,'tip.'+s),
                'thigh.'+s:('hip.'+s,'knee.'+s),'shin.'+s:('knee.'+s,'ankle.'+s),'foot.'+s:('ankle.'+s,'toe.'+s)})
ALLOW={0:'head',1:None,2:'head',3:'head',4:'head chest',5:'hips spine thigh shin',6:'hips spine chest upper_arm forearm',7:'hand forearm',
       8:'thigh shin',9:'shin foot',10:'shin foot',11:'shin foot',12:'shin foot'}

def seg_dist(p,a,b):
    ab=b-a; t=np.clip(((p-a)@ab)/(ab@ab),0,1); return np.linalg.norm(p-(a+np.outer(t,ab)),axis=1)

def crew(meshname, rigname, joints):
    o=bpy.data.objects[meshname]
    rig=armature(rigname,[(n,(0,0,0) if j=='origin' else joints[j],p) for n,j,p in BONES])
    # the crew ride on the outfit's chassis bone
    c=rig.constraints.new('CHILD_OF'); c.target=srig; c.subtarget='chassis'; c.inverse_matrix=Matrix.Identity(4)
    o.parent=rig; mod=o.modifiers.new('Armature','ARMATURE'); mod.object=rig
    me=o.data; V=np.array([v.co[:] for v in me.vertices])
    # body part of each vertex from the painted model (the baked mesh has one material): same triangles, same order
    src={'Driver':'Rider','Passenger':'Passenger'}[meshname]
    cm=[c[3] for c in corners[src]]; me.calc_loop_triangles(); assert len(me.loop_triangles)*3==len(cm)
    vm=np.zeros(len(V),int)
    for k,tri in enumerate(me.loop_triangles):
        for c in range(3): vm[tri.vertices[c]]=cm[3*k+c]
    names=[b for b in SEG]
    dist=np.stack([seg_dist(V,np.array(joints[SEG[b][0]]),np.array(joints[SEG[b][1]])) for b in names],1)
    allowed=np.ones_like(dist,bool)
    for mi,a in ALLOW.items():
        if a is None: continue
        keys=a.split(); row=np.array([any(b==k or b.startswith(k+'.') for k in keys) for b in names])
        # glove, boot and limb parts only follow their own side
        allowed[vm==mi]=row
    side=np.where(V[:,1]>joints['pelvis'][1],'L','R')
    for i,b in enumerate(names):
        if b[-2:] in ('.L','.R'): allowed[:,i]&=(side==b[-1])
    w=np.where(allowed,1.0/(dist+0.012)**6,0.0)
    top=np.argsort(-w,1)[:,:3]
    groups={b:o.vertex_groups.new(name=b) for b in names}
    for vi in range(len(V)):
        ws=w[vi,top[vi]]; tot=ws.sum()
        for k,bi in enumerate(top[vi]):
            if ws[k]/tot>0.02: groups[names[bi]].add([vi],float(ws[k]/tot),'REPLACE')
    return rig
crew('Driver','driver_rig',J)
crew('Passenger','passenger_rig',JP)
json.dump(dict(driver=J,passenger=JP,sidecar={n:h for n,h,_ in SC}),open(W+'joints.json','w'),indent=1)
bpy.ops.wm.save_as_mainfile(filepath=W+'sidecar_rigged.blend')
print('rigged saved')
