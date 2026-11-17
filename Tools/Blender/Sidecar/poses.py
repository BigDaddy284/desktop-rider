"""Pose library for the sidecar crew, built on the rig (no shape keys). Every pose is a dict:
hips_t (m), root (whole-body turn about pivot), hips / spine / chest / head (Quaternions),
arms / legs: side -> (target, pole) in the outfit's space. blend() mixes two poses.
Poses are matched to the owner's race photos (References/Original-Photographs/Sidecar) - see ASSET_INDEX.md."""
import math
from scrig import *

PJ={k:V(v) for k,v in JOINTS['passenger'].items()}
DJ={k:V(v) for k,v in JOINTS['driver'].items()}
# the passenger's grab rail, from the bike end (by the seat) out to the front of the chair (outfit space)
RAIL=[V(p) for p in [(-0.48,0.15,0.97),(-0.435,0.178,1.06),(-0.345,0.213,1.09),(-0.28,0.374,1.075),
                     (-0.183,0.675,0.985),(-0.02,0.838,0.955),(0.06,0.963,0.925),(0.088,0.98,0.78),(0.11,0.945,0.60)]]
def rail(t, up=0.03):
    t=max(0.0,min(len(RAIL)-1.001,t)); i=int(t); return RAIL[i].lerp(RAIL[i+1],t-i)+V((0,0,up))
FLOOR=0.40   # ankle height with the boot on the platform

def model_limb(J,kind,sd):
    if kind=='arm': A,E,W=J['shoulder.'+sd],J['elbow.'+sd],J['wrist.'+sd]
    else: A,E,W=J['hip.'+sd],J['knee.'+sd],J['ankle.'+sd]
    return (W.copy(),E-(A+W)/2)

def P(J, **k):
    p=dict(hips_t=V((0,0,0)),root=I,hips=I,spine=I,chest=I,head=I,pivot=None,arms={},legs={},hand_rot={})
    for kk,v in k.items(): p[kk]=v
    p['hips_t']=V(p['hips_t'])
    for kind,key in (('arm','arms'),('leg','legs')):
        d=dict(p[key])
        for sd in 'LR':
            if sd not in d or d[sd] is None or d[sd]=='rest': d[sd]=model_limb(J,kind,sd)
            else: d[sd]=(V(d[sd][0]),V(d[sd][1]))
        p[key]=d
    return p

def blend(a,b,w):
    if w<=0: return a
    if w>=1: return b
    o=dict(a)
    o['hips_t']=a['hips_t'].lerp(b['hips_t'],w)
    for k in ('root','hips','spine','chest','head'): o[k]=a[k].slerp(b[k],w)
    for key in ('arms','legs'):
        o[key]={sd:(a[key][sd][0].lerp(b[key][sd][0],w),a[key][sd][1].lerp(b[key][sd][1],w)) for sd in 'LR'}
    hr={}
    for sd in set(a['hand_rot'])|set(b['hand_rot']): hr[sd]=a['hand_rot'].get(sd,I).slerp(b['hand_rot'].get(sd,I),w)
    o['hand_rot']=hr
    return o

def add(p, **k):
    """Layer small changes on a pose: extra rotations are applied after the pose's own, vectors are added."""
    o=dict(p); o['arms']=dict(p['arms']); o['legs']=dict(p['legs']); o['hand_rot']=dict(p['hand_rot'])
    for kk,v in k.items():
        if kk in ('root','hips','spine','chest','head'): o[kk]=p[kk]@v
        elif kk=='hips_t': o[kk]=p[kk]+V(v)
        elif kk in ('arms','legs'):
            for sd,val in v.items(): o[kk][sd]=(V(val[0]),V(val[1]) if val[1] is not None else p[kk][sd][1])
        elif kk=='hand_rot': o[kk].update(v)
        else: o[kk]=v
    return o

# ------------------------------------------------------------------ passenger
# Riding: crouched low and forward over the rail, head down between the hands, elbows out wide
# (owner's race photo, 16 Nov 2026).
def pas_ride():
    return P(PJ, hips_t=(-0.06,0,-0.03), spine=q(Y,12.5), chest=q(Y,12.5)@q(Z,-10), head=q(Z,10)@q(Y,-22.5),
             arms={'L':(rail(4.4),(-0.3,1,0.3)),'R':(rail(3.0),(-0.3,-1,0.3))})

def aim(J, d, twist=0.0):
    """Whole-body turn that points the body (pelvis to neck) along d, plus a twist about that line."""
    d0=(J['neck']-J['pelvis']).normalized(); d=V(d).normalized()
    return q(d,twist)@d0.rotation_difference(d)

# Turning towards the chair (left): the passenger hangs out over the chair wheel, bum low and out,
# the bike-side arm stretched to the rail, the other hand on the front hoop (race photos 1, 4, 5 and 6).
def pas_hang():
    return P(PJ, hips_t=(-0.08,0.30,-0.17), root=aim(PJ,(0.22,0.12,0.97),-10), spine=q(Y,10), chest=q(Y,8)@q(Z,-12), head=q(Z,35)@q(Y,-15),
             arms={'L':((-0.33,0.97,0.78),(-0.3,1,0.3)),'R':(rail(3.7),(-0.2,-0.3,-1))},
             legs={'L':((-0.50,0.78,FLOOR+0.02),(1,0.1,0)),'R':((-0.64,0.56,FLOOR+0.02),(1,0,0))})

# Turning away from the chair (right): the passenger swings over the back of the bike behind the driver,
# chest down over the seat, far hand on the bike's right side, near hand on the rail post (race photos 9 and 11).
def pas_swing():
    return P(PJ, hips_t=(-0.04,-0.20,0.14), root=aim(PJ,(0.35,-0.80,0.40),-20), spine=q(Y,10), chest=q(Y,8), head=q(Z,-25)@q(Y,-30),
             arms={'L':(rail(1.4),(-0.3,0.5,0.6)),'R':((-0.40,-0.16,0.84),(-0.3,-0.3,1))},
             legs={'L':((-0.36,0.42,FLOOR),(1,0.3,0)),'R':((-0.62,0.27,FLOOR),(1,-0.2,0))})

# Standing tall, arms straight down to the rail (race photo 7: chair wheel up over the bumps) - used for the wheelie.
def pas_stand():
    return P(PJ, hips_t=(0.04,0,0.22), spine=q(Y,-2), chest=q(Y,2), head=q(Y,-4),
             arms={'L':(rail(4.5),(-0.6,1,0)),'R':(rail(2.9),(-0.6,-1,0))})

# ------------------------------------------------------------------ driver
def drv_ride(gr):
    return P(DJ, arms={'L':(gr['L'],model_limb(DJ,'arm','L')[1]),'R':(gr['R'],model_limb(DJ,'arm','R')[1])})
def drv_lean(gr,side):
    """Turning: the driver leans into the turn and looks through it, hands on the bars."""
    s=1 if side=='L' else -1
    return P(DJ, hips_t=(0,0.03*s,0), root=q(X,-9*s), spine=q(Y,6), chest=q(Z,8*s), head=q(Z,18*s)@q(X,6*s),
             arms={'L':(gr['L'],(0,1,-0.6)),'R':(gr['R'],(0,-1,-0.6))},
             legs={'L':'rest','R':'rest'})

def body_point(p, J, joint):
    """Where a body joint ends up for pose p (body only: root/hips rotation and hips_t), outfit space."""
    pel=J['pelvis']; piv=V(p['pivot']) if p.get('pivot') is not None else pel
    R=p['root']@p['hips']
    base=p['root']@pel-(p['root']@piv-piv)+p['hips_t']
    return base+R@(J[joint]-pel)

# Thrown out of the chair and hanging on: body out behind the chair wheel, arms at full stretch to the rail,
# legs trailing (the lost-passenger move).
def pas_dragged(flap=0.0):
    p=P(PJ, hips_t=(-0.32,0.40,-0.10), root=aim(PJ,(0.85,-0.25,0.45),15), spine=q(Y,-6), chest=q(Y,-6), head=q(Y,-30)@q(Z,10),
        arms={'L':(rail(4.7),(-0.2,1,0.5)),'R':(rail(3.6),(-0.2,-1,0.6))})
    for sd,dy in (('L',0.07),('R',-0.07)):
        h=body_point(p,PJ,'hip.'+sd)
        p['legs'][sd]=(h+V((-0.68,dy+0.08,-0.30+0.10*flap*(1 if sd=='L' else -1))),V((0,0,-1)))
    return p

# Flying flat out behind, holding on by the hands (the passenger superman jump trick).
def pas_superman(kick=0.0):
    p=P(PJ, hips_t=(-0.28,0.0,0.22), root=aim(PJ,(1.0,0.0,0.12),0), spine=q(Y,-8), chest=q(Y,-6), head=q(Y,-45),
        arms={'L':(rail(4.3),(0,1,0.3)),'R':(rail(3.1),(0,-1,0.3))})
    for sd,dy in (('L',0.10),('R',-0.10)):
        h=body_point(p,PJ,'hip.'+sd)
        p['legs'][sd]=(h+V((-0.76,dy,0.04+0.05*kick*(1 if sd=='L' else -1))),V((0,0,-1)))
    return p

def drv_attack(gr):
    """Up off the seat, chest over the bars, elbows up: rough ground and jumps."""
    return P(DJ, hips_t=(0.03,0,0.08), spine=q(Y,8), chest=q(Y,10), head=q(Y,-14),
             arms={'L':(gr['L'],(0,1,0.25)),'R':(gr['R'],(0,-1,0.25))}, legs={'L':'rest','R':'rest'})
