"""Keys every sidecar clip on sidecar_rigged.blend: the outfit (travel, body, steering, suspension, wheels), the driver
and the passenger. Writes sidecar_anim.blend and clips.json (names, frame ranges, loops, props, travel).
The crew's Left / Right / Jump shapes are the owner's own poses from the model and are used as they are."""
import bpy, math, json, sys
sys.path.insert(0,'/tmp/claude-0/sc/anim')
from scrig import *

FPS=24; G=2.6; V0=3.0
bpy.ops.wm.open_mainfile(filepath=W+'sidecar_rigged.blend')
sc=bpy.context.scene; sc.render.fps=FPS
SR=bpy.data.objects['sidecar_rig']; DR=bpy.data.objects['driver_rig']; PR=bpy.data.objects['passenger_rig']
DM=bpy.data.objects['Driver']; PM=bpy.data.objects['Passenger']
CD=Crew('driver'); CP=Crew('passenger')
SJ={k:V(v) for k,v in JOINTS['sidecar'].items()}
STEER_UP=(V((0.33,0,0.95))-V((0.75,0,0.345))).normalized()

# ================================================================= the outfit
def outfit(s):
    """s: x, y, yaw (root, deg), heave, pitch (+ nose up), roll (+ chair up), byaw (body yaw), pivot, steer (+ left),
    cf / cr (front / rear compression, m, + = compressed), spin_f / spin_r / spin_c (rad)."""
    D={}; T={}
    D['root']=q(Z,s.get('yaw',0)); T['root']=(s.get('x',0),s.get('y',0),0)
    Rb=q(Z,s.get('byaw',0))@q(Y,-s.get('pitch',0))@q(X,s.get('roll',0))
    piv=V(s.get('pivot',(0,0,0)))
    D['chassis']=D['root']@Rb
    T['chassis']=D['root']@((piv-Rb@piv)+V((0,0,s.get('heave',0))))
    D['steer']=D['chassis']@q(STEER_UP,s.get('steer',0))
    cf=max(-0.07,min(0.11,s.get('cf',0))); cr=max(-0.08,min(0.14,s.get('cr',0)))
    D['front_link']=D['steer']@q(Y,-math.degrees(math.asin(cf/0.152*0.999)))
    D['front_wheel']=D['front_link']@q(Y,math.degrees(s.get('spin_f',0)))
    D['swingarm']=D['chassis']@q(Y,math.degrees(math.asin(cr/0.52)))
    D['rear_wheel']=D['swingarm']@q(Y,math.degrees(s.get('spin_r',0)))
    D['chair_wheel']=D['chassis']@q(Y,math.degrees(s.get('spin_c',0)))
    P,_=apply(SR,D,T)
    return P

def grips(P):
    """Where the driver's wrists go: the modelled wrist positions carried by the handlebars (outfit/chassis space)."""
    inv=P['chassis'].inverted(); out={}
    for sd in 'LR':
        w=V(JOINTS['driver']['wrist.'+sd]); out[sd]=inv@P['steer']@Matrix.Translation(-SJ['steer'])@w
    return out

def model_pole(c,limb,sd):
    J=c.J
    if limb=='arm': A,E,Wr=J['shoulder.'+sd],J['elbow.'+sd],J['wrist.'+sd]
    else: A,E,Wr=J['hip.'+sd],J['knee.'+sd],J['ankle.'+sd]
    return E-(A+Wr)/2

# Owner's note (16 Nov 2026): the passenger's elbows sat too low holding the rail; they are carried higher and a little out.
ELBOW_UP={'L':V((0.0,0.35,1.0)),'R':V((0.0,-0.35,1.0))}
def rest_pole(c,limb,sd,k=1.0):
    p=model_pole(c,limb,sd)
    if c is CP and limb=='arm':
        p=p.normalized().lerp(ELBOW_UP[sd].normalized(),0.8*k)
    return p

def crew_pose(c, rig, mesh, p, f, gr=None):
    """p: dict with keys (shape weights), hips_t, hips/spine/chest/head (Quaternions), arms/legs ('rest', 'grip', None or (target, pole)),
    hand_rot, root, root_t, pivot."""
    keys=p.get('keys',{})
    set_shapes(mesh,f,**keys)
    # the owner's pose shapes carry the limbs themselves; the raised elbows fade out while a shape is in use
    k=1.0-max([0.0]+[float(v) for v in keys.values()])
    def limb(kind,sd,val):
        if val is None: return None
        if val=='rest':
            return (c.J[('wrist.' if kind=='arm' else 'ankle.')+sd], rest_pole(c,kind,sd,k))
        if val=='grip': return (gr[sd], rest_pole(c,'arm',sd,k))
        tgt,pole=val
        return (V(tgt), V(pole) if pole is not None else rest_pole(c,kind,sd,k))
    arms={sd:limb('arm',sd,p.get('arms',{}).get(sd,p.get('arm_default','rest'))) for sd in 'LR'}
    legs={sd:limb('leg',sd,p.get('legs',{}).get(sd,p.get('leg_default','rest'))) for sd in 'LR'}
    c.pose(rig,hips_t=p.get('hips_t',(0,0,0)),hips=p.get('hips',I),spine=p.get('spine',I),chest=p.get('chest',I),head=p.get('head',I),
           arms=arms,legs=legs,hand_rot=p.get('hand_rot'),root=p.get('root',I),root_t=p.get('root_t',(0,0,0)),pivot=p.get('pivot'))
    key_rig(rig,f)

# ================================================================= terrain and riding
class Terrain:
    def __init__(self): self.pieces=[]
    def add(self,x0,x1,fn,name=None): self.pieces.append((x0,x1,fn,name)); return self
    def h(self,x):
        b=0.0
        for x0,x1,fn,_ in self.pieces:
            if x0<=x<=x1: b=max(b,fn(x-x0))
        return b
    def axle(self,x,r):
        b=0.0
        for i in range(-10,11):
            dx=r*i/10; b=max(b,self.h(x+dx)+math.sqrt(max(0,r*r-dx*dx)))
        return b

WX={'rear':-0.75,'front':0.75,'chair':-0.595}
def plane(dr,df,dc):
    """Body heave/pitch/roll that moves the rear, front and chair axles up by dr, df, dc."""
    b=(df-dr)/1.5; a=(df+dr)/2; cc=(dc-a+0.595*b)/1.05
    return a, math.degrees(math.atan(b)), math.degrees(math.atan(cc))

def smooth(seq,sig):
    if sig<=0: return list(seq)
    n=len(seq); out=[]
    k=int(3*sig)+1
    for i in range(n):
        s=w=0.0
        for j in range(-k,k+1):
            jj=min(n-1,max(0,i+j)); ww=math.exp(-0.5*(j/sig)**2); s+=seq[jj]*ww; w+=ww
        out.append(s/w)
    return out

def ride_over(terr, xs, sig=0.0):
    """Outfit following the ground at root positions xs: body smoothed by sig frames, suspension takes the rest."""
    raw=[]
    for x in xs:
        raw.append({k:terr.axle(x+WX[k],RW[k][1])-RW[k][1] for k in WX})
    body={k:smooth([r[k] for r in raw],sig) for k in WX}
    out=[]
    for i,x in enumerate(xs):
        a,pt,rl=plane(body['rear'][i],body['front'][i],body['chair'][i])
        out.append(dict(x=x,heave=a,pitch=pt,roll=rl,cf=body['front'][i]-raw[i]['front'],cr=body['rear'][i]-raw[i]['rear']))
    return out

def spins(xs):
    """Wheel angles from distance travelled."""
    return [dict(spin_f=x/RW['front'][1],spin_r=x/RW['rear'][1],spin_c=x/RW['chair'][1]) for x in xs]

def loop_spin(i,n,revs=(2,2,2)):
    u=i/n; return dict(spin_f=2*math.pi*revs[1]*u,spin_r=2*math.pi*revs[0]*u,spin_c=2*math.pi*revs[2]*u)

# ================================================================= clip table
CLIPS=[]; frame=[1]
def clip(name, n, fn, loop=False, kind='everyday', props=None, info=None):
    start=frame[0]
    travel=[]
    for i in range(n+1):
        f=start+i; sc.frame_set(f)
        st=fn(i)
        s=st.get('sc',{})
        P=outfit(s); key_rig(SR,f)
        gr=grips(P)
        d=dict(st.get('drv',{})); d.setdefault('arm_default','grip')
        crew_pose(CD,DR,DM,d,f,gr)
        crew_pose(CP,PR,PM,st.get('pas',{}),f,gr)
        travel.append(round(s.get('x',0.0),4))
    CLIPS.append(dict(name=name,start=start,end=start+n,frames=n,loop=loop,kind=kind,props=props or [],travel=travel,info=info or {}))
    frame[0]=start+n+6
    print('clip',name,start,start+n)

wave=lambda t,per,amp,ph=0: amp*math.sin(2*math.pi*t/per+ph)

# ---------------------------------------------------------------- everyday
def idle(i):
    t=i/FPS; br=wave(t,2.0,1)
    return dict(sc=dict(heave=-0.004+0.002*br),
                drv=dict(chest=q(Y,1.2*br),head=q(Z,4*math.sin(2*math.pi*t/2.0)),hips_t=(0,0,0.004*br)),
                pas=dict(chest=q(Y,1.5*br),head=q(Z,-6*math.sin(2*math.pi*t/2.0+1))@q(Y,2*br),hips_t=(0,0,0.005*br)))
clip('Idle',48,idle,loop=True)

def look_around(i):
    t=i/FPS
    yaw=55*bump(t,0.2,0.6,1.2,1.6)-55*bump(t,1.5,1.9,2.5,2.9)
    pyaw=-70*bump(t,0.4,0.8,1.4,1.9)+45*bump(t,1.7,2.1,2.6,2.95)
    return dict(drv=dict(head=q(Z,yaw)@q(Y,-6*bump(t,0.2,0.6,2.5,2.9)),chest=q(Z,0.3*yaw)),
                pas=dict(head=q(Z,pyaw)@q(X,8*bump(t,0.4,0.8,1.4,1.9)),chest=q(Z,0.35*pyaw)))
clip('LookAround',72,look_around)

def revving(i):
    t=i/FPS; n=24
    blip=0.5-0.5*math.cos(2*math.pi*i/n)
    vib=0.003*math.sin(2*math.pi*i/2.0)
    return dict(sc=dict(heave=-0.01*blip+vib,pitch=-0.6*blip,cr=0.02*blip),
                drv=dict(hand_rot={'R':q(Y,-28*blip)},chest=q(Y,3*blip),head=q(Y,-3*blip)),
                pas=dict(hips_t=(0,0,vib),head=q(Z,-10)@q(Y,2*blip)))
clip('Revving',24,revving,loop=True)

def thinking(i):
    t=i/FPS
    up=sm(t,0.2,0.7)*(1-sm(t,2.4,2.9))
    chin=V((0.20,0.10,1.43)); tap=0.012*math.sin(2*math.pi*t*1.5)*up
    tgt=V(JOINTS['driver']['wrist.L']).lerp(chin+V((0,0,tap)),up)
    return dict(drv=dict(arms={'L':(tgt,V((0,1,-0.5)))},head=q(X,-10*up)@q(Y,-6*up)@q(Z,12*up),chest=q(Z,6*up)),
                pas=dict(head=q(Z,-25*up)@q(X,6*up)))
clip('Thinking',72,thinking)

def talking(i):
    t=i/FPS; n=48
    g=0.5-0.5*math.cos(2*math.pi*i/n)
    nod=math.sin(2*math.pi*i/12)
    tgt=V(JOINTS['driver']['wrist.L'])+V((0.05,0.08,0.10))*g+V((0,0,0.03*math.sin(2*math.pi*i/16)))*g
    return dict(drv=dict(head=q(Z,40)@q(Y,5*nod),chest=q(Z,10),arms={'L':(tgt,V((0,1,-0.6)))},hand_rot={'L':q(X,-30*g)}),
                pas=dict(head=q(Z,-35)@q(Y,4*math.sin(2*math.pi*i/24+1)),chest=q(Z,-8)))
clip('Talking',48,talking,loop=True)

def celebration(i):
    t=i/FPS
    up=sm(t,0.15,0.5)*(1-sm(t,2.0,2.45)); pump=0.06*math.sin(2*math.pi*t*2.2)*up
    dL=V(JOINTS['driver']['shoulder.L'])+V((0.02,0.12,0.42+pump)); dR=V(JOINTS['driver']['shoulder.R'])+V((0.02,-0.12,0.42-pump))
    gL=V(JOINTS['driver']['wrist.L']); gR=V(JOINTS['driver']['wrist.R'])
    pa=V(JOINTS['passenger']['shoulder.L'])+V((0.05,0.15,0.45-pump)); pw=V(JOINTS['passenger']['wrist.L'])
    stand=sm(t,0.1,0.45)*(1-sm(t,2.0,2.45))
    return dict(sc=dict(heave=-0.01*stand),
                drv=dict(arms={'L':(gL.lerp(dL,up),V((0,1,-1))),'R':(gR.lerp(dR,up),V((0,-1,-1)))},head=q(Y,-12*up),chest=q(Y,-6*up)),
                pas=dict(keys={'Jump':stand},head=q(Y,-10*up)))
clip('Celebration',60,celebration)

def sleep_pose(w,br=0.0):
    return dict(drv=dict(hips_t=(0.03*w,0,-0.03*w),spine=q(Y,14*w),chest=q(Y,14*w+1.5*br),head=q(Y,30*w)@q(X,8*w),hand_rot={'R':q(Y,10*w)}),
                pas=dict(hips_t=(0,0,-0.03*w),spine=q(Y,10*w),chest=q(X,-8*w)@q(Y,2*br),head=q(X,-28*w)@q(Y,25*w),
                         arms={'L':(V(JOINTS['passenger']['wrist.L'])+V((-0.08,0.02,-0.12))*w,None)}))
clip('SleepStart',24,lambda i: sleep_pose(sm(i/24,0,1)))
clip('Sleeping',96,lambda i: sleep_pose(1.0,math.sin(2*math.pi*i/48)),loop=True)
def sleep_end(i):
    t=i/24; p=sleep_pose(1-sm(t,0.1,0.8))
    p['drv']['head']=p['drv']['head']@q(Z,15*bump(t,0.5,0.7,0.85,1.0)); return p
clip('SleepEnd',24,sleep_end)

def ride_off(i):
    n=36; T=n/FPS; t=i/FPS; a=V0/T            # steady push from standstill to riding speed
    x=0.5*a*t*t; acc=1-sm(t,0.6*T,T)
    s=dict(x=x,pitch=1.8*acc*sm(t,0,0.15),cr=0.03*acc,heave=-0.005*acc,**spins([x])[0])
    return dict(sc=s,drv=dict(hips_t=(0.02*acc,0,0),chest=q(Y,6*acc)),pas=dict(hips_t=(0.02*acc,0,-0.01*acc),chest=q(Y,8*acc)))
clip('RideOff',36,ride_off,kind='riding')

def ride_on(i):
    n=36; T=n/FPS; t=i/FPS; d=V0/T
    x=V0*t-0.5*d*t*t; br=bump(t,0,0.1,0.75*T,T)
    s=dict(x=x,pitch=-1.6*br,cf=0.035*br,heave=-0.006*br,**spins([x])[0])
    return dict(sc=s,drv=dict(chest=q(Y,-4*br),hips_t=(-0.015*br,0,0)),pas=dict(chest=q(Y,6*br),hips_t=(0.02*br,0,0)))
clip('RideOn',36,ride_on,kind='riding')

def riding(i,n=32):
    u=i/n; bob=math.sin(2*math.pi*2*u)
    s=dict(heave=0.004*bob,pitch=0.25*math.sin(2*math.pi*u),cf=0.006*math.sin(2*math.pi*2*u+1),cr=0.006*bob,**loop_spin(i,n))
    return s,bob
def riding_loop(i):
    s,bob=riding(i)
    return dict(sc=s,drv=dict(chest=q(Y,4),hips_t=(0,0,0.003*bob)),pas=dict(chest=q(Y,6),hips_t=(0,0,0.004*bob)))
clip('RidingLoop',32,riding_loop,loop=True,kind='riding')

# ---------------------------------------------------------------- natural riding: corners (passenger works the outfit)
def corner_left(i):
    s,bob=riding(i); s.update(steer=14,roll=-1.5,byaw=0)
    return dict(sc=s,drv=dict(keys={'Left':1.0},hips_t=(0,0,0.003*bob)),pas=dict(keys={'Left':1.0},root_t=(0,0,0.006*bob)))
clip('CornerLeft',32,corner_left,loop=True,kind='riding',info=dict(note='turning towards the chair: passenger hangs out over the chair wheel'))
def corner_right(i):
    s,bob=riding(i); s.update(steer=-14,roll=2.5)
    return dict(sc=s,drv=dict(keys={'Right':1.0},hips_t=(0,0,0.003*bob)),pas=dict(keys={'Right':1.0},root_t=(0,0,0.006*bob)))
clip('CornerRight',32,corner_right,loop=True,kind='riding',info=dict(note='turning away from the chair: passenger swings over behind the driver'))
def into_corner(side):
    def fn(i):
        n=16; w=sm(i/n,0,1); s,bob=riding(i,32)
        s.update(steer=(14 if side=='Left' else -14)*w, roll=(-1.5 if side=='Left' else 2.5)*w)
        return dict(sc=s,drv=dict(keys={side:w}),pas=dict(keys={side:w}))
    return fn
def out_of_corner(side):
    def fn(i):
        n=16; w=1-sm(i/n,0,1); s,bob=riding(i,32)
        s.update(steer=(14 if side=='Left' else -14)*w, roll=(-1.5 if side=='Left' else 2.5)*w)
        return dict(sc=s,drv=dict(keys={side:w}),pas=dict(keys={side:w}))
    return fn
clip('CornerLeftIn',16,into_corner('Left'),kind='riding'); clip('CornerLeftOut',16,out_of_corner('Left'),kind='riding')
clip('CornerRightIn',16,into_corner('Right'),kind='riding'); clip('CornerRightOut',16,out_of_corner('Right'),kind='riding')

# ---------------------------------------------------------------- tricks on the flat
def flying_chair(i):
    n=96; t=i/FPS; T=n/FPS
    lift=sm(t,0.6,1.3)*(1-sm(t,2.9,3.6)); sw=sm(t,0.1,0.6)*(1-sm(t,3.4,3.9))
    land=shock(t-3.6,0.08,0.6)
    s,bob=riding(i%32)
    s.update(roll=24*lift+1.5*land*-1,steer=-10*sw+4*lift*math.sin(2*math.pi*t*0.9),heave=-0.004*land)
    return dict(sc=s,drv=dict(keys={'Right':sw},head=q(X,-5*lift)),pas=dict(keys={'Right':sw},root_t=(0,0,0.01*land)))
clip('FlyingChair',96,flying_chair,kind='trick',info=dict(note='rides along on two wheels with the chair in the air'))

def wheelie(i):
    n=84; t=i/FPS
    up=sm(t,0.3,0.9)*(1-sm(t,2.6,3.3)); wob=1.5*math.sin(2*math.pi*t*0.8)*up
    land=shock(t-3.3,0.08,0.55)
    s,bob=riding(i%32)
    piv=V((-0.69,0.5,0.0))
    s.update(pitch=24*up+wob,pivot=(-0.70,0.5,0.0),cr=0.04*up,cf=0.06*land,heave=-0.006*land)
    return dict(sc=s,drv=dict(hips_t=(-0.05*up,0,0.02*up),chest=q(Y,-10*up),head=q(Y,10*up)),
                pas=dict(hips_t=(0.06*up,0,0.0),chest=q(Y,14*up),head=q(Y,8*up)))
clip('Wheelie',84,wheelie,kind='trick')

def power_slide(i):
    n=84; t=i/FPS
    sl=sm(t,0.3,0.9)*(1-sm(t,2.4,3.1))
    s,bob=riding(i%32)
    # the back steps out (round the front wheel) and the bars go to opposite lock
    s.update(byaw=28*sl,pivot=(0.75,0,0),steer=-22*sl,roll=-2*sl,heave=0.002*math.sin(2*math.pi*t*6)*sl)
    return dict(sc=s,drv=dict(keys={'Left':sl}),pas=dict(keys={'Left':sl}))
clip('PowerSlide',84,power_slide,kind='trick')

def passenger_wave(i):
    n=72; t=i/FPS
    up=sm(t,0.2,0.6)*(1-sm(t,2.3,2.9)); wv=math.sin(2*math.pi*t*1.6)*up
    sh=V(JOINTS['passenger']['shoulder.L']); rest=V(JOINTS['passenger']['wrist.L'])
    tgt=rest.lerp(sh+V((0.05,0.22+0.08*wv,0.38)),up)
    s,bob=riding(i%32)
    return dict(sc=s,drv=dict(chest=q(Y,4)),pas=dict(arms={'L':(tgt,V((0,1,-1)))},hand_rot={'L':q(X,25*wv)},head=q(Z,25*up)@q(X,8*up)))
clip('PassengerWave',72,passenger_wave,kind='trick')

def lost_passenger(i):
    n=108; t=i/FPS
    hit=1.0
    kick=shock(t-hit,0.06,0.5)
    up=0.0; tt=t-hit-0.05; air=0.95
    if 0<tt<air: up=1.9*tt-0.5*4.0*tt*tt          # thrown up out of the chair, back down in about a second
    up=max(0.0,up)
    inair=1.0 if 0<tt<air else 0.0
    flail=math.sin(2*math.pi*t*3.5)
    landed=shock(t-hit-0.05-air,0.07,0.6)
    look=sm(t,hit+0.15,hit+0.45)*(1-sm(t,3.2,3.8))
    s,bob=riding(i%32); s.update(heave=0.03*kick-0.012*landed,pitch=-2*kick,cf=0.05*kick,cr=-0.02*kick+0.03*landed)
    ps=V(JOINTS['passenger']['shoulder.L']); pr=V(JOINTS['passenger']['shoulder.R'])
    ar=inair*sm(tt,0,0.15)
    pas=dict(root_t=(-0.12*ar,0.0,up-0.03*landed),root=q(Y,-25*ar*math.sin(math.pi*min(1,max(0,tt/air))))@q(X,10*ar*flail),
             arms={'L':(V(JOINTS['passenger']['wrist.L']).lerp(ps+V((0.0,0.25,0.25+0.12*flail)),ar),V((0,1,-1))),
                   'R':(V(JOINTS['passenger']['wrist.R']).lerp(pr+V((0.0,-0.25,0.25-0.12*flail)),ar),V((0,-1,-1)))},
             legs={'L':(V(JOINTS['passenger']['ankle.L'])+V((0.10,0.05,0.10+0.08*flail))*ar,None),'R':(V(JOINTS['passenger']['ankle.R'])+V((0.10,-0.05,0.10-0.08*flail))*ar,None)},
             head=q(Y,-15*ar))
    # the passenger's hands and feet go with the body while airborne (targets are in the outfit's space)
    if ar>0:
        for k in ('arms','legs'):
            for sd,(tg,pl) in pas[k].items(): pas[k][sd]=(tg+V((-0.12*ar,0,up)),pl)
    return dict(sc=s,drv=dict(head=q(Z,-70*look)@q(Y,-8*look),chest=q(Z,-18*look)),pas=pas)
clip('LostPassenger',108,lost_passenger,kind='trick',info=dict(note='a big bump throws the passenger up out of the chair; they land back in'))

# ---------------------------------------------------------------- rollers (wider, bigger and further apart than Harvey's whoops)
ROLL=dict(count=4,length=1.9,height=0.38,gap=0.9)
def roller_fn(u): return ROLL['height']*(0.5-0.5*math.cos(2*math.pi*u/ROLL['length']))
def rollers_clip():
    v=2.8; x0=2.2; pitch_len=ROLL['length']+ROLL['gap']
    terr=Terrain()
    for k in range(ROLL['count']):
        a=x0+k*pitch_len; terr.add(a,a+ROLL['length'],roller_fn)
    end=x0+ROLL['count']*pitch_len
    n=int(round((end+1.8)/v*FPS))+30
    xs=[]; x=0.0
    for i in range(n+1):
        t=i/FPS; tb=(end+1.8)/v
        xs.append(v*t if t<tb else v*tb+v*(t-tb)-0.5*(v/1.2)*(t-tb)**2 if t-tb<1.2 else v*tb+v*1.2/2)
    body=ride_over(terr,xs,sig=4.0)
    def fn(i):
        s=dict(body[i]); s.update(spins([xs[i]])[0])
        pump=max(-1,min(1,(s['cf']+s['cr'])*6))
        on=1.0 if 1.4<xs[i]<end+0.5 else 0.0
        return dict(sc=s,drv=dict(keys={'Jump':0.55*sm(xs[i],1.0,2.0)*(1-sm(xs[i],end,end+0.8))},hips_t=(0,0,-0.03*pump)),
                    pas=dict(root_t=(0,0,-0.02*pump),chest=q(Y,6*on)))
    clip('Rollers',n,fn,kind='ground',props=[dict(prop='SC_Prop_Rollers',x=x0,length=end-x0-ROLL['gap'])],info=dict(rollers=ROLL,speed=v))
rollers_clip()

# ---------------------------------------------------------------- jumps
LIP=dict(height=0.5,angle=33.0)
def ramp_fn(u):
    L=2*LIP['height']/math.tan(math.radians(LIP['angle'])); return LIP['height']*(u/L)**2 if u<=L else 0.0
RAMP_LEN=2*LIP['height']/math.tan(math.radians(LIP['angle']))
LAND=dict(face=0.45,top=0.5,down=2.6,height=0.5)
def landing_fn(u):
    H=LAND['height']; a,b,c=LAND['face'],LAND['top'],LAND['down']
    if u<a: return H*sm(u,0,a)
    if u<a+b: return H
    if u<a+b+c: return H*(1-sm(u,a+b,a+b+c))
    return 0.0
LAND_LEN=LAND['face']+LAND['top']+LAND['down']

def jump_clip(name, trick, note):
    v=V0; x_ramp=2.0; x_lip=x_ramp+RAMP_LEN
    th=math.radians(LIP['angle']); vx=v*math.cos(th); vz=v*math.sin(th)
    R=2*vx*vz/G
    x_land=x_lip+R-LAND['face']-LAND['top']-0.55        # touch down on the upper part of the down slope
    terr=Terrain().add(x_ramp,x_lip,ramp_fn).add(x_land,x_land+LAND_LEN,landing_fn)
    dt=1.0/FPS; frames=[]; x=0.0; mode='ground'; t_air=None; t=0.0; z0=p0=None; land_t=None; brake_t=None
    end_x=x_land+LAND_LEN+0.4
    ground_x=[]
    while True:
        if mode=='ground':
            ground_x.append(x)
            raw={k:terr.axle(x+WX[k],RW[k][1])-RW[k][1] for k in WX}
            # past the lip the front wheel is already flying on the lip's line
            if x+WX['front']>x_lip and x+WX['rear']<x_lip:
                raw['front']=LIP['height']+math.tan(th)*(x+WX['front']-x_lip)-0.0
            a,pt,rl=plane(raw['rear'],raw['front'],raw['chair'])
            frames.append(dict(x=x,heave=a,pitch=pt,roll=rl,cf=0.0,cr=0.0,mode='ground',t=t))
            if x+WX['rear']>=x_lip and t_air is None:
                mode='air'; t_air=t; z0=a; p0=pt
            spd=v
            if land_t is not None and x>end_x:
                if brake_t is None: brake_t=t
                spd=max(0.0,v*(1-(t-brake_t)/1.0))
                if spd==0.0: break
            x+=spd*dt
        else:
            ta=t-t_air; xa=x
            z=z0+vz*ta-0.5*G*ta*ta
            T_air=R/vx
            ptc=lerp(p0,-22.0,sm(ta,0.15*T_air,0.95*T_air))
            frames.append(dict(x=xa,heave=z,pitch=ptc,roll=0.0,cf=-0.06*sm(ta,0,0.2),cr=-0.07*sm(ta,0,0.2),mode='air',t=t,ta=ta))
            x+=vx*dt
            # touch down when a wheel meets the landing
            hit=False
            for k in ('front','rear'):
                wx=x+WX[k]*math.cos(math.radians(ptc)); wz=z+vz*dt-0.5*G*(ta+dt)**2+0.5*G*ta*ta  # next frame height
                axz=RW[k][0].z+ (z+vz*dt-0.5*G*(2*ta*dt+dt*dt)) + WX[k]*math.sin(math.radians(ptc))
                if axz-RW[k][1] <= terr.axle(wx,RW[k][1])-RW[k][1]+RW[k][0].z-RW[k][1]+0.0 and ta>0.3: hit=True
            if hit: mode='ground'; land_t=t+dt
        t+=dt
        if t>12: break
    n=len(frames)-1
    # landing: big squash through the suspension and the crew's legs
    for f in frames:
        if land_t is not None and f['t']>=land_t-1e-6:
            k=shock(f['t']-land_t,0.10,0.9)
            f['cf']+=0.10*k; f['cr']+=0.12*k; f['heave']-=0.05*k; f['squash']=k
        else: f['squash']=0.0
    air0=t_air; air1=land_t
    def fn(i):
        f=frames[i]; s={k:f[k] for k in ('x','heave','pitch','roll','cf','cr')}; s.update(spins([f['x']])[0])
        ta=f['t']-air0 if air0 is not None else -1; T=(air1-air0) if air1 else 1.0
        u=ta/T if f['mode']=='air' else (-1 if f['t']<air0 else 2)
        stand=sm(f['t'],air0-0.9,air0-0.4)*(1-sm(f['t'],air1+0.5,air1+1.2))
        sq=f['squash']
        st=dict(sc=s,drv=dict(keys={'Jump':stand*(1-0.8*max(0,sq))},hips_t=(0,0,-0.05*max(0,sq))),
                pas=dict(keys={'Jump':0.6*stand*(1-max(0,sq))},root_t=(0,0,-0.04*max(0,sq))))
        trick(st,u,f,T)
        return st
    lip_info=dict(ramp_x=x_ramp,lip_x=x_lip,landing_x=x_land,take_off_t=round(air0,3),land_t=round(air1,3),air_time=round(air1-air0,3))
    clip(name,n,fn,kind='jump',props=[dict(prop='SC_Prop_JumpRamp',x=x_ramp,length=RAMP_LEN),dict(prop='SC_Prop_JumpLanding',x=x_land,length=LAND_LEN)],
         info=dict(note=note,**lip_info))

def mid(u,a=0.12,b=0.35,c=0.62,d=0.86): return bump(u,a,b,c,d) if 0<=u<=1 else 0.0

def t_tabletop(st,u,f,T):
    w=mid(u,0.1,0.38,0.58,0.88)
    st['sc']['roll']+=-32*w; st['sc']['byaw']=14*w; st['sc']['pivot']=(0,0.5,0.6)
    st['drv']['keys']['Right']=0.0
    st['pas']['keys']={'Right':w,'Jump':st['pas']['keys']['Jump']*(1-w)}
jump_clip('Tabletop',t_tabletop,'lays the whole outfit over sideways in the air, then a big squash on landing')

def t_nohander(st,u,f,T):
    w=mid(u,0.15,0.35,0.6,0.82)
    J=JOINTS['driver']
    st['drv']['arms']={'L':(V(J['shoulder.L'])+V((0.0,0.40,0.10)),V((0,0,-1))),'R':(V(J['shoulder.R'])+V((0.0,-0.40,0.10)),V((0,0,-1)))} if w>0 else {}
    if w>0:
        st['drv']['keys']['Jump']*=(1-w)
        for sd,g in (('L',V(J['wrist.L'])),('R',V(J['wrist.R']))):
            tg,pl=st['drv']['arms'][sd]; st['drv']['arms'][sd]=(g.lerp(tg,w),pl)
        st['drv']['head']=q(Y,-10*w); st['drv']['chest']=q(Y,-8*w)
jump_clip('NoHander',t_nohander,'the driver lets go of the bars and spreads both arms in the air')

def t_superman(st,u,f,T):
    w=mid(u,0.12,0.36,0.6,0.86)
    if w<=0: return
    J=JOINTS['passenger']
    st['pas']['keys']={'Jump':st['pas']['keys']['Jump']*(1-w)}
    # body swings up and out behind, hanging on to the grab rail
    hold=(V(J['wrist.L'])+V(J['wrist.R']))/2
    st['pas']['root']=q(Y,-62*w)@q(X,-12*w); st['pas']['pivot']=tuple(hold)
    st['pas']['arms']={'L':(V(J['wrist.L']),None),'R':(V(J['wrist.R']),None)}
    pel=V(J['pelvis']); Rw=q(Y,-62*w)@q(X,-12*w)
    for sd in 'LR':
        a=V(J['ankle.'+sd]); tgt=hold+Rw@(pel-hold)+ (Rw@V((-0.55,0.08 if sd=='L' else -0.08,0.0)))
        st['pas'].setdefault('legs',{})[sd]=(V(J['ankle.'+sd]).lerp(tgt,w),V((0,0,-1)))
    st['pas']['head']=q(Y,-35*w)
    st['drv']['head']=q(Z,-20*w)
jump_clip('PassengerSuperman',t_superman,'the passenger lets their body fly out flat behind, holding on by the hands')

def t_nacnac(st,u,f,T):
    w=mid(u,0.12,0.36,0.6,0.86)
    if w<=0: return
    J=JOINTS['driver']; P_=JOINTS['passenger']
    st['drv']['keys']['Jump']*=(1-w)
    st['drv']['legs']={'R':(V(J['ankle.R']).lerp(V((-0.45,0.10,0.95)),w),V((0,0.3,1)))}
    st['drv']['hips_t']=(0,0.04*w,0.03*w); st['drv']['chest']=q(X,6*w); st['drv']['head']=q(Z,-25*w)
    st['pas']['keys']['Jump']*=(1-w)
    st['pas']['legs']={'L':(V(P_['ankle.L']).lerp(V((-0.45,1.30,0.75)),w),V((1,0,0.3)))}
    st['pas']['head']=q(Z,25*w)
jump_clip('NacNac',t_nacnac,'both kick a leg out: the driver over the back, the passenger out over the chair')

json.dump(dict(fps=FPS,clips=CLIPS,rollers=ROLL,lip=LIP,landing=LAND,ramp_length=RAMP_LEN,landing_length=LAND_LEN),open(W+'clips.json','w'),indent=1)
sc.frame_start=1; sc.frame_end=frame[0]
bpy.ops.wm.save_as_mainfile(filepath=W+'sidecar_anim.blend')
print('anim saved, last frame',frame[0])
