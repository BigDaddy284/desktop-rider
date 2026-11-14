"""Check pictures and measurements for the trick animations, from any side, before they are exported.

    blender --background <desktop_rider_3d.blend> --python trick_check.py -- <out dir> <Name> [Name ...] [--frames 8]

For each trick it keys the clip in this session only (the owner's .blend is never saved), then:
  * renders a few moments of it from the side, the front three-quarter, the rear three-quarter and above, with the
    props in place, into <out dir>/<Name>/<frame>_<view>.png;
  * measures, at every frame, how far each hand is from its grip and each foot from its peg target, and prints one
    "CHECK" line per trick with the worst gaps (a hand off the bar shows up here even when it is hard to see).
"""
import bpy, math, os, sys, json
from mathutils import Euler, Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import trick_animations as TA  # noqa: E402

VIEWS = {
    'side': ((0.6, -6.0, 1.3), (90, 0, 0)),
    'front34': ((5.2, -3.6, 2.2), (78, 0, 55)),
    'rear34': ((-3.6, -4.6, 2.4), (76, 0, -38)),
    'above': ((0.6, -3.2, 5.6), (35, 0, 0)),
}


def gaps(frames):
    sc = bpy.context.scene
    b = bpy.data.objects['bike_rig']; r = bpy.data.objects['rider_rig']
    worst = {}
    for f in frames:
        sc.frame_set(f)
        for s in 'LR':
            hand = r.matrix_world @ r.pose.bones['forearm.' + s].tail
            grip = b.matrix_world @ b.pose.bones['grip.' + s].head
            foot = r.matrix_world @ r.pose.bones['shin.' + s].tail
            peg = b.matrix_world @ b.pose.bones['peg.' + s].head
            for k, v in (('hand' + s, (hand - grip).length), ('foot' + s, (foot - peg).length)):
                if v > worst.get(k, (0, 0))[0]:
                    worst[k] = (v, f)
    return worst


def follow(cam, view, centre):
    loc, rot = VIEWS[view]
    cam.location = Vector(loc) + Vector((centre.x, 0, centre.z - 0.45))
    cam.rotation_euler = Euler([math.radians(a) for a in rot])


def main(argv):
    out = argv[0]
    names = [a for a in argv[1:] if not a.startswith('--')]
    count = int(argv[argv.index('--frames') + 1]) if '--frames' in argv else 8
    sc = bpy.context.scene
    table = TA.build('Right', names)
    cam = sc.camera
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.8
    sc.render.resolution_x = 560; sc.render.resolution_y = 420; sc.render.resolution_percentage = 100
    sc.render.film_transparent = False; sc.render.image_settings.file_format = 'PNG'
    try:
        sc.eevee.taa_render_samples = 8
    except AttributeError:
        pass
    b = bpy.data.objects['bike_rig']
    report = {}
    for a in table:
        frames = list(range(a['start'], a['end'] + 1))
        g = gaps(frames)
        report[a['name']] = {k: (round(v * 1000), f - a['start']) for k, (v, f) in g.items()}
        print('CHECK %s %s' % (a['name'], json.dumps(report[a['name']])))
        d = os.path.join(out, a['name']); os.makedirs(d, exist_ok=True)
        lo, hi = 0, a['frames']
        info = a.get('info', {})
        if 'tL' in info and 'T' in info:          # a jump: from just before the lip to just after the landing
            lo = max(0, int((info['tL'] - 0.35 * info['T']) * TA.FPS)); hi = min(a['frames'], int((info['tL'] + info['T'] + 0.15) * TA.FPS))
        picks = [a['start'] + lo + round(i * (hi - lo) / (count - 1)) for i in range(count)]
        for f in picks:
            sc.frame_set(f)
            centre = b.matrix_world @ b.pose.bones['chassis'].head
            for v in VIEWS:
                follow(cam, v, centre)
                sc.render.filepath = os.path.join(d, '%04d_%s.png' % (f - a['start'], v))
                bpy.ops.render.render(write_still=True)
    json.dump(report, open(os.path.join(out, 'gaps.json'), 'w'), indent=1)


if __name__ == '__main__' and '--' in sys.argv:
    main(sys.argv[sys.argv.index('--') + 1:])
