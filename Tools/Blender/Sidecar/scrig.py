"""Pose engine for the sidecar rigs. All bones rest unrotated, so a pose is given as armature-space rotations.
Used by sidecar_anim.py; runs inside Blender's Python."""
import bpy, math, json
from mathutils import Vector, Matrix, Quaternion

W='/tmp/claude-0/sc/work/'
JOINTS=json.load(open(W+'joints.json'))
X,Y,Z=Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1))
I=Quaternion()
def q(axis,deg): return Quaternion(Vector(axis).normalized(),math.radians(deg))
def V(t): return Vector(t)
def sm(t,a,b):
    if b==a: return 1.0 if t>=b else 0.0
    x=min(1.0,max(0.0,(t-a)/(b-a))); return x*x*(3-2*x)
def bump(t,a,b,c,d): return sm(t,a,b)*(1-sm(t,c,d))
def lerp(a,b,w): return a+(b-a)*w
def shock(tau,rise=0.09,settle=0.75):
    if tau<=0: return 0.0
    if tau<rise: return math.sin(0.5*math.pi*tau/rise)
    x=(tau-rise)/(settle-rise)
    if x>=1: return 0.0
    return math.exp(-3.4*x)*math.cos(1.5*math.pi*x)*(1-x*x*x)

# wheel geometry (outfit space, metres)
RW=dict(rear=(V((-0.75,0,0.335)),0.335),front=(V((0.75,0,0.345)),0.345),chair=(V((-0.595,1.05,0.285)),0.285))
STEER_HEAD=V(JOINTS['sidecar']['steer']); STEER_AXIS=(V((0.75,0,0.345))-V((0.60,0,0.10))).normalized()
STEER_AXIS=(V((0.30,0,1.0))-V((0.62,0,0.35))).normalized()*-1   # down the fork rake (from the head stock towards the axle)

def apply(rig, D, T=None):
    """D: bone -> absolute armature-space rotation (Quaternion); T: bone -> extra armature-space translation of its head.
    Bones not in D take their parent's rotation."""
    T=T or {}
    bones=rig.data.bones; P={}; Dabs={}
    for b in bones:          # parents come before children in this list
        h=b.head_local
        if b.parent:
            pb=b.parent; hp=pb.head_local
            Dp=Dabs[pb.name]; headp=P[pb.name].to_translation()
            head=headp+Dp@(h-hp)
        else:
            Dp=I; head=h.copy()
        if b.name in T: head=head+V(T[b.name])
        d=D.get(b.name,Dp)
        Dabs[b.name]=d
        P[b.name]=Matrix.Translation(head)@d.to_matrix().to_4x4()
        par=P[b.parent.name]@Matrix.Translation(h-b.parent.head_local) if b.parent else Matrix.Translation(h)
        basis=par.inverted()@P[b.name]
        pbn=rig.pose.bones[b.name]
        pbn.location=basis.to_translation(); pbn.rotation_quaternion=basis.to_quaternion()
    return P, Dabs

def two_bone(A,E0,W0,A0,T,pole):
    """Upper and lower limb rotations so the limb starting at A reaches T, bending towards pole (rest joints A0,E0,W0)."""
    l1=(E0-A0).length; l2=(W0-E0).length
    d=T-A; dl=max(1e-4,min(d.length,l1+l2-1e-4)); u=d.normalized()
    ca=max(-1,min(1,(l1*l1+dl*dl-l2*l2)/(2*l1*dl))); sa=math.sqrt(1-ca*ca)
    v=pole-u*pole.dot(u)
    if v.length<1e-6: v=u.orthogonal()
    v.normalize()
    E=A+(u*ca+v*sa)*l1; Wp=A+u*dl
    n0=(E0-A0).cross(W0-E0)
    n=(E-A).cross(Wp-E)
    if n0.length<1e-6: n0=(E0-A0).cross(Z)
    if n.length<1e-6: n=(E-A).cross(v)
    def frame(dv,nv):
        dv=dv.normalized(); nv=(nv-dv*nv.dot(dv)).normalized(); return Matrix((dv,nv,dv.cross(nv))).transposed()
    R1=(frame(E-A,n)@frame(E0-A0,n0).transposed()).to_quaternion()
    R2=(frame(Wp-E,n)@frame(W0-E0,n0).transposed()).to_quaternion()
    return R1,R2,E,Wp

class Crew:
    """Builds a crew pose: body rotations (relative, in rest axes) plus limbs that reach for targets."""
    def __init__(self, who):
        self.J={k:V(v) for k,v in JOINTS[who].items()}
    def pose(self, rig, hips_t=(0,0,0), hips=I, spine=I, chest=I, head=I, arms=None, legs=None, hand_rot=None, foot_rot=None, root=I, root_t=(0,0,0), pivot=None):
        """arms/legs: side -> (target, pole) in outfit space, or None to keep the limb as modelled (following the body).
        hand_rot/foot_rot: side -> extra rotation of the hand/foot (relative to the limb); root: whole-body rotation about pivot."""
        J=self.J; arms=arms or {}; legs=legs or {}; hand_rot=hand_rot or {}; foot_rot=foot_rot or {}
        D={}; T={}
        piv=V(pivot) if pivot is not None else J['pelvis']
        # whole body: rotate about pivot, then move
        D['root']=I
        D['hips']=root@hips
        T['hips']=(root@J['pelvis']-J['pelvis'])-(root@piv-piv)+V(hips_t)+V(root_t)
        D['spine']=D['hips']@spine; D['chest']=D['spine']@chest; D['head']=D['chest']@head
        # first pass: body only, to find where the shoulders and hips are
        P,_=apply(rig,D,T)
        for s in 'LR':
            if s in arms and arms[s] is not None:
                tgt,pole=arms[s]; A=P['upper_arm.'+s].to_translation()
                R1,R2,E,Wp=two_bone(A,J['elbow.'+s],J['wrist.'+s],J['shoulder.'+s],V(tgt),V(pole))
                D['upper_arm.'+s]=R1; D['forearm.'+s]=R2; D['hand.'+s]=R2@hand_rot.get(s,I)
            else:
                D['upper_arm.'+s]=D['chest']; D['forearm.'+s]=D['chest']; D['hand.'+s]=D['chest']@hand_rot.get(s,I)
            if s in legs and legs[s] is not None:
                tgt,pole=legs[s]; A=P['thigh.'+s].to_translation()
                R1,R2,E,Wp=two_bone(A,J['knee.'+s],J['ankle.'+s],J['hip.'+s],V(tgt),V(pole))
                D['thigh.'+s]=R1; D['shin.'+s]=R2; D['foot.'+s]=foot_rot.get(s,I)
            else:
                D['thigh.'+s]=D['hips']; D['shin.'+s]=D['hips']; D['foot.'+s]=D['hips']@foot_rot.get(s,I)
        return apply(rig,D,T)

def key_rig(rig, frame):
    for pb in rig.pose.bones:
        pb.keyframe_insert('location',frame=frame); pb.keyframe_insert('rotation_quaternion',frame=frame)

def set_shapes(ob, frame, **w):
    kb=ob.data.shape_keys.key_blocks
    for n in ('Left','Right','Jump'):
        kb[n].value=max(0.0,min(1.0,w.get(n,0.0))); kb[n].keyframe_insert('value',frame=frame)
