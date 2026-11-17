"""Renders the sidecar outfit's approved moves as Desktop Rider sprite sheets (see Documentation/SPRITE_SPECIFICATION.md).

    python sprites.py render <Right|Left> [AppName ...]      frames to work/sprites/<Side>/<AppName>/0000.png
    python sprites.py pack <Assets/Sprites/Sidecar41>          sheets and sprites.json

The outfit stays where it is and the camera follows it, so riding moves play on the spot and ramps roll past, as for
the rider. The view is from a little in front and above (18 degrees round, 8 degrees down) so the passenger and the
chair can be seen; a flat side view hides one behind the other.
"""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

W = '/tmp/claude-0/sc/work/'
OUT = W + 'sprites/'
PPM = 170                          # pixels per metre: the outfit comes out about as wide on screen as the rider's bike
YAW, PITCH = 18.0, 8.0
STD = dict(w=470, h=375, ox=235, gy=345)   # the standard frame; (ox, gy) is the outfit's place on the ground
STAGES = {'std': dict(left=0, right=0, up=0),
          'trick': dict(left=140, right=0, up=20),     # room behind for the passenger thrown out, a little above
          'air': dict(left=140, right=0, up=380)}      # jumps and rollers
STEP = 2                                               # keyed at 24 a second, shown at 12
# app name: (clip, stage, loop, enter, exit)
MOVES = {
    'Idle': ('Idle', 'std', True, None, None),
    'IdleVariation': ('LookAround', 'std', False, None, None),
    'Revving': ('Revving', 'std', True, None, None),
    'Thinking': ('Thinking', 'std', False, None, None),
    'Talking': ('Talking', 'std', True, None, None),
    'Celebration': ('Celebration', 'std', False, None, None),
    'SleepStart': ('SleepStart', 'std', False, None, None),
    'Sleeping': ('Sleeping', 'std', True, 'SleepStart', 'SleepEnd'),
    'SleepEnd': ('SleepEnd', 'std', False, None, None),
    'RideOff': ('RideOff', 'std', False, None, None),
    'RidingLoop': ('RidingLoop', 'std', True, 'RideOff', 'RideOn'),
    'RideOn': ('RideOn', 'std', False, None, None),
    'Wheelie': ('Wheelie', 'trick', False, None, None),
    'FlyingChair': ('FlyingChair', 'trick', False, None, None),
    'PowerSlide': ('PowerSlide', 'trick', False, None, None),
    'PassengerWave': ('PassengerWave', 'trick', False, None, None),
    'LostPassenger': ('LostPassenger', 'trick', False, None, None),
    'Rollers': ('Rollers', 'air', False, None, None),
    'Double': ('Double', 'air', False, None, None),
    'NoHander': ('NoHander', 'air', False, None, None),
    'PassengerSuperman': ('PassengerSuperman', 'air', False, None, None),
}


def frame_size(stage):
    st = STAGES[stage]
    return STD['w'] + st['left'] + st['right'], STD['h'] + st['up'], st['left'], st['up']


def render(side, names):
    import bpy
    from mathutils import Vector, Euler
    import props
    bpy.ops.wm.open_mainfile(filepath=W + 'sidecar_anim.blend')
    D = json.load(open(W + 'clips.json'))
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.film_transparent = True; sc.view_settings.view_transform = 'Standard'
    sc.eevee.taa_render_samples = int(os.environ.get('SAMPLES', '16'))
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs[0].default_value = (1, 1, 1, 1); bg.inputs[1].default_value = 0.55
    sun = bpy.data.lights.new('Sun', 'SUN'); sun.energy = 2.8; so = bpy.data.objects.new('Sun', sun); sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(50), 0, math.radians(-30 if side == 'Right' else 150))
    cam = bpy.data.cameras.new('Cam'); cam.type = 'ORTHO'; co = bpy.data.objects.new('Cam', cam); sc.collection.objects.link(co); sc.camera = co
    P = props.build(D)
    sg = 1 if side == 'Right' else -1
    d = Vector((0, -sg, 0)); d.rotate(Euler((0, 0, math.radians(sg * YAW)))); d.z = math.tan(math.radians(PITCH)); d.normalize()
    fwd = -d; u = fwd.cross(Vector((0, 0, 1))).normalized(); v = u.cross(fwd).normalized()
    clips = {c['name']: c for c in D['clips']}
    for name in names:
        clip, stage, loop, _, _ = MOVES[name]
        c = clips[clip]
        wpx, hpx, left, up = frame_size(stage)
        # where the outfit's ground point sits in this frame, in pixels from the top-left (Right); Left is the mirror image
        ox = left + STD['ox'] if side == 'Right' else wpx - left - STD['ox']
        gy = up + STD['gy']
        cx = (wpx / 2 - ox) / PPM; cy = (gy - hpx / 2) / PPM          # frame centre from the ground point, metres
        sc.render.resolution_x = wpx; sc.render.resolution_y = hpx; sc.render.resolution_percentage = 100
        cam.ortho_scale = max(wpx, hpx) / PPM
        for o in P.values(): o.location = (0, 0, -50)
        for p in c['props']: P[p['prop']].location = (p['x'], 0, 0)
        last = c['frames'] - (STEP if loop else 0)
        ts = list(range(0, last + 1, STEP))
        out = os.path.join(OUT, side, name); os.makedirs(out, exist_ok=True)
        for k, t in enumerate(ts):
            path = os.path.join(out, '%04d.png' % k)
            if os.path.exists(path) and os.environ.get('FORCE') != '1':
                continue                               # already rendered: a stopped run carries on where it left off
            sc.frame_set(c['start'] + t)
            org = Vector((c['travel'][t], 0, 0))
            centre = org + u * cx + v * cy
            co.location = centre + d * 30; co.rotation_euler = fwd.to_track_quat('-Z', 'Y').to_euler()
            sc.render.filepath = path; bpy.ops.render.render(write_still=True)
        json.dump(dict(name=name, clip=clip, stage=stage, loop=loop, count=len(ts), size=[wpx, hpx]), open(os.path.join(out, 'info.json'), 'w'))
        print('rendered', side, name, len(ts))


def pack(assets):
    import glob
    from PIL import Image
    COLS = 8
    os.makedirs(os.path.join(assets, 'Right'), exist_ok=True); os.makedirs(os.path.join(assets, 'Left'), exist_ok=True)
    anims = []
    for name, (clip, stage, loop, enter, exit_) in MOVES.items():
        wpx, hpx, left, up = frame_size(stage); counts = set()
        for side in ('Right', 'Left'):
            files = sorted(glob.glob(os.path.join(OUT, side, name, '[0-9]*.png')))
            info = json.load(open(os.path.join(OUT, side, name, 'info.json')))
            if len(files) != info['count']: raise SystemExit('%s %s: %d of %d frames' % (side, name, len(files), info['count']))
            rows = (len(files) + COLS - 1) // COLS
            sheet = Image.new('RGBA', (wpx * COLS, hpx * rows), (0, 0, 0, 0))
            for i, f in enumerate(files):
                im = Image.open(f).convert('RGBA')
                if im.size != (wpx, hpx): raise SystemExit('%s is %s, expected %s' % (f, im.size, (wpx, hpx)))
                sheet.paste(im, ((i % COLS) * wpx, (i // COLS) * hpx))
            sheet.quantize(256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE).save(os.path.join(assets, side, name + '.png'), optimize=True)
            counts.add(len(files))
        if len(counts) != 1: raise SystemExit(name + ': the two directions differ')
        e = dict(name=name, loop=loop, frameCount=counts.pop(), columns=COLS, right='Right/%s.png' % name, left='Left/%s.png' % name)
        if enter: e['enter'] = enter
        if exit_: e['exit'] = exit_
        if stage != 'std': e.update(frameWidth=wpx, frameHeight=hpx, originX=left, originY=up)
        anims.append(e)
    man = dict(displayName='Sidecar 41', rideSpeed=6.3, frameWidth=STD['w'], frameHeight=STD['h'], framesPerSecond=12, pixelsPerMetre=PPM, groundY=STD['gy'], animations=anims)
    with open(os.path.join(assets, 'sprites.json'), 'w') as f:
        json.dump(man, f, indent=2); f.write('\n')
    print('packed', len(anims))


if __name__ == '__main__':
    a = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    if a[0] == 'render': render(a[1], a[2:] or list(MOVES))
    else: pack(a[1])
