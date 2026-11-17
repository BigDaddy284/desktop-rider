"""Keys every sidecar clip on sidecar_rigged.blend: the outfit (travel, body, steering, suspension, wheels), the driver
and the passenger. Writes sidecar_anim.blend and clips.json (names, frame ranges, loops, props, travel).
Second pass (17 Nov 2026): every crew pose is built on the rig from poses.py, matched to the owner's race photos;
the outfit runs at race speed with real gravity; every frame is checked for wheels sinking into the ground or the jumps."""
import bpy, math, json, sys
sys.path.insert(0,'/tmp/claude-0/sc/anim')
from scrig import *
from poses import *

FPS=24; G=9.81; V0=6.3          # V0: riding speed (m/s), about 14 mph
bpy.ops.wm.open_mainfile(filepath=W+'sidecar_rigged.blend')
sc=bpy.context.scene; sc.render.fps=FPS
SR=bpy.data.objects['sidecar_rig']; DR=bpy.data.objects['driver_rig']; PR=bpy.data.objects['passenger_rig']
DM=bpy.data.objects['Driver']; PM=bpy.data.objects['Passenger']
CD=Crew('driver'); CP=Crew('passenger')
SJ={k:V(v) for k,v in JOINTS['sidecar'].items()}
STEER_UP=(V((0.33,0,0.95))-V((0.75,0,0.345))).normalized()

# ================================================================= the outfit
def outfit(s):
    """s: x, y, yaw (root, deg), heave, pitch (+ nose up), roll (+ chair up), byaw (body yaw), pivot, paxis (pitch axis, default across the bike), steer (+ left),
    cf / cr (front / rear compression, m, + = compressed), spin_f / spin_r / spin_c (rad)."""
    D={}; T={}
    D['root']=q(Z,s.get('yaw',0)); T['root']=(s.get('x',0),s.get('y',0),0)
    Rb=q(Z,s.get('byaw',0))@q(V(s.get('paxis',(0,1,0))),-s.get('pitch',0))@q(X,s.get('roll',0))
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

def put(c, rig, mesh, p, f):
    set_shapes(mesh,f)      # the model's own Left / Right / Jump shapes stay at zero: every pose is on the rig
    c.pose(rig,hips_t=p['hips_t'],hips=p['hips'],spine=p['spine'],chest=p['chest'],head=p['head'],arms=p['arms'],legs=p['legs'],
           hand_rot=p['hand_rot'],root=p['root'],pivot=p['pivot'])
    key_rig(rig,f)

# ================================================================= ground, suspension and flight
class Terrain:
    def __init__(self): self.pieces=[]
    def add(self,x0,x1,fn,name=None): self.pieces.append((x0,x1,fn,name)); return self
    def h(self,x):
        b=0.0
        for x0,x1,fn,_ in self.pieces:
            if x0<=x<=x1: b=max(b,fn(x-x0))
        return b
    def axle(self,x,r):
        """Height of a wheel's centre resting on the ground at x (the tyre touches wherever the ground is highest)."""
        b=0.0
        for i in range(-12,13):
            dx=r*i/12; b=max(b,self.h(x+dx)+math.sqrt(max(0,r*r-dx*dx)))
        return b
FLAT=Terrain()

WX={'rear':-0.75,'front':0.75,'chair':-0.595}
SUSP={'front':(0.07,0.11),'rear':(0.08,0.14),'chair':(0.0,0.0)}     # (most extension, most compression), metres
def plane(dr,df,dc):
    """Body heave/pitch/roll that moves the rear, front and chair axles up by dr, df, dc."""
    b=(df-dr)/1.5; a=(df+dr)/2; cc=(dc-a+0.595*b)/1.05
    return a, math.degrees(math.atan(b)), math.degrees(math.atan(cc))

def smooth(seq,sig):
    if sig<=0: return list(seq)
    n=len(seq); out=[]; k=int(3*sig)+1
    for i in range(n):
        s=w=0.0
        for j in range(-k,k+1):
            jj=min(n-1,max(0,i+j)); ww=math.exp(-0.5*(j/sig)**2); s+=seq[jj]*ww; w+=ww
        out.append(s/w)
    return out

def drive(terr, xs, sig=2.0, land=0.07):
    """The outfit driven over the ground at root positions xs (one per frame). Each axle's sprung point follows the
    smoothed ground but can never fall faster than gravity allows, so the outfit flies off crests and lips and lands
    with a squash; the suspension takes up the rest and the body is pushed up when a wheel bottoms out.
    Returns per frame: x, heave, pitch, roll, cf, cr, air (wheels off the ground) and squash (0..1)."""
    n=len(xs); dt=1.0/FPS
    raw={k:[terr.axle(x+WX[k],RW[k][1])-RW[k][1] for x in xs] for k in WX}
    tgt={k:smooth(raw[k],sig) for k in WX}
    body={k:[0.0]*n for k in WX}; air={k:[False]*n for k in WX}; squash=[0.0]*n
    hits=[]
    for k in WX:
        ext,comp=SUSP[k]; b=tgt[k][0]; vb=0.0; flying=False
        for i in range(n):
            bal=b+vb*dt-G*dt*dt if i else b
            nb=max(tgt[k][i],bal,raw[k][i]-comp)
            if i: vb=(nb-b)/dt
            fl=nb>tgt[k][i]+0.01 and nb>raw[k][i]-comp+0.005
            if flying and not fl and k!='chair': hits.append((i,max(0.0,vb-vb_prev)))
            vb_prev=vb; flying=fl; b=nb; body[k][i]=b; air[k][i]=fl
    # landings: a squash through the suspension and the crew's legs, bigger for a harder hit
    for i0,v in hits:
        amp=min(1.0,v/4.0)
        if amp<0.15: continue
        for i in range(i0,n):
            sq=shock((i-i0)*dt,0.08,0.7)*amp
            if abs(sq)<1e-4 and i>i0+4: break
            squash[i]=max(squash[i],sq) if sq>0 else squash[i]+sq*0.3
    out=[]
    for i,x in enumerate(xs):
        b={k:body[k][i]-(land*squash[i] if k!='chair' else land*squash[i]*0.6) for k in WX}
        c={}
        for k in ('front','rear'):
            ext,comp=SUSP[k]
            w=max(raw[k][i],b[k]-ext)                 # the wheel hangs at full extension in the air
            c[k]=min(comp,w-b[k]); b[k]=max(b[k],w-comp)
        b['chair']=max(b['chair'],raw['chair'][i])
        a,pt,rl=plane(b['rear'],b['front'],b['chair'])
        out.append(dict(x=x,heave=a,pitch=pt,roll=rl,cf=c['front'],cr=c['rear'],
                        air=air['front'][i] and air['rear'][i],rear_air=air['rear'][i],squash=squash[i]))
    return out

def loop_spin(i,n,v=None):
    """Wheel turns for an on-the-spot loop: whole turns per loop, as near the riding speed as possible."""
    d=(v or V0)*n/FPS; u=i/n; out={}
    for k,key in (('front','spin_f'),('rear','spin_r'),('chair','spin_c')):
        revs=max(1,round(d/(2*math.pi*RW[k][1]))); out[key]=2*math.pi*revs*u
    return out
def spins(x,slip=0.0):
    return dict(spin_f=x/RW['front'][1],spin_r=x/RW['rear'][1]+slip,spin_c=x/RW['chair'][1])

# ================================================================= clip table and checks
CLIPS=[]; frame=[1]; CHECK=[]
def clip(name, n, fn, loop=False, kind='everyday', props=None, info=None, terr=None):
    """fn(i) -> dict(sc=outfit state, drv=pose or fn(grips)->pose, pas=pose or fn(grips)->pose)."""
    start=frame[0]; terr=terr or FLAT
    travel=[]; low=(9,None); fixes=0.0
    for i in range(n+1):
        f=start+i; sc.frame_set(f)
        st=fn(i); s=st.get('sc',{})
        P=outfit(s); fix=0.0
        for it in range(4):         # tyres may squash 8 mm; anything deeper is taken up by the suspension, then the body
            gaps={k:P[bone].to_translation().z-terr.axle(P[bone].to_translation().x,RW[k][1]) for k,bone in (('rear','rear_wheel'),('front','front_wheel'),('chair','chair_wheel'))}
            need={k:-0.008-g for k,g in gaps.items() if g<-0.008}
            if not need: break
            fix=max(fix,max(need.values()))
            for k,d in need.items():
                if k=='chair': s['roll']=s.get('roll',0)+math.degrees(math.atan(d/1.05))
                else:
                    key='cf' if k=='front' else 'cr'; room=SUSP[k][1]-s.get(key,0)
                    take=max(0.0,min(room,d)); s[key]=s.get(key,0)+take
                    if d-take>0: s['heave']=s.get('heave',0)+(d-take)
            P=outfit(s)
        key_rig(SR,f); fixes=max(fixes,fix)
        gr=grips(P)
        d=st.get('drv'); d=d(gr) if callable(d) else (d or drv_ride(gr))
        p=st.get('pas'); p=p(gr) if callable(p) else (p or pas_ride())
        put(CD,DR,DM,d,f); put(CP,PR,PM,p,f)
        travel.append(round(s.get('x',0.0),4))
        for k,bone in (('rear','rear_wheel'),('front','front_wheel'),('chair','chair_wheel')):
            c=P[bone].to_translation(); gap=c.z-terr.axle(c.x,RW[k][1])
            if gap<low[0]: low=(gap,(i,k))
    CHECK.append((name,round(low[0],3),low[1],round(fixes,3)))
    CLIPS.append(dict(name=name,start=start,end=start+n,frames=n,loop=loop,kind=kind,props=props or [],travel=travel,info=info or {}))
    frame[0]=start+n+6
    print('clip',name,start,start+n,'lowest wheel gap %.3f'%low[0],low[1],'ground fix %.3f'%fixes)

wave=lambda t,per,amp,ph=0: amp*math.sin(2*math.pi*t/per+ph)

def track(t, keys):
    """Pose at time t from (time, pose) keys, eased between neighbours. A key's pose may be a function of t."""
    ev=lambda p: p(t) if callable(p) else p
    if t<=keys[0][0]: return ev(keys[0][1])
    for (t0,a),(t1,b) in zip(keys,keys[1:]):
        if t<=t1: return blend(ev(a),ev(b),sm(t,t0,t1))
    return ev(keys[-1][1])

def lift(p, **k):
    """add() with arm targets given as offsets from the pose's own."""
    if 'arms_by' in k:
        by=k.pop('arms_by'); k['arms']={sd:(p['arms'][sd][0]+V(o),None) for sd,o in by.items()}
    return add(p,**k)

PR0=pas_ride(); PH=pas_hang(); PS=pas_swing(); PST=pas_stand()
def Dr(**m): return (lambda gr: add(drv_ride(gr),**m)) if m else drv_ride

# ---------------------------------------------------------------- everyday (standing still)
def idle(i):
    t=i/FPS; br=wave(t,2.0,1)
    return dict(sc=dict(heave=-0.004+0.002*br),
                drv=Dr(chest=q(Y,1.2*br),head=q(Z,4*math.sin(2*math.pi*t/2.0)),hips_t=(0,0,0.004*br)),
                pas=lift(PR0,chest=q(Y,1.5*br),head=q(Z,-6*math.sin(2*math.pi*t/2.0+1)),hips_t=(0,0,0.005*br)))
clip('Idle',48,idle,loop=True)

def look_around(i):
    t=i/FPS
    yaw=55*bump(t,0.2,0.6,1.2,1.6)-55*bump(t,1.5,1.9,2.5,2.9)
    pyaw=-60*bump(t,0.4,0.8,1.4,1.9)+45*bump(t,1.7,2.1,2.6,2.95)
    return dict(drv=Dr(head=q(Z,yaw)@q(Y,-6*bump(t,0.2,0.6,2.5,2.9)),chest=q(Z,0.3*yaw)),
                pas=lift(PR0,head=q(Z,pyaw)@q(Y,-8*bump(t,0.4,0.8,1.4,1.9)),chest=q(Z,0.3*pyaw)))
clip('LookAround',72,look_around)

def revving(i):
    n=24; blip=0.5-0.5*math.cos(2*math.pi*i/n); vib=0.003*math.sin(2*math.pi*i/2.0)
    return dict(sc=dict(heave=-0.01*blip+vib,pitch=-0.6*blip,cr=0.02*blip),
                drv=Dr(hand_rot={'R':q(Y,-28*blip)},chest=q(Y,3*blip),head=q(Y,-3*blip)),
                pas=lift(PR0,hips_t=(0,0,vib),head=q(Z,-10)@q(Y,2*blip)))
clip('Revving',24,revving,loop=True)

def thinking(i):
    t=i/FPS; up=sm(t,0.2,0.7)*(1-sm(t,2.4,2.9))
    chin=V((0.20,0.10,1.43)); tap=0.012*math.sin(2*math.pi*t*1.5)*up
    def drv(gr): return add(drv_ride(gr),arms={'L':(gr['L'].lerp(chin+V((0,0,tap)),up),(0,1,-0.5))},
                            head=q(X,-10*up)@q(Y,-6*up)@q(Z,12*up),chest=q(Z,6*up))
    return dict(drv=drv,pas=lift(PR0,head=q(Z,-25*up)@q(X,6*up)))
clip('Thinking',72,thinking)

def talking(i):
    t=i/FPS; n=48; g=0.5-0.5*math.cos(2*math.pi*i/n); nod=math.sin(2*math.pi*i/12)
    def drv(gr):
        tgt=gr['L']+V((-0.05,0.10,0.12))*g+V((0,0,0.03*math.sin(2*math.pi*i/16)))*g
        return add(drv_ride(gr),head=q(Z,40)@q(Y,5*nod),chest=q(Z,10),arms={'L':(tgt,(0,1,-0.6))},hand_rot={'L':q(X,-30*g)})
    return dict(drv=drv,pas=lift(PR0,head=q(Z,-35)@q(Y,4*math.sin(2*math.pi*i/24+1)),chest=q(Z,-8)))
clip('Talking',48,talking,loop=True)

def celebration(i):
    """Both throw their left fist in the air together: the driver keeps the right hand on the throttle, the passenger
    comes up off his haunches with the right hand on the rail (owner's note, 16 Nov 2026)."""
    t=i/FPS; up=sm(t,0.15,0.5)*(1-sm(t,2.0,2.45)); pump=0.07*math.sin(2*math.pi*t*2.2)*up
    def drv(gr):
        sh=V(JOINTS['driver']['shoulder.L'])
        return add(drv_ride(gr),arms={'L':(gr['L'].lerp(sh+V((0.06,0.10,0.44+pump)),up),(-0.3,1,-0.2))},
                   head=q(Y,-12*up),chest=q(Y,-5*up)@q(X,-4*up))
    p=blend(PR0,PST,0.55*up)
    sh=body_point(p,PJ,'shoulder.L')
    p=add(p,arms={'L':(p['arms']['L'][0].lerp(sh+V((0.06,0.12,0.44+pump)),up),(-0.3,1,-0.2))},head=q(Y,-14*up))
    return dict(sc=dict(heave=-0.008*up),drv=drv,pas=p)
clip('Celebration',60,celebration)

def sleep_pose(w,br=0.0):
    return dict(drv=Dr(hips_t=(0.03*w,0,-0.03*w),spine=q(Y,14*w),chest=q(Y,14*w+1.5*br),head=q(Y,30*w)@q(X,8*w),hand_rot={'R':q(Y,10*w)}),
                pas=lift(PR0,hips_t=(0,0,-0.04*w),spine=q(Y,6*w),chest=q(X,-6*w)@q(Y,2*br),head=q(Y,24*w)@q(X,-20*w)))
clip('SleepStart',24,lambda i: sleep_pose(sm(i/24,0,1)))
clip('Sleeping',96,lambda i: sleep_pose(1.0,math.sin(2*math.pi*i/48)),loop=True)
def sleep_end(i):
    t=i/24; w=1-sm(t,0.1,0.8); p=sleep_pose(w)
    jolt=bump(t,0.5,0.6,0.75,0.95)
    p['drv']=Dr(hips_t=(0.03*w,0,-0.03*w),spine=q(Y,14*w),chest=q(Y,14*w),head=q(Y,30*w)@q(X,8*w)@q(Z,15*jolt))
    return p
clip('SleepEnd',24,sleep_end)

# ---------------------------------------------------------------- riding
def ride_off(i):
    """From standstill to riding speed in 1.5 s: the back squats, the rear tyre spins up and the crew lean into it."""
    T=1.5; t=i/FPS; a=V0/T
    x=0.5*a*t*t if t<T else 0.5*a*T*T+V0*(t-T)
    acc=1-sm(t,0.8*T,T+0.2); go=sm(t,0,0.12)*acc
    s=dict(x=x,pitch=1.6*go,cr=0.04*go,heave=-0.006*go,**spins(x,slip=2.2*sm(t,0,0.6)))
    return dict(sc=s,drv=lambda gr: blend(drv_ride(gr),add(drv_attack(gr),hips_t=(0,0,-0.04)),0.7*go),
                pas=lift(PR0,hips_t=(0.03*go,0,-0.01*go),chest=q(Y,6*go)))
clip('RideOff',42,ride_off,kind='riding')

def ride_on(i):
    """Riding speed to a stop in 1.5 s on the brakes: the nose dives and the crew brace back."""
    T=1.5; t=i/FPS; d=V0/T
    x=V0*t-0.5*d*t*t if t<T else V0*T-0.5*d*T*T
    br=bump(t,0,0.15,0.85*T,T+0.15); settle=shock(t-T,0.08,0.6)
    s=dict(x=x,pitch=-1.8*br+0.8*settle,cf=0.05*br-0.02*settle,heave=-0.008*br,**spins(x))
    return dict(sc=s,drv=Dr(chest=q(Y,-5*br),hips_t=(-0.02*br,0,0)),pas=lift(PR0,hips_t=(-0.03*br,0,0),chest=q(Y,-6*br),head=q(Y,4*br)))
clip('RideOn',48,ride_on,kind='riding')

def riding(i,n=32,rough=1.0):
    """On-the-spot riding loop at race speed: chatter from the ground, small pitches, everything in whole cycles."""
    u=i/n; c1=math.sin(2*math.pi*2*u); c2=math.sin(2*math.pi*5*u+0.7); c3=math.sin(2*math.pi*u+1.3)
    s=dict(heave=rough*(0.006*c1+0.003*c2),pitch=rough*(0.5*c3+0.2*c2),roll=rough*0.4*math.sin(2*math.pi*3*u),
           cf=rough*(0.012*math.sin(2*math.pi*4*u+1)+0.006*c2),cr=rough*(0.012*c1+0.005*c2),**loop_spin(i,n))
    return s,c1,c2
def riding_loop(i):
    s,c1,c2=riding(i)
    return dict(sc=s,drv=Dr(chest=q(Y,4),hips_t=(0,0,0.006*c1)),pas=lift(PR0,hips_t=(0,0,0.008*c1-0.004*c2),head=q(Y,2*c2)))
clip('RidingLoop',32,riding_loop,loop=True,kind='riding')

# ---------------------------------------------------------------- natural riding: corners (the passenger works the outfit)
# Towards the chair (left): the chair wants to lift, so the passenger crouches on the outer edge of the chair floor
# with the backside hanging out; the back slides out.
# Away from the chair (right): the back wheel goes light, so the passenger sits on the back of the bike behind the driver.
CORNER={'L':dict(steer=10,roll=2.0,byaw=9,pivot=(0.75,0,0)),'R':dict(steer=-13,roll=-2.5,byaw=-7,pivot=(0.75,0,0))}
def corner_state(side,w,i):
    s,c1,c2=riding(i,32,0.8)
    for k,v in CORNER[side].items(): s[k]=v if k=='pivot' else s.get(k,0)+w*v
    return s,c1,c2
def corner(side):
    def fn(i):
        s,c1,c2=corner_state(side,1.0,i)
        p=PH if side=='L' else PS
        return dict(sc=s,drv=lambda gr: add(drv_lean(gr,side),hips_t=(0,0,0.005*c1)),
                    pas=lift(p,hips_t=(0,0.012*c2 if side=='L' else 0,0.008*c1)))
    return fn
clip('CornerLeft',32,corner('L'),loop=True,kind='riding',info=dict(note='turning towards the chair: passenger crouches on the edge of the chair with the backside hanging out'))
clip('CornerRight',32,corner('R'),loop=True,kind='riding',info=dict(note='turning away from the chair: passenger sits sideways behind the driver, arm round the waist'))

def move_over(a,b,u,side):
    """The passenger's move between the riding crouch and a corner pose: body first, hands following,
    up and over the rail when swinging across the bike."""
    wb=sm(u,0.0,0.8); wh=sm(u,0.2,1.0)
    p=blend(a,b,wb)
    arms={sd:(a['arms'][sd][0].lerp(b['arms'][sd][0],wh),a['arms'][sd][1].lerp(b['arms'][sd][1],wh)) for sd in 'LR'}
    arc=math.sin(math.pi*min(1,max(0,u)))
    if side=='R':
        p['hips_t']=p['hips_t']+V((-0.10*arc,0,0.12*arc)); arms['R']=(arms['R'][0]+V((-0.22,0,0.10))*math.sin(math.pi*wh),arms['R'][1])
    else:
        p['hips_t']=p['hips_t']+V((0.18*arc,0,0.06*arc))   # forward and over, clear of the chair wheel
    # the feet step across rather than slide through the floor
    p['legs']={sd:(p['legs'][sd][0]+V((0,0,0.10*arc)),p['legs'][sd][1]) for sd in 'LR'}
    p['arms']=arms
    return p
def into_corner(side):
    def fn(i):
        u=i/16; w=sm(u,0,1); s,c1,c2=corner_state(side,w,i)
        return dict(sc=s,drv=lambda gr: blend(drv_ride(gr),drv_lean(gr,side),w),pas=move_over(PR0,PH if side=='L' else PS,u,side))
    return fn
def out_of_corner(side):
    def fn(i):
        u=i/16; w=1-sm(u,0,1); s,c1,c2=corner_state(side,w,i)
        return dict(sc=s,drv=lambda gr: blend(drv_ride(gr),drv_lean(gr,side),w),pas=move_over(PH if side=='L' else PS,PR0,u,side))
    return fn
clip('CornerLeftIn',16,into_corner('L'),kind='riding'); clip('CornerLeftOut',16,out_of_corner('L'),kind='riding')
clip('CornerRightIn',16,into_corner('R'),kind='riding'); clip('CornerRightOut',16,out_of_corner('R'),kind='riding')

# ---------------------------------------------------------------- tricks on the flat (all travelling at riding speed)
def flying_chair(i):
    """A right-hand sweeper with the chair wheel high in the air: the passenger is right over the bike to lift it."""
    t=i/FPS; x=V0*t
    over=sm(t,0.1,0.6)*(1-sm(t,3.4,3.9)); lift_=sm(t,0.7,1.3)*(1-sm(t,2.9,3.5)); land=shock(t-3.5,0.08,0.6)
    u=(i%32); s,c1,c2=riding(u,32,0.6)
    s.update(x=x,roll=26*lift_-2*land,steer=-11*over+3*lift_*math.sin(2*math.pi*t*0.9),byaw=-4*over,pivot=(0.75,0,0),
             heave=s['heave']-0.01*land,**spins(x))
    p=move_over(PR0,PS,min(1.0,t/0.6),'R') if t<0.6 else (PS if t<3.4 else move_over(PS,PR0,(t-3.4)/0.5,'R'))
    return dict(sc=s,drv=lambda gr: blend(drv_ride(gr),drv_lean(gr,'R'),over),pas=lift(p,hips_t=(0,0,-0.02*land)))
clip('FlyingChair',96,flying_chair,kind='trick',info=dict(note='rides along on two wheels with the chair in the air'))

WHEELIE_AXIS=(V((-0.595,1.05,0))-V((-0.75,0,0))).normalized()
def wheelie(i):
    """Front wheel up on the throttle, balancing on the back and chair wheels: the passenger stands up with the arms
    at full stretch (owner's note), the driver hangs back off the bars."""
    t=i/FPS; x=V0*t
    up=sm(t,0.35,0.9)*(1-sm(t,2.7,3.25)); wob=1.6*math.sin(2*math.pi*t*0.8)*up
    stand=sm(t,0.1,0.5)*(1-sm(t,3.0,3.5)); land=shock(t-3.25,0.08,0.55)
    s=dict(x=x,pitch=17*up+wob,paxis=tuple(WHEELIE_AXIS),pivot=(-0.75,0,0),cr=0.04*up,cf=0.07*land-0.07*up,heave=-0.008*land,
           **spins(x,slip=1.8*sm(t,0.3,0.9)))
    return dict(sc=s,drv=lambda gr: add(drv_ride(gr),hips_t=(-0.04*up,0,0.02*up),chest=q(Y,-10*up),head=q(Y,10*up),
                                       arms={'L':(gr['L'],(0,1,-1)),'R':(gr['R'],(0,-1,-1))}),
                pas=lift(blend(PR0,PST,stand),hips_t=(0,0,-0.03*land)))
clip('Wheelie',84,wheelie,kind='trick')

def power_slide(i):
    """Sliding the back out round a left-hander towards the chair on the throttle, bars on opposite lock."""
    t=i/FPS; x=V0*t
    sl=sm(t,0.3,0.9)*(1-sm(t,2.4,3.1)); hang=sm(t,0.1,0.6)*(1-sm(t,2.8,3.3))
    s,c1,c2=riding(i%32,32,0.7)
    s.update(x=x,byaw=30*sl,pivot=(0.75,0,0),steer=-20*sl+6*hang*(1-sl),roll=2.5*sl,heave=s['heave']+0.002*math.sin(2*math.pi*t*6)*sl,
             **spins(x,slip=6.0*sm(t,0.3,2.6)))
    p=move_over(PR0,PH,t/0.6,'L') if t<0.6 else (PH if t<2.8 else move_over(PH,PR0,(t-2.8)/0.5,'L'))
    return dict(sc=s,drv=lambda gr: blend(drv_ride(gr),drv_lean(gr,'L'),hang),pas=p)
clip('PowerSlide',84,power_slide,kind='trick')

def passenger_wave(i):
    t=i/FPS; x=V0*t
    up=sm(t,0.2,0.6)*(1-sm(t,2.3,2.9)); wv=math.sin(2*math.pi*t*1.8)*up
    s,c1,c2=riding(i%32,32,0.8); s.update(x=x,**spins(x))
    p=blend(PR0,PST,0.3*up); sh=body_point(p,PJ,'shoulder.L')
    p=add(p,arms={'L':(p['arms']['L'][0].lerp(sh+V((0.10,0.22+0.07*wv,0.30)),up),(-0.4,1,-0.4))},hand_rot={'L':q(X,28*wv)},
          head=q(Z,30*up)@q(Y,-8*up))
    return dict(sc=s,drv=Dr(chest=q(Y,4)),pas=p)
clip('PassengerWave',72,passenger_wave,kind='trick')

def pas_thrown(t):
    p=P(PJ, hips_t=(-0.12,0.22,0.30), root=aim(PJ,(0.55,-0.35,0.75),10), spine=q(Y,-10), chest=q(Y,-6), head=q(Y,-20),
        arms={'L':(rail(4.6),(-0.2,1,0.6)),'R':(rail(3.4),(-0.2,-1,0.6))})
    fl=math.sin(2*math.pi*t*3.2)
    for sd,dy in (('L',0.12),('R',-0.06)):
        h=body_point(p,PJ,'hip.'+sd)
        p['legs'][sd]=(h+V((-0.30,dy,-0.32+0.10*fl*(1 if sd=='L' else -1))),V((1,0,-0.3)))
    return p
def pas_climb(t):
    """Hauling back in: chest down on the rail, one knee back on the platform."""
    p=P(PJ, hips_t=(-0.10,0.12,0.06), root=aim(PJ,(0.75,-0.2,0.6),5), spine=q(Y,10), chest=q(Y,6), head=q(Y,-25),
        arms={'L':(rail(4.4),(-0.3,1,0.3)),'R':(rail(3.1),(-0.3,-1,0.3))},
        legs={'L':((-0.62,0.80,0.52),(0,0,-1)),'R':((-0.70,0.45,FLOOR),(1,0,0))})
    return p
def lost_passenger(i):
    """A big hit flings the passenger up and out of the chair. They hang on by the hands and get dragged with the legs
    trailing off the back while the driver eases off and looks round, then haul themselves back in."""
    t=i/FPS; hit=0.8
    v=V0*(1-0.55*sm(t,1.1,2.2)+0.55*sm(t,3.6,4.6))
    x=0.0
    for j in range(i): x+=V0*(1-0.55*sm(j/FPS,1.1,2.2)+0.55*sm(j/FPS,3.6,4.6))/FPS
    kick=shock(t-hit,0.06,0.55)
    s,c1,c2=riding(i%32,32,0.8)
    s.update(x=x,heave=s['heave']+0.05*kick,pitch=s['pitch']-3*kick,roll=6*kick,cf=0.09*max(0,kick),cr=0.06*max(0,kick),steer=4*math.sin(2*math.pi*t*1.1)*bump(t,1.0,1.4,3.4,4.0),
             **spins(x))
    p=track(t,[(hit+0.02,PR0),(hit+0.32,pas_thrown),(hit+0.75,lambda tt: pas_dragged(math.sin(2*math.pi*tt*2.6))),
               (3.3,lambda tt: pas_dragged(math.sin(2*math.pi*tt*2.6))),(3.9,pas_climb),(4.5,PR0)])
    look=sm(t,hit+0.2,hit+0.6)*(1-sm(t,3.7,4.3))
    return dict(sc=s,drv=Dr(head=q(Z,65*look)@q(Y,-6*look),chest=q(Z,15*look)),pas=p)
clip('LostPassenger',120,lost_passenger,kind='trick',info=dict(note='a big hit throws the passenger out of the chair; they hang on, get dragged, and climb back in'))

# ---------------------------------------------------------------- rollers at race speed
ROLL=dict(count=5,length=3.0,height=0.55,gap=1.4)
RV=7.5          # m/s, about 17 mph
def roller_fn(u): return ROLL['height']*(0.5-0.5*math.cos(2*math.pi*u/ROLL['length']))
def rollers_clip():
    x0=4.0; pitch_len=ROLL['length']+ROLL['gap']
    terr=Terrain()
    for k in range(ROLL['count']):
        a=x0+k*pitch_len; terr.add(a,a+ROLL['length'],roller_fn)
    end=x0+ROLL['count']*pitch_len-ROLL['gap']
    xs=[]; x=0.0; v=RV; t=0.0
    while True:
        xs.append(x)
        if x>end+5.0: v-=7.0/FPS                     # brake once clear
        if v<=0: break
        x+=v/FPS
    n=len(xs)-1
    body=drive(terr,xs,sig=0.6)
    # throttle on each roller's up face (front wheel lofts, back squats), rear tyre spins up when it's in the air
    slip=[0.0]
    for i in range(1,n+1):
        xr=xs[i]+WX['rear']; rel=(xr-x0)%pitch_len
        face=1.0 if (x0<=xr<=end and rel<ROLL['length']*0.5) else 0.0
        slip.append(slip[-1]+(0.9*face+(1.6 if body[i]['rear_air'] else 0.0))*(RV/FPS)/RW['rear'][1]*0.35)
    def fn(i):
        b=body[i]; xr=xs[i]+WX['rear']; rel=(xr-x0)%pitch_len
        gas=1.0 if (x0<=xr<=end and rel<ROLL['length']*0.5) else 0.0
        s=dict(x=xs[i],heave=b['heave'],pitch=b['pitch']+2.5*gas,roll=b['roll'],cf=b['cf'],cr=b['cr']+0.02*gas,**spins(xs[i],slip=slip[i]))
        comp=max(-1,min(1,(b['cf']+b['cr'])*5))-b['squash']
        on=sm(xs[i],x0-2.5,x0-1.0)*(1-sm(xs[i],end+1.0,end+3.0))
        drv=lambda gr: add(blend(drv_ride(gr),drv_attack(gr),on),hips_t=(0,0,-0.05*max(0,comp)+0.03*max(0,-comp)*on))
        pas=lift(blend(PR0,PST,0.35*on),hips_t=(0,0,-0.05*max(0,comp)*on+0.03*max(0,-comp)*on))
        return dict(sc=s,drv=drv,pas=pas)
    clip('Rollers',n,fn,kind='ground',terr=terr,props=[dict(prop='SC_Prop_Rollers',x=x0,length=end-x0)],info=dict(rollers=ROLL,speed=RV))
rollers_clip()

# ---------------------------------------------------------------- jumps: a sidecar-sized double (1 m lip, 5.5 m gap)
LIP=dict(height=1.0,angle=28.0)
RAMP_LEN=2*LIP['height']/math.tan(math.radians(LIP['angle']))
def ramp_fn(u): return LIP['height']*(u/RAMP_LEN)**2 if u<=RAMP_LEN else 0.0
LAND=dict(face=0.9,top=1.2,down=3.4,height=1.0)
LAND_LEN=LAND['face']+LAND['top']+LAND['down']
def landing_fn(u):
    H=LAND['height']; a,b,c=LAND['face'],LAND['top'],LAND['down']
    if u<a: return H*sm(u,0,a)
    if u<a+b: return H
    if u<a+b+c: return H*(1-sm(u,a+b,a+b+c))
    return 0.0
JV=8.8          # m/s off the lip, about 20 mph: about 0.85 s in the air, as in the race footage

BIG_V=13.0      # m/s off the lip for the big double, about 29 mph: about 1.2 s in the air
BIG_LAND=dict(face=1.0,top=1.0,down=4.5,height=1.0)
BIG_EDGE=1.0    # the landing's top edge sits this far short of where the outfit would be back at lip height, so the back wheel clears it and lands on the far slope
def big_landing_fn(u):
    H=BIG_LAND['height']; a,b,c=BIG_LAND['face'],BIG_LAND['top'],BIG_LAND['down']
    if u<a: return H*sm(u,0,a)
    if u<a+b: return H
    if u<a+b+c: return H*(1-sm(u,a+b,a+b+c))
    return 0.0
BIG_LAND_LEN=BIG_LAND['face']+BIG_LAND['top']+BIG_LAND['down']
BIG_TOUCH=0.6   # the back wheel first touches this far down the far slope, just past the top edge (owner 17 Nov 2026: was 2.0, too far down)
def jump_clip(name, trick, note, big=False):
    JV_=BIG_V if big else JV
    th=math.radians(LIP['angle']); vx=JV_*math.cos(th); vz=JV_*math.sin(th)
    x_ramp=5.0; x_lip=x_ramp+RAMP_LEN
    R=2*vx*vz/G                                          # back to lip height
    if big:
        # Place the landing from the simulated flight (the real take-off off the rounded lip is lower than the ideal):
        # move it back until the back wheel's first touch is just over the top edge, on the start of the far slope.
        def first_touch(x_land_):
            air_end_=x_land_+BIG_LAND['face']+BIG_LAND['top']+2.0
            terr_=Terrain().add(x_ramp,x_lip,ramp_fn).add(x_land_,x_land_+BIG_LAND_LEN,big_landing_fn)
            xs_=[]; x_=0.0
            while x_<x_land_+BIG_LAND_LEN+2.0:
                xs_.append(x_); x_+=(vx if x_lip-0.75<x_<air_end_ else JV_)/FPS
            bd=drive(terr_,xs_,sig=0.0,land=0.09); up=False
            for i_,b_ in enumerate(bd):
                if b_['rear_air']: up=True
                elif up: return xs_[i_]+WX['rear']
            return None
        # start well beyond and come back, so the back wheel lands just past the top edge (not halfway down)
        x_land=x_lip+R-BIG_EDGE-BIG_LAND['face']-BIG_LAND['top']+3.0
        for _ in range(80):
            t_=first_touch(x_land)
            if t_ is not None and t_>x_land+BIG_LAND['face']+BIG_LAND['top']+BIG_TOUCH: break
            x_land-=0.1
        land_end=x_land+BIG_LAND_LEN; air_end=x_land+BIG_LAND['face']+BIG_LAND['top']+2.0
        terr=Terrain().add(x_ramp,x_lip,ramp_fn).add(x_land,land_end,big_landing_fn)
        props_=[dict(prop='SC_Prop_JumpRamp',x=x_ramp,length=RAMP_LEN),dict(prop='SC_Prop_DoubleLanding',x=x_land,length=BIG_LAND_LEN)]
    else:
        x_land=x_lip+R-2.4-LAND['face']-LAND['top']          # both wheels clear the table and meet the down slope (back wheel first)
        land_end=x_land+LAND_LEN; air_end=x_land+LAND['face']+LAND['top']+1.0
        terr=Terrain().add(x_ramp,x_lip,ramp_fn).add(x_land,land_end,landing_fn)
        props_=[dict(prop='SC_Prop_JumpRamp',x=x_ramp,length=RAMP_LEN),dict(prop='SC_Prop_JumpLanding',x=x_land,length=LAND_LEN)]
    xs=[]; x=0.0; v=JV_; end=land_end
    while True:
        xs.append(x)
        if x>end+3.0: v-=7.5/FPS
        if v<=0: break
        # horizontal speed drops to vx in the air (the rest went upwards) - near enough constant JV on the ground
        x+=(vx if x_lip-0.75<x<air_end else v)/FPS
    n=len(xs)-1
    body=drive(terr,xs,sig=0.0,land=0.09)
    airs=[i for i,b in enumerate(body) if b['air']]
    a0=airs[0] if airs else 0; a1=airs[-1]+1 if airs else 1
    T=(a1-a0)/FPS
    # In the air the outfit keeps its nose up (race footage: it flies nearly level, nose a little high, and lands
    # back wheel first), settling onto the ground's line over a fifth of a second after touching down.
    p_take=body[a0]['pitch'] if airs else 0.0
    def air_pitch(i):
        g=body[i]['pitch']
        if not airs or i<a0: return g
        if i<a1: return lerp(p_take,7.0,sm((i-a0)/max(1,a1-a0),0,0.55))
        return lerp(7.0,g,sm((i-a1)/FPS,0,0.2))
    def fn(i):
        b=body[i]; t=i/FPS
        s=dict(x=xs[i],heave=b['heave'],pitch=air_pitch(i),roll=b['roll'],cf=b['cf'],cr=b['cr'],**spins(xs[i],slip=(0.0 if i<a0 else 2.0*min(1,(i-a0)/FPS/0.3))))
        u=(i-a0)/(a1-a0)
        ready=sm(xs[i],x_ramp-3.0,x_ramp-1.0)*(1-sm(xs[i],end,end+2.5))
        face=1.0 if x_ramp<xs[i]+WX['rear']<x_lip else 0.0
        sq=max(0.0,b['squash'])
        fly=(sm(i,a0-3,a0+3)*(1-sm(i,a1-2,a1+3))) if airs else 0.0
        st=dict(sc=s,u=u,T=T,ready=ready,
                drv=dict(base='attack',w=ready,dz=-0.05*face-0.10*sq+0.10*fly),
                pas=dict(w=ready,dz=-0.05*face-0.09*sq,stand=fly))
        trick(st,u,b)
        dd=st['drv']; pp=st['pas']
        def drv(gr,dd=dd):
            base=blend(drv_ride(gr),drv_attack(gr),dd['w']); base=add(base,hips_t=(0,0,dd['dz']))
            return dd['mod'](base,gr) if 'mod' in dd else base
        pas=lift(blend(PR0,PST,pp['stand']),hips_t=(0,0,pp['dz']))
        if 'mod' in pp: pas=pp['mod'](pas)
        return dict(sc=st['sc'],drv=drv,pas=pas)
    info=dict(note=note,ramp_x=round(x_ramp,3),lip_x=round(x_lip,3),landing_x=round(x_land,3),take_off_t=round(a0/FPS,3),land_t=round(a1/FPS,3),
              air_time=round(T,3),speed=JV_)
    clip(name,n,fn,kind='jump',terr=terr,props=props_,info=info)

def mid(u,a=0.12,b=0.35,c=0.62,d=0.86): return bump(u,a,b,c,d) if 0<=u<=1 else 0.0

def t_double(st,u,b):
    """No trick: a big double (owner's notes 17 Nov 2026). Both crew stand up through the air as on every jump."""
    pass
jump_clip('Double',t_double,'a big double: off the lip, over a 7.9 m gap and down the landing\'s far slope',big=True)

def t_nohander(st,u,b):
    w=mid(u,0.15,0.35,0.6,0.82)
    def mod(p,gr,w=w):
        J=DJ
        return add(p,arms={'L':(gr['L'].lerp(J['shoulder.L']+V((0.12,0.26,0.30)),w),(0,0,-1)),'R':(gr['R'].lerp(J['shoulder.R']+V((0.0,-0.42,0.12)),w),(0,0,-1))},
                   head=q(Y,-10*w),chest=q(Y,-8*w))
    st['drv']['mod']=mod
jump_clip('NoHander',t_nohander,'the driver lets go of the bars and spreads both arms in the air')

def t_superman(st,u,b):
    # on the big double (owner's notes 17 Nov 2026): out quickly, held flat for most of the flight, back in before the landing
    w=mid(u,0.05,0.20,0.78,0.92)
    st['pas']['mod']=lambda p,w=w,u=u: blend(p,pas_superman(math.sin(2*math.pi*max(0,u)*2)),w)
    st['drv']['mod']=lambda p,gr,w=w: add(p,head=q(Z,25*w))
jump_clip('PassengerSuperman',t_superman,'on the big double: the passenger lets their body fly out flat behind, holding on by the hands, and holds it',big=True)

json.dump(dict(fps=FPS,clips=CLIPS,rollers=ROLL,lip=LIP,landing=LAND,ramp_length=RAMP_LEN,landing_length=LAND_LEN,big_landing=BIG_LAND,ride_speed=V0,
               turn_radius=4.5,checks=CHECK),open(W+'clips.json','w'),indent=1)
sc.frame_start=1; sc.frame_end=frame[0]
bpy.ops.wm.save_as_mainfile(filepath=W+'sidecar_anim.blend')
print('anim saved, last frame',frame[0])
for c in CHECK: print('CHECK',c)
