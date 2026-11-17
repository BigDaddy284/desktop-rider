"""Dirt props for the sidecar (wider than Harvey's: the outfit's track runs from the bike's right side to past the chair wheel)."""
import bpy, math, sys
sys.path.insert(0,'/tmp/claude-0/sc/anim')
Y0,Y1=-0.55,1.45          # across the track (outfit space): bike on the right, chair on the left

def mat(name,col,rough=0.95):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes=True; b=m.node_tree.nodes['Principled BSDF']; b.inputs['Base Color'].default_value=(*col,1); b.inputs['Roughness'].default_value=rough
    return m

def profile_prop(name, length, fn, step=0.04):
    n=max(2,int(round(length/step))); xs=[length*i/n for i in range(n+1)]
    verts=[]; faces=[]; fm=[]
    for x in xs:
        h=max(0.0,fn(x)); verts+= [(x,Y0,0),(x,Y0,h),(x,Y1,h),(x,Y1,0)]
    for i in range(n):
        a=4*i; b=4*(i+1)
        faces.append((a+1,b+1,b+2,a+2)); fm.append(0)       # top
        faces.append((a,b,b+1,a+1)); fm.append(1)           # right side
        faces.append((a+3,a+2,b+2,b+3)); fm.append(1)       # left side
    faces.append((0,1,2,3)); fm.append(1); b=4*n; faces.append((b+3,b+2,b+1,b)); fm.append(1)
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces)
    me.materials.append(mat('SC_Dirt',(0.42,0.26,0.14))); me.materials.append(mat('SC_DirtSide',(0.30,0.18,0.10)))
    for p,mi in zip(me.polygons,fm): p.material_index=mi
    me.validate(); me.update()
    ob=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(ob)
    return ob

def build(clipdata):
    import json
    R=clipdata['rollers']; L=clipdata['lip']; LD=clipdata['landing']
    ramp_len=clipdata['ramp_length']; land_len=clipdata['landing_length']
    def ramp(u): return L['height']*(u/ramp_len)**2
    def sm(t,a,b):
        x=min(1,max(0,(t-a)/(b-a))); return x*x*(3-2*x)
    def landing(u):
        H=LD['height']; a,b,c=LD['face'],LD['top'],LD['down']
        if u<a: return H*sm(u,0,a)
        if u<a+b: return H
        return H*(1-sm(u,a+b,a+b+c))
    pitch=R['length']+R['gap']; total=R['count']*pitch-R['gap']
    def rollers(u):
        k=int(u//pitch); v=u-k*pitch
        return R['height']*(0.5-0.5*math.cos(2*math.pi*v/R['length'])) if v<=R['length'] else 0.0
    T=clipdata['table']
    def tabletop(u):
        if u<ramp_len: return ramp(u)
        v=u-ramp_len
        if v<T['top']: return L['height']
        return L['height']*(1-sm(v,T['top'],T['top']+T['down']))
    return {'SC_Prop_Tabletop':profile_prop('SC_Prop_Tabletop',ramp_len+T['top']+T['down'],tabletop),
            'SC_Prop_JumpRamp':profile_prop('SC_Prop_JumpRamp',ramp_len,ramp),
            'SC_Prop_JumpLanding':profile_prop('SC_Prop_JumpLanding',land_len,landing),
            'SC_Prop_Rollers':profile_prop('SC_Prop_Rollers',total,rollers,step=0.03)}
