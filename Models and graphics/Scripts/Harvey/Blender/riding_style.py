"""The rider's own riding style for the 3D model: how this particular rider sits, corners and starts.

Based on References/Tricks/RIDER_STYLE.md (the owner's photographs and videos). It keys five clips on the bike and
rider rigs, using the same machinery as the trick animations (trick_core.py):

    RideOff     the start: seated and forward, the rear squatting as it drives, the nose lifting a little
    RidingLoop  seated right at the front of the seat, chest low over the bars, head low, looking ahead
    CornerLeft  the corner pose for a left turn: inside (left) leg straight out ahead beside the front wheel, toe up,
                head turned into the corner, the body held more upright than the bike leans
    CornerRight the same for a right turn

In the app, CornerLeft and CornerRight are blended over RidingLoop by how hard the bike is turning, and the lean of the
whole bike comes from the app, so the corner clips only hold the rider's part of it (the leg, the head, and the body
staying nearer upright than the bike).

The clips replace the model's own RideOff and RidingLoop in the app; the rest of its clips stay as they are.
Boss tried a feet-down resting pose and feet dabbing the ground at the start (from the start-gate video) on 14 November
2026 and asked for both to be taken out, so the model's own resting pose (feet on the pegs) stays.
Like the trick animations, they are keyed only in a throwaway background session for export (export_models.py with
anims=riding_style); the owner's .blend file is never changed or saved.

    blender --background <desktop_rider_3d.blend> --python riding_style.py -- render <out dir>   check pictures
"""
import bpy, math, os, sys
from mathutils import Euler, Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from trick_core import *  # noqa: F401,F403
import trick_core as C

# ----------------------------------------------------------------- poses (see trick_core.pose for the keys)
# Hip offsets are metres in the bike's axes (x forward, y left, z up); rotations are degrees (X roll, Y forward, Z turn).
SEAT_FRONT = dict(hips=(0.075, 0, -0.015), hipsrot=(0, 8, 0), spine=(0, 26, 0), head=(0, -20, 0))
# The inside foot for a corner: straight out ahead, beside the front wheel, level with the axle, held off the ground.
LEG_OUT = (0.40, 0.13, 0.03)
LEAN_BACK = 14.0          # degrees the body stays more upright than the bike at full lean
HEAD_TURN = 24.0          # degrees the head turns into the corner

RIDE_FRAMES = 24          # one second at 24 a second
START_FRAMES = 30


def seated(extra=None):
    p = dict(SEAT_FRONT)
    p.update(extra or {})
    return pose(**p)


def wheel_angles(i, frames, turns=2):
    """Wheel rotation for a loop: whole turns over the loop so it repeats without a jump (about riding speed)."""
    a = 360.0 * turns * i / frames
    return a, a


def ride_state(i, frames):
    """The bike's small movement while riding along: a gentle bob over the ground and a little fork movement."""
    d = state()
    u = 2 * math.pi * i / frames
    d['squat'] = 0.006 * math.sin(u * 2)
    d['dive'] = 0.004 * math.sin(u * 2 + 0.8)
    d['pitch'] = 0.6 * math.sin(u)
    d['wr'], d['wf'] = wheel_angles(i, frames)
    return d


def apply_pose(d, p):
    for k in RIDER_KEYS:
        d[k] = p[k]
    return d


def riding_loop():
    out = []
    for i in range(RIDE_FRAMES + 1):
        u = 2 * math.pi * i / RIDE_FRAMES
        d = ride_state(i, RIDE_FRAMES)
        p = seated(dict(spine=(0, 26 + 1.2 * math.sin(u * 2), 0), head=(0, -20 - 1.2 * math.sin(u * 2), 0)))
        out.append(apply_pose(d, p))
    return out


def corner(side):
    """side = +1 for a left turn (inside leg is the left), -1 for a right turn."""
    out = []
    peg = 'pegL' if side > 0 else 'pegR'
    for i in range(RIDE_FRAMES + 1):
        u = 2 * math.pi * i / RIDE_FRAMES
        d = ride_state(i, RIDE_FRAMES)
        extra = {
            # body held nearer upright than the bike: roll the hips and spine back out of the lean
            'hipsrot': (-side * LEAN_BACK * 0.6, 8, side * 6),
            'spine': (-side * LEAN_BACK * 0.4, 24 + 1.0 * math.sin(u * 2), side * 8),
            'head': (-side * 4, -16, side * HEAD_TURN),
            'hips': (0.085, side * 0.02, -0.02),
            peg: (LEG_OUT[0], side * LEG_OUT[1], LEG_OUT[2] + 0.008 * math.sin(u * 2)),
        }
        out.append(apply_pose(d, seated(extra)))
    return out


def ride_off():
    """The start: the bike launches, the rear squats and the nose lifts a little while the rider tucks forward into
    the seated riding position. The feet stay on the pegs (Boss's choice; the video's feet-down start was taken out)."""
    dt = 1.0 / FPS
    out = []
    wr = wf = 0.0
    for i in range(START_FRAMES + 1):
        t = i * dt
        d = state()
        go = sm(t, 0.0, 0.25)
        v = C.V * sm(t, 0.0, 1.0)
        wr += math.degrees(v * dt / RR) * (1 + 0.6 * bump(t, 0.0, 0.08, 0.2, 0.4))       # the rear wheel spins up first
        wf += math.degrees(v * dt / RF)
        d['wr'], d['wf'] = wr, wf
        d['squat'] = 0.03 * bump(t, 0.0, 0.1, 0.25, 0.6)                                  # the rear squats as it drives
        d['pitch'] = -2.0 * bump(t, 0.0, 0.12, 0.3, 0.7)                                  # and the nose lifts a little
        extra = {
            'spine': (0, 20 + 8 * go, 0),
            'head': (0, -12 - 8 * go, 0),
            'hips': (0.07 + 0.005 * go, 0, -0.02 + 0.005 * go),
        }
        out.append(apply_pose(d, seated(extra)))
    return out


def animations():
    return [
        dict(name='RideOff', loop=False, build=ride_off),
        dict(name='RidingLoop', loop=True, build=riding_loop),
        dict(name='CornerLeft', loop=True, build=lambda: corner(+1)),
        dict(name='CornerRight', loop=True, build=lambda: corner(-1)),
    ]


def build(side='Right', names=None):
    """Keys every clip one after another on the rigs (replacing their actions in this session) and returns the
    table of clips, in the same form as trick_animations.build."""
    sc = bpy.context.scene; sc.render.fps = FPS
    rig = Rig(); rig.reset()
    f0 = 1; table = []
    for a in animations():
        if names and a['name'] not in names:
            continue
        states = a['build']()
        N = len(states) - 1
        for t, d in enumerate(states):
            rig.apply(f0 + t, d)
        table.append(dict(name=a['name'], start=f0, end=f0 + N, frames=N, loop=a['loop'], stage='ground'))
        f0 += N + 1 + 4
    sc.frame_start = 1; sc.frame_end = max(1, f0 - 5)
    sc.frame_set(1)
    return table


# ----------------------------------------------------------------- check pictures
VIEWS = {
    'side': ((0.0, -3.2, 0.75), (math.radians(90), 0, 0)),
    'front': ((2.6, -1.6, 1.25), (math.radians(78), 0, math.radians(58))),
    'above': ((1.6, -1.8, 2.6), (math.radians(45), 0, math.radians(42))),
}


def render(out, names=None, views=('side', 'front', 'above')):
    sc = bpy.context.scene
    table = build(names=names)
    cam = sc.camera
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.2
    sc.render.resolution_x = 640; sc.render.resolution_y = 480; sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    try:
        sc.eevee.taa_render_samples = 16
    except AttributeError:
        pass
    os.makedirs(out, exist_ok=True)
    done = []
    for a in table:
        for k in (0, a['frames'] // 2):
            sc.frame_set(a['start'] + k)
            for v in views:
                loc, rot = VIEWS[v]
                cam.location = loc; cam.rotation_euler = Euler(rot)
                path = os.path.join(out, '%s_%02d_%s.png' % (a['name'], k, v))
                sc.render.filepath = path
                bpy.ops.render.render(write_still=True)
                done.append(path)
    return done


if __name__ == '__main__' and '--' in sys.argv:
    args = sys.argv[sys.argv.index('--') + 1:]
    if args and args[0] == 'render':
        print('RENDERED', render(args[1], args[2:] or None))
