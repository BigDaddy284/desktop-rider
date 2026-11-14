"""Builds and renders the Desktop Rider jump, trick and whoop animations.

Usage (with Blender's Python, for example the `bpy` package):
    python trick_animations.py build   <in.blend> <out.blend>            key every animation and save the file
    python trick_animations.py render  <in.blend> <frames dir> <Right|Left> [name ...] [--samples N] [--step N]
    python trick_animations.py table   <in.blend>                        print the list of animations as JSON

The animations are keyed for the side being rendered, because gestures use the hand nearest the camera.
"""
import bpy, math, json, os, sys
from mathutils import Euler
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from trick_core import *  # noqa: F401,F403
import trick_core as C

PPM = 240
STD = dict(x0=-1.15 + 8 / 240, z1=1.87 - 67 / 240, w=467, h=389)          # the standard sprite frame, in metres and pixels
STAGES = {                                                                    # extra room around the standard frame, in pixels
    'air': dict(left=180, right=260, up=456),
    'ground': dict(left=180, right=260, up=100),
}
SIDE = {'hs': -1.0, 'near': 'R', 'far': 'L'}                                 # set by configure()


def configure(side):
    SIDE['hs'] = 1.0 if side == 'Left' else -1.0
    SIDE['near'] = 'L' if side == 'Left' else 'R'
    SIDE['far'] = 'R' if side == 'Left' else 'L'


def near(v):
    """A sideways distance measured towards the camera."""
    return v * SIDE['hs']


# ================================================================= rider choreography in the air
# Each trick is fn(u) -> (pose, bike extras) for u = 0..1 through the flight. The pose replaces the attack position
# by the returned weight; extras may hold roll, yaw, steer.
def hold(u, a=0.14, b=0.38, c=0.60, d=0.84):
    return bump(u, a, b, c, d)


def smoother(t, a, b):
    """Like sm(), but with no sudden change in how fast it moves at either end (for long, flowing swings)."""
    x = min(1.0, max(0.0, (t - a) / (b - a)))
    return x * x * x * (x * (6 * x - 15) + 10)


def _feet(l, r):
    return dict(pegL=tuple(Vector(l) - PEG['L']), pegR=tuple(Vector(r) - PEG['R']))


def _hands(l, r):
    return dict(gripL=tuple(Vector(l) - GRIP['L']), gripR=tuple(Vector(r) - GRIP['R']))


def _side(near_v, far_v, fn):
    """Give targets as (near side, far side); y values are written for the near side being +y."""
    hs = SIDE['hs']
    n = (near_v[0], near_v[1] * hs, near_v[2]); f = (far_v[0], far_v[1] * hs, far_v[2])
    return fn(n, f) if hs > 0 else fn(f, n)


def t_plain(u):
    # A racer's jump: stay in the attack position, squeeze the bike, let it float.
    p = pose(hips=(0.03, 0, 0.07), spine=(0, 24, 0), head=(0, -16, 0))
    return p, hold(u, 0.1, 0.4, 0.6, 0.9), {}


def t_superman(u):
    p = pose(hips=(-0.316, 0, 0.37), hipsrot=(0, 75, 0), head=(0, -58, 0),
             **_feet((-1.6, 0.1, 1.34), (-1.6, -0.1, 1.34)))
    return p, hold(u), {}


def t_no_hander(u):
    p = pose(hips=(0.0, 0, 0.03), spine=(0, -22, 0), head=(0, 6, 0),
             **_hands((-0.25, 0.75, 1.75), (-0.25, -0.75, 1.75)))
    return p, hold(u), {}


def t_nothing(u):
    # Hands and feet off together. Boss (14 November): the legs went back a little too far; they now trail just behind.
    p = pose(hips=(-0.02, 0, 0.12), spine=(0, -14, 0), head=(0, 2, 0),
             **_hands((-0.2, 0.8, 1.7), (-0.2, -0.8, 1.7)), **_feet((-0.50, 0.50, 0.62), (-0.50, -0.50, 0.62)))
    return p, hold(u), {}


def t_bar_hop(u):
    # Both feet come up between the arms and out over the bars, then back through to the pegs. (Until 14 November this
    # was called the heel clicker; Boss pointed out it is a bar hop, because the legs pass between the arms.)
    p = pose(hips=(-0.12, 0, 0.14), hipsrot=(0, -16, 0), spine=(0, 22, 0), head=(0, -4, 0),
             **_feet((0.8, 0.07, 1.2), (0.8, -0.07, 1.2)))
    return p, hold(u), {}


def _path(points, steps):
    """A point moved along `points` by `steps` (one 0..1 weight for each leg of the path)."""
    p = Vector(points[0])
    for q, w in zip(points[1:], steps):
        p = p.lerp(Vector(q), w)
    return p


def t_heel_clicker(u):
    # The legs come up round the OUTSIDE of the arms, knees out past the elbows, and the heels click together once in
    # front of the chest above the bars; then back round the arms and down to the pegs. The rider sits back with the
    # arms nearly straight. (Red Bull's trick list: the legs "wrap around the arms" and the heels click "in front of
    # the chest"; legs between the arms would be a bar hop.)
    out = [sm(u, 0.10, 0.19), sm(u, 0.17, 0.28), sm(u, 0.27, 0.38)]
    back = [sm(u, 0.72, 0.84), sm(u, 0.62, 0.73), sm(u, 0.55, 0.63)]
    steps = [a * (1 - b) for a, b in zip(out, back)]
    click = bump(u, 0.40, 0.45, 0.48, 0.53)

    def foot(y):
        return tuple(_path([(0.05, 0.165 * y, 0.31), (0.0, 0.50 * y, 0.75), (0.10, 0.56 * y, 1.24),
                            (0.30, (0.05 - 0.045 * click) * y, 1.12)], steps))
    p = pose(hips=(-0.10, 0, 0.14), hipsrot=(0, -14, 0), spine=(0, 16, 0), head=(0, -6, 0), **_feet(foot(1), foot(-1)))
    knees = bump(u, 0.08, 0.20, 0.74, 0.88)
    return p, hold(u, 0.08, 0.30, 0.66, 0.88), dict(kneew=knees, kneeL=(0.42, 0.95, 1.25), kneeR=(0.42, -0.95, 1.25))


def t_can_can(u):
    # The far leg comes over the seat and kicks out on the camera's side.
    def feet(n, f):
        return _feet(n, f) if SIDE['hs'] > 0 else _feet(f, n)
    hs = SIDE['hs']
    p = pose(hips=(-0.02, 0, 0.12), hipsrot=(0, 0, near(-14)), spine=(0, 14, 0), head=(0, -10, near(-10)),
             **feet(tuple(PEG[SIDE['near']]), (0.62, 0.55 * hs, 0.62)))
    return p, hold(u), {}


def t_nac_nac(u):
    # From Boss's video and photographs (14 November): the far leg swings back over the rear mudguard and out to the
    # camera's side, and as it goes the whole bike swings round underneath the rider until it is nearly side-on to the
    # direction of travel, while the rider stays facing the way they are going, upright, looking ahead; then the bike
    # swings back into line under them and the leg comes back for the landing. The back of the bike goes away from the
    # rider's body.
    def feet(n, f):
        return _feet(n, f) if SIDE['hs'] > 0 else _feet(f, n)
    hs = SIDE['hs']
    leg = hold(u, 0.12, 0.34, 0.60, 0.82)
    swing = smoother(u, 0.06, 0.40) * (1 - smoother(u, 0.56, 0.90))
    yaw = near(72) * swing
    p = pose(hips=(0.0, near(0.06), 0.16), hipsrot=(near(-6) * swing, 6, -yaw * 0.8 + near(14) * (1 - swing)),
             spine=(near(-4), 6, -yaw * 0.12), head=(0, -10, near(10) - yaw * 0.08),
             **feet(tuple(PEG[SIDE['near']]), (-0.80, 0.55 * hs, 0.80)))
    return p, leg, dict(yaw=yaw, roll=near(12) * swing, steer=near(14) * swing, pivot=(0.05, 0.0, 0.75))


def t_lazy_boy(u):
    p = pose(hips=(0.10, 0, 0.10), hipsrot=(0, -92, 0), spine=(0, -6, 0), head=(0, 22, 0),
             **_hands((-1.3, 0.2, 1.12), (-1.3, -0.2, 1.12)), **_feet((0.95, 0.24, 0.86), (0.95, -0.24, 0.86)))
    return p, hold(u, 0.14, 0.40, 0.58, 0.84), {}


def t_one_hander(u):
    # Near hand off the bar and thrown high.
    w = math.sin(u * 22) * 0.08
    hand = (-0.1 + w, near(0.55), 1.8)
    hands = _hands(hand, tuple(GRIP['R'])) if SIDE['hs'] > 0 else _hands(tuple(GRIP['L']), hand)
    p = pose(hips=(0.0, 0, 0.06), spine=(0, -8, near(-10)), head=(0, 0, near(20)), **hands)
    return p, hold(u), {}


def t_seat_grab(u):
    # Superman seat grab: near hand lets go of the bar and holds the seat, body stretched out behind the bike.
    hs = SIDE['hs']
    seat = (-0.40, 0.13 * hs, 0.84)
    hands = _hands(seat, tuple(GRIP['R'])) if hs > 0 else _hands(tuple(GRIP['L']), seat)
    p = pose(hips=(-0.41, near(0.04), 0.29), hipsrot=(0, 78, near(34)), head=(0, -55, near(-20)),
             **_feet((-1.9, 0.1 + 0.25 * hs, 1.36), (-1.9, -0.1 + 0.25 * hs, 1.36)), **hands)
    return p, hold(u), {}


def t_indian_air(u):
    # A superman with the legs crossed and scissoring, the body twisted round onto its side (Boss, 14 November: more
    # twist). The legs cross over each other and swap twice.
    k = 0.10 * math.sin((u - 0.2) * 2 * math.pi * 2.2)
    twist = sm(u, 0.16, 0.36) * (1 - sm(u, 0.60, 0.80))
    cross = 0.12 * twist
    p = pose(hips=(-0.30, near(0.05) * twist, 0.37), hipsrot=(near(55) * twist, 75, near(22) * twist),
             spine=(near(22) * twist, 0, near(14) * twist), head=(near(-26) * twist, -58, near(-12) * twist),
             **_side((-1.55, 0.10 - 0.10 * twist - cross, 1.36 + k + 0.06 * twist), (-1.62, -0.10 + 0.10 * twist + cross, 1.30 - k), _feet))
    return p, hold(u), {}


def t_rock_solid(u):
    # From a seat grab, both hands let go for a moment and the rider floats free above the bike.
    hs = SIDE['hs']
    free = bump(u, 0.36, 0.44, 0.56, 0.64)
    seat = Vector((-0.40, 0.13 * hs, 0.84)); out_n = Vector((-0.75, 0.7 * hs, 1.0)); out_f = Vector((-0.2, -0.7 * hs, 1.25))
    grip_f = GRIP['R'] if hs > 0 else GRIP['L']
    n = tuple(seat.lerp(out_n, free)); f = tuple(grip_f.lerp(out_f, free))
    hands = _hands(n, f) if hs > 0 else _hands(f, n)
    p = pose(hips=(-0.41, near(0.04), 0.29 + 0.05 * free), hipsrot=(0, 78, near(34) * (1 - free)), head=(0, -55, near(-20) * (1 - free)),
             **_feet((-1.9, 0.1 + 0.25 * hs * (1 - free), 1.36), (-1.9, -0.1 + 0.25 * hs * (1 - free), 1.36)), **hands)
    return p, hold(u), {}


def t_scrub(u):
    # Staying low (adbmag, "How to motocross scrub"): the bike is leaned over and turned sideways while still on the
    # face, bars turned into the scrub, so it soaks up the kick and leaves the lip already laid over; the rider's weight
    # goes to the high side, the low knee presses into the bike, the high foot comes off its peg with the knee tucked up
    # under the elbow. In the air it is brought back upright and pointed straight for the landing. Starts on the face.
    w = bump(u, -0.40, -0.02, 0.30, 0.72)
    p = pose(hips=(0.0, near(-0.08), 0.02), spine=(near(30), 28, near(-6)), head=(near(42), -18, near(14)),
             **_side((0.05, 0.165, 0.31), (-0.06, -0.30, 0.64), _feet))
    return p, w, dict(roll=near(-74) * w, yaw=near(-24) * w, steer=near(-28) * w)


def t_whip(u):
    # (Transmoto, "How-To: Whip with Ben Townley", and Red Bull's trick list.) An arc up the face starts the lean; at the
    # lip the bars are counter-steered and the back end swings out and up about the steering head; at the peak the bike
    # is laid over nearly flat and sideways to the travel, the bars turned back toward the landing, the rider up on the
    # high side, low foot firm on its peg, high leg bent, looking at the landing. The throttle is shut at the lip (the
    # rear wheel slows) and opened hard past the peak, which pulls the back end down and into line.
    # The back end leads: it starts swinging out at the lip, the lay-down follows it, and on the way back the back end
    # comes into line first while the bike is still picking itself up (Boss, 14 November: the back end didn't flow).
    arc = bump(u, -0.30, -0.06, 0.04, 0.20)
    swing = smoother(u, -0.06, 0.32) * (1 - smoother(u, 0.46, 0.80))
    w = smoother(u, 0.0, 0.42) * (1 - smoother(u, 0.52, 0.88))
    roll = near(-16) * arc * (1 - w) + near(-82) * w
    yaw = near(-6) * arc * (1 - swing) + near(-50) * swing
    p = pose(hips=(0.0, near(-0.07), 0.08), spine=(near(32), 20, near(8)), head=(near(44), -14, near(30)),
             **_side((0.05, 0.165, 0.31), (0.02, -0.165, 0.40), _feet))
    spin = 1.15 * (1 - 0.6 * bump(u, 0.0, 0.06, 0.34, 0.46)) + 1.5 * bump(u, 0.50, 0.58, 0.74, 0.86)
    return p, max(w, 0.45 * arc), dict(roll=roll, yaw=yaw, steer=near(-22) * swing, rear_spin=spin)


def t_backflip(u):
    # Tucked in and looking back for the landing through the middle of the rotation.
    w = bump(u, 0.02, 0.2, 0.7, 0.92)
    p = pose(hips=(-0.02, 0, 0.03), spine=(0, 8, 0), head=(0, -34, 0))
    return p, w, {}


# ================================================================= jumps
AIR_PIVOT = Vector((0.25, 0.0, 0.60))      # what a whip turns about in the air: near the steering head, so the back end swings
GROUND_PIVOT = Vector((0.0, 0.0, 0.0))    # on the ground the bike leans over about its tyres


def turn_about(d, pivot):
    """Moves the chassis so that its roll and yaw turn the bike about `pivot` (bike frame) instead of the rear axle,
    which is where the chassis bone sits."""
    if abs(d['roll']) < 1e-6 and abs(d['yaw']) < 1e-6:
        return
    p = pivot - Vector((RA[0], 0.0, RA[1]))
    full = C._quat((('X', d['roll']), ('Y', d['pitch']), ('Z', d['yaw'])))
    plain = C._quat((('Y', d['pitch']),))
    move = plain @ p - full @ p
    d['dx'] += move.x; d['dy'] += move.y; d['dz'] += move.z


def build_jump(trick=t_plain, angle=38.0, lip=0.5, land_angle=31.0, land_drop=0.0, ramp='steel', flip=0, name='Jump', nose_hold=0.15,
               pop=1.0, face=0.0, speed=1.0):
    """Returns (frames, list of states, props). One keyed frame every 1/24 s.
    pop: the share of the ramp's upward kick the bike keeps (a scrub soaks up the face and stays low).
    face: how long before the lip the trick starts, as a share of the flight (a whip and a scrub start on the face).
    speed: riding speed as a share of the normal speed (a scrub comes in faster, so it covers the same gap lower)."""
    vel = V * speed
    dt = 1.0 / FPS
    Ta = 0.6                                    # time to reach riding speed
    tan_a = math.tan(math.radians(angle))
    # --- take-off ramp: a circular arc from flat to `angle`
    Rarc = lip / (1 - math.cos(math.radians(angle)))
    Lr = Rarc * math.sin(math.radians(angle))
    def ramp_fn(x):
        x = min(max(x, 0.0), Lr)
        return Rarc - math.sqrt(Rarc * Rarc - x * x)
    s_at = lambda t: vel * (t - Ta / 2) if t >= Ta else vel * (t * t * t / (Ta * Ta) - t ** 4 / (2 * Ta ** 3))   # integral of vel*smoothstep
    xc0 = RA[0] + CREL[0]
    base = xc0 + s_at(Ta) + 0.35 + (FA[0] - xc0)
    lipx = base + Lr
    terr = Terrain().add(base, lipx, ramp_fn, ramp)
    ghost = lambda x: lip + tan_a * (x - lipx) if lipx < x < lipx + 1.6 else 0.0
    # --- ground phase up to the lip
    frames = []
    k = 0
    while True:
        t = k * dt; xc = xc0 + s_at(t)
        phi, xr, zr = ground_pose(terr, xc, ghost)
        if xr >= lipx:
            break
        frames.append(dict(t=t, s=s_at(t), phi=phi, xr=xr, zr=zr, air=0.0))
        k += 1
    kL = k - 1; tL = kL * dt
    L = frames[kL]
    cL = rot(CREL, L['phi']); zcL = L['zr'] + cL[1]
    vz0 = vel * tan_a * pop
    # --- flight
    disc = vz0 * vz0 + 2 * G * land_drop
    T = (vz0 + math.sqrt(disc)) / G
    nT = int(round(T / dt)); T = nT * dt
    droop_r, droop_f = 0.03, 0.045
    phi_td = land_angle - 4.0
    turn = (phi_td - 360.0 * flip) - L['phi']
    def air_phi(u):
        if flip:
            return L['phi'] + turn * (0.45 * u + 0.55 * sm(u, 0.0, 1.0))
        return L['phi'] + turn * sm(u, nose_hold, 0.97)
    zc_air = lambda tau: zcL + vz0 * tau - 0.5 * G * tau * tau
    # --- landing mound, placed so the wheels meet its down slope at T
    tan_b = math.tan(math.radians(land_angle))
    top_h = lip - land_drop + 0.30; up_len = 0.55; top_len = 0.5; toe = 0.5
    down_len = top_h / tan_b
    def mound_fn(x):
        if x < up_len:
            return top_h * sm(x, 0, up_len)
        if x < up_len + top_len:
            return top_h
        y = x - up_len - top_len
        hgt = top_h - tan_b * y
        # round the knuckle at the top and the run-out at the bottom
        knuckle = 0.35
        if y < knuckle:
            hgt = top_h - tan_b * (y * y / (2 * knuckle))
        else:
            hgt = top_h - tan_b * (y - knuckle / 2)
        if hgt < 0.16:
            hgt = 0.16 * math.exp((hgt - 0.16) / 0.16 * 1.0)
            if hgt < 0.004:
                hgt = 0.0
        return max(0.0, hgt)
    mound_len = up_len + top_len + down_len + 1.6
    sT = s_at(tL + T); xcT = xc0 + sT
    target = zc_air(T) - droop_r
    def zc_ground(x0):
        tt = Terrain().add(x0, x0 + mound_len, mound_fn)
        phi, xr, zr = ground_pose(tt, xcT)
        return zr + rot(CREL, phi)[1]
    lo = xcT - (up_len + top_len + down_len) - 0.2; hi = xcT - (up_len + top_len) - 0.45
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        if zc_ground(mid) < target:
            lo = mid
        else:
            hi = mid
    mound_x = 0.5 * (lo + hi)
    terr.add(mound_x, mound_x + mound_len, mound_fn, 'dirt')
    for i in range(1, nT):
        tau = i * dt; t = tL + tau
        phi = air_phi(tau / T); c = rot(CREL, phi)
        xc = xc0 + s_at(t)
        frames.append(dict(t=t, s=s_at(t), phi=phi, xr=xc - c[0], zr=zc_air(tau) - c[1], air=1.0, u=tau / T))
    kT = kL + nT; tT = kT * dt
    # --- roll-out and braking
    toe_x = mound_x + up_len + top_len + down_len + 0.9
    t_brake = None; Tb = 0.8
    k = kT
    s = sT; v = vel
    gT = None
    while True:
        t = k * dt
        if t_brake is None and (xc0 + s - CREL[0]) > toe_x:
            t_brake = t
        v = vel if t_brake is None else vel * (1 - sm(t, t_brake, t_brake + Tb))
        xc = xc0 + s
        phi, xr, zr = ground_pose(terr, xc)
        if gT is None:
            gT = phi
        # carry the last of the flight attitude into the first frames on the ground (rear wheel touches first)
        carry = (air_phi(1.0) + 360.0 * flip - gT) * (1 - sm(t, tT, tT + 0.14))
        frames.append(dict(t=t, s=s, phi=phi + carry - 360.0 * flip, xr=xr, zr=zr, air=0.0, v=v))
        if t_brake is not None and t >= t_brake + Tb + 0.2:
            break
        s += v * dt
        k += 1
    N = len(frames) - 1
    if N % 2:
        frames.append(dict(frames[-1], t=frames[-1]['t'] + dt)); N += 1
    t_end = N * dt
    # --- states
    t_load0 = None
    for fr in frames:
        if fr['xr'] + FREL[0] >= base and t_load0 is None:
            t_load0 = fr['t']
    out = []; wr = wf = 0.0; prev_s = 0.0
    land_v = abs(vz0 - G * T + vel * tan_b) / 3.0             # how hard the landing is, roughly 0..1
    A_r = 0.055 + 0.05 * min(1.0, land_v); A_f = 0.06 + 0.05 * min(1.0, land_v)
    for i, fr in enumerate(frames):
        t = fr['t']; d = state(); u = fr.get('u', 0.0)
        tr = None
        if fr['air'] == 1.0:
            tr = trick(u)
        elif face > 0 and t <= tL + 1e-6 and t > tL - face * T:
            tr = trick((t - tL) / T)                  # on the face, before the lip
        c = rot(CREL, fr['phi'])
        d['pitch'] = fr['phi']; d['air'] = fr['air']
        d['dx'] = fr['xr'] - (fr['s'] + RA[0]); d['dz'] = fr['zr'] - RA[1]
        # suspension
        kick = bump(t, 0, 0.25, 0.33, 0.7)
        load = bump(t, t_load0, t_load0 + 0.14, tL - 0.12, tL)
        flying = sm(t, tL, tL + 0.12) if t < tT else 0.0
        tau = t - tT
        after = (1 - sm(tau, 0, 0.07)) if t >= tT else 0.0
        gout = bump(t, tT + down_len / vel * 0.55, tT + down_len / vel * 0.55 + 0.2, tT + down_len / vel * 0.55 + 0.3, tT + down_len / vel * 0.55 + 0.6) if t > tT else 0.0
        brake = bump(t, t_brake, t_brake + 0.3, t_brake + Tb - 0.2, t_brake + Tb + 0.25) if t_brake is not None else 0.0
        d['squat'] = 0.02 * kick + 0.05 * load - droop_r * (flying + after) + A_r * shock(tau) + 0.02 * gout
        d['dive'] = -0.01 * kick + 0.055 * load - droop_f * (flying + after) + A_f * shock(tau - 0.03) + 0.025 * gout + 0.04 * brake
        if fr['air'] == 0.0 and t < tL:
            d['pitch'] += -4 * kick
        ds = fr['s'] - prev_s; prev_s = fr['s']
        spin = tr[2].get('rear_spin', 1.15) if tr is not None else 1.15
        wr += math.degrees(ds / RR) if fr['air'] == 0 else math.degrees(vel * dt / RR) * spin
        wf += math.degrees(ds / RF) if fr['air'] == 0 else math.degrees(vel * dt / RF) * (1 - 0.5 * u)
        d['wr'] = wr; d['wf'] = wf
        # rider
        up = sm(t, 0.25, Ta + 0.1)
        base_pose = blend(blend(REST, SEATED_FWD, bump(t, 0, 0.3, 0.4, 0.8)), ATTACK, up)
        base_pose = blend(base_pose, ABSORB, 0.55 * load)
        pop = bump(t, tL - 0.1, tL + 0.05, tL + 0.15, tL + 0.4)
        base_pose = blend(base_pose, pose(hips=(0.05, 0, 0.16), spine=(0, 12, 0), head=(0, -10, 0)), 0.7 * pop)
        if tr is not None:
            tp, w, extra = tr
            base_pose = blend(base_pose, tp, w)
            for kx, vx in extra.items():
                if kx not in ('rear_spin', 'pivot'):
                    d[kx] = vx
            turn_about(d, Vector(extra['pivot']) if 'pivot' in extra and fr['air'] == 1.0 else AIR_PIVOT if fr['air'] == 1.0 else GROUND_PIVOT)
        hit = shock(tau, 0.14, 0.8)
        base_pose = blend(base_pose, ABSORB, max(0.0, min(1.0, 0.85 * hit)))
        if t_brake is not None:
            down = sm(t, t_brake + 0.15, t_brake + Tb)
            base_pose = blend(base_pose, blend(REST, pose(spine=(0, 5, 0)), bump(t, t_brake, t_brake + 0.4, t_brake + Tb - 0.2, t_end)), down)
        for kx in RIDER_KEYS:
            d[kx] = base_pose[kx]
        # keep the eyes on where the bike is going instead of letting the head swing with the bike
        if not flip:
            hp = d['head']; d['head'] = (hp[0], hp[1] - 0.45 * max(-40, min(40, fr['phi'])), hp[2])
        d['_s'] = fr['s']
        out.append(d)
    props = [(piece, piece[0]) for piece in terr.pieces]
    info = dict(tL=tL, T=T, tT=tT, t_brake=t_brake, mound_x=mound_x, lipx=lipx, N=N, apex=zcL + vz0 * vz0 / (2 * G))
    return out, terr, info



# ================================================================= supercross whoops
def build_whoops(count=7, pitch_len=1.3, height=0.30, speed=3.3):
    """Blitzing a whoop section: the wheels skim from top to top, the suspension does the work and the bike stays almost level."""
    dt = 1.0 / FPS; Ta = 0.6; Tb = 0.8
    xc0 = RA[0] + CREL[0]
    s_at = lambda t: speed * (t - Ta / 2) if t >= Ta else speed * (t * t * t / (Ta * Ta) - t ** 4 / (2 * Ta ** 3))
    x0 = xc0 + s_at(Ta) + 0.3 + (FA[0] - xc0); x1 = x0 + count * pitch_len
    terr = Terrain().add(x0, x1, lambda x: height * (0.5 - 0.5 * math.cos(2 * math.pi * x / pitch_len)))
    skim = 0.58 * height
    floor = lambda x: skim * sm(x, x0 + 0.15 * pitch_len, x0 + 0.5 * pitch_len) * (1 - sm(x, x1 - 0.55 * pitch_len, x1 - 0.1 * pitch_len))
    raw = []; k = 0; s = 0.0; t_brake = None
    while True:
        t = k * dt
        if t_brake is None:
            s = s_at(t); v = speed if t >= Ta else speed * sm(t, 0, Ta)
            if xc0 + s - CREL[0] - RR > x1 + 0.35:
                t_brake = t
        if t_brake is not None:
            v = speed * (1 - sm(t, t_brake, t_brake + Tb))
        phi, xr, zr = ground_pose(terr, xc0 + s, floor)
        f = rot(FREL, phi)
        raw.append(dict(t=t, s=s, phi=phi, xr=xr, zr=zr, zf=zr + f[1], v=v))
        if t_brake is not None and t >= t_brake + Tb + 0.2:
            break
        if t_brake is not None:
            s += v * dt
        k += 1
    if (len(raw) - 1) % 2:
        raw.append(dict(raw[-1], t=raw[-1]['t'] + dt))
    n = len(raw)
    def smooth(key, i, r=4):
        tot = w = 0.0
        for j in range(i - r, i + r + 1):
            jj = min(n - 1, max(0, j)); ww = r + 1 - abs(j - i)
            tot += raw[jj][key] * ww; w += ww
        return tot / w
    out = []; wr = wf = 0.0; prev = 0.0
    t_in = (x0 - (xc0 + FREL[0] - CREL[0])) / speed + Ta / 2          # roughly when the front wheel reaches the first whoop
    for i, fr in enumerate(raw):
        t = fr['t']; d = state()
        inside = sm(t, t_in - 0.1, t_in + 0.25) * (1 - (sm(t, t_brake - 0.15, t_brake + 0.2) if t_brake is not None else 0.0))
        zr_s = smooth('zr', i); zf_s = smooth('zf', i)
        kick = bump(t, 0, 0.25, 0.33, 0.7)
        brake = bump(t, t_brake, t_brake + 0.3, t_brake + Tb - 0.2, t_brake + Tb + 0.2) if t_brake is not None else 0.0
        d['pitch'] = fr['phi'] - 4 * kick
        d['dx'] = fr['xr'] - (fr['s'] + RA[0]); d['dz'] = fr['zr'] - RA[1]
        # the wheels follow the ground, the chassis follows a smoothed path: the difference is suspension travel
        d['squat'] = max(-0.04, min(0.11, (fr['zr'] - zr_s) * 1.0 + 0.022 * inside + 0.02 * kick))
        d['dive'] = max(-0.05, min(0.12, (fr['zf'] - zf_s) / 0.9 - 0.03 * inside - 0.01 * kick + 0.04 * brake))
        ds = fr['s'] - prev; prev = fr['s']
        wr += math.degrees(ds / RR) * (1 + 0.12 * inside); wf += math.degrees(ds / RF)
        d['wr'] = wr; d['wf'] = wf
        up = sm(t, 0.25, Ta + 0.1)
        p = blend(blend(REST, SEATED_FWD, bump(t, 0, 0.3, 0.4, 0.8)), ATTACK, up)
        # through the whoops: weight back, legs working against the bike so the head stays level
        bob = (0.5 * (zr_s + zf_s) - 0.5 * (RA[1] + FA[1]) - skim * inside)
        p = blend(p, pose(hips=(-0.07, 0, 0.11 - 0.9 * bob), spine=(0, 26 + 60 * bob, 0), head=(0, -20, 0)), inside)
        if t_brake is not None:
            down = sm(t, t_brake + 0.15, t_brake + Tb)
            p = blend(p, blend(REST, pose(spine=(0, 5, 0)), bump(t, t_brake, t_brake + 0.4, t_brake + Tb - 0.2, t_brake + Tb + 0.2)), down)
        for kx in RIDER_KEYS:
            d[kx] = p[kx]
        hp = d['head']; d['head'] = (hp[0], hp[1] - 0.6 * fr['phi'], hp[2])
        d['_s'] = fr['s']
        out.append(d)
    return out, terr, dict(N=n - 1, t_brake=t_brake, x0=x0, x1=x1)


def build_whoops_wheelie(count=7, pitch_len=1.3, height=0.30, speed=3.3, angle=32.0):
    """Wheeling through the whoops (Boss's video, on rollers, 14 November 2026: the same technique). The bike comes in
    level; the face of the first whoop kicks the front wheel up, and from then on the front stays up while the rear
    wheel bounces from the top of one whoop to the top of the next, never dropping into the gaps between them; each
    hit nudges the nose up a little more and it settles again before the next. The rider stands with knees bent, arms
    nearly straight, weight back over the rear wheel. After the last whoop the front is set down and the rider brakes,
    as in Whoops. Same whoops prop as Whoops (Boss's second note: not riding down between each one)."""
    dt = 1.0 / FPS; Ta = 0.6; Tb = 0.8
    xc0 = RA[0] + CREL[0]
    s_at = lambda t: speed * (t - Ta / 2) if t >= Ta else speed * (t * t * t / (Ta * Ta) - t ** 4 / (2 * Ta ** 3))
    x0 = xc0 + s_at(Ta) + 0.3 + (FA[0] - xc0); x1 = x0 + count * pitch_len
    whoop = lambda x: height * (0.5 - 0.5 * math.cos(2 * math.pi * x / pitch_len))
    terr = Terrain().add(x0, x1, whoop)
    # The rear wheel's path: on the flat before, up the first face, then from crest to crest, falling only a little
    # (a short hop under gravity) between them, then down off the last one.
    crests = [x0 + (k + 0.5) * pitch_len for k in range(count)]
    def rear_path(x):
        ground = terr.axle(x, RR)
        if x < crests[0] or x > crests[-1]:
            return ground
        k = min(count - 2, int((x - crests[0]) // pitch_len)); a = crests[k]; f = (x - a) / pitch_len
        hop = 0.035 * math.sin(math.pi * f)          # a small bounce up off each top and down onto the next
        return max(ground, height + RR + hop)
    t_kick = (x0 + 0.3 * pitch_len - (RA[0] + FREL[0])) / speed + Ta / 2      # the front wheel meets the first face
    t_out = (crests[-1] + 0.4 - RA[0]) / speed + Ta / 2                        # the rear wheel leaves the last top
    raw = []; k = 0; s = 0.0; t_brake = None
    while True:
        t = k * dt
        if t_brake is None:
            s = s_at(t)
            if t >= t_out + 0.6:
                t_brake = t
        v = speed if t_brake is None else speed * (1 - sm(t, t_brake, t_brake + Tb))
        xr = RA[0] + s
        raw.append(dict(t=t, s=s, xr=xr, zr=rear_path(xr), v=v))
        if t_brake is not None and t >= t_brake + Tb + 0.25:
            break
        if t_brake is not None:
            s += v * dt
        k += 1
    if (len(raw) - 1) % 2:
        raw.append(dict(raw[-1], t=raw[-1]['t'] + dt))
    n = len(raw)
    out = []; wr = wf = 0.0; prev = 0.0; fspin = 1.0
    for i, fr in enumerate(raw):
        t = fr['t']; d = state()
        lift = smoother(t, t_kick, t_kick + 0.35) * (1 - smoother(t, t_out, t_out + 0.4))
        inside = sm(t, t_kick, t_kick + 0.2) * (1 - sm(t, t_out - 0.1, t_out + 0.2))
        # each top the rear wheel lands on nudges the nose up, and it settles before the next
        xr = fr['xr']; f = ((xr - crests[0]) / pitch_len) % 1.0 if crests[0] <= xr <= crests[-1] else 0.5
        nudge = 5.0 * math.exp(-((f if f < 0.5 else f - 1.0) ** 2) / 0.02) * inside
        kick = bump(t, 0, 0.25, 0.33, 0.7)
        drop = t - (t_out + 0.4)                                                     # the front wheel lands
        d['air'] = 1.0
        d['pitch'] = -(angle * lift + nudge) - 4 * kick
        d['dx'] = 0.0
        hit = nudge / 5.0
        d['dz'] = fr['zr'] - RA[1] - 0.025 * lift - 0.035 * hit
        brake = bump(t, t_brake, t_brake + 0.3, t_brake + Tb - 0.2, t_brake + Tb + 0.2) if t_brake is not None else 0.0
        d['squat'] = 0.025 * lift + 0.035 * hit + 0.02 * kick + 0.05 * shock(drop, 0.08, 0.5)
        d['dive'] = -0.045 * lift + 0.07 * shock(drop - 0.02, 0.08, 0.5) + 0.04 * brake
        ds = fr['s'] - prev; prev = fr['s']
        wr += math.degrees(ds / RR) * (1 + 0.15 * lift)
        fspin = 1.0 if lift < 0.05 else max(0.55, fspin - 0.004)
        wf += math.degrees(ds / RF) * fspin
        d['wr'] = wr; d['wf'] = wf
        up = sm(t, 0.25, Ta + 0.1)
        p = blend(blend(REST, SEATED_FWD, bump(t, 0, 0.3, 0.4, 0.8)), ATTACK, up)
        # standing, knees bent, arms nearly straight, weight back over the rear wheel; the knees take each hit
        wheel = pose(hips=(-0.03, 0, 0.24 - 0.05 * hit), hipsrot=(0, 18, 0), spine=(0, 8, 0), head=(0, -20, 0))
        p = blend(p, wheel, lift)
        p = blend(p, ABSORB, max(0.0, min(1.0, 0.6 * shock(drop, 0.12, 0.5))))
        if t_brake is not None:
            down = sm(t, t_brake + 0.15, t_brake + Tb)
            p = blend(p, blend(REST, pose(spine=(0, 5, 0)), bump(t, t_brake, t_brake + 0.4, t_brake + Tb - 0.2, t_brake + Tb + 0.25)), down)
        for kx in RIDER_KEYS:
            d[kx] = p[kx]
        hp = d['head']; d['head'] = (hp[0], hp[1] - 0.6 * d['pitch'], hp[2])
        d['_s'] = fr['s']
        out.append(d)
    return out, terr, dict(N=n - 1, t_brake=t_brake, x0=x0, x1=x1, t_kick=t_kick, t_out=t_out)


# ================================================================= stoppie
def build_stoppie(peak=40.0):
    """Hard on the front brake: the forks dive, the rear wheel comes up, the rider balances it and sets it back down."""
    dt = 1.0 / FPS; Ta = 0.6
    t0 = 0.95; t_up = 1.55; t_hold = 2.05; t_down = 2.5; t_end = 3.0
    fork = (0.449, -0.894)
    out = []; wr = wf = 0.0; s = 0.0
    N = int(round(t_end / dt))
    for i in range(N + 1):
        t = i * dt; d = state()
        v = V * sm(t, 0, Ta) * (1 - 0.82 * sm(t, t0, t0 + 0.5)) * (1 - sm(t, t_hold - 0.2, t_down))
        s += v * dt
        lift = sm(t, t0 + 0.12, t_up) * (1 - sm(t, t_hold, t_down))
        lift += 0.05 * math.sin((t - t_up) * 9) * bump(t, t_up, t_up + 0.1, t_hold - 0.1, t_hold)      # small corrections while balancing
        phi = peak * lift
        dive = 0.105 * bump(t, t0, t0 + 0.16, t_hold, t_down - 0.02) * (0.75 + 0.25 * (1 - lift))
        kick = bump(t, 0, 0.25, 0.33, 0.7)
        if t0 <= t < t_down:
            f = rot(FREL, phi); a = rot(fork, phi)
            d['air'] = 1.0; d['pitch'] = phi
            d['dx'] = FREL[0] - f[0] + dive * a[0]; d['dz'] = FREL[1] - f[1] + dive * a[1]
            d['dive'] = dive; d['squat'] = -0.035 * sm(t, t0, t0 + 0.2)
        else:
            tau = t - t_down
            d['pitch'] = -4 * kick
            d['squat'] = 0.02 * kick + (-0.035 * (1 - sm(tau, 0, 0.06)) + 0.07 * shock(tau, 0.08, 0.5) if tau >= 0 else 0.0)
            d['dive'] = -0.01 * kick + (0.02 * shock(tau - 0.04, 0.1, 0.45) if tau >= 0 else 0.0)
        wf += math.degrees(v * dt / RF)
        wr += math.degrees(v * dt / RR) * (1 - sm(t, t0 + 0.15, t0 + 0.5) * (1 - sm(t, t_down - 0.05, t_down)))
        d['wr'] = wr; d['wf'] = wf
        p = blend(REST, SEATED_FWD, bump(t, 0, 0.3, t0 - 0.1, t0 + 0.1))
        braking = bump(t, t0, t0 + 0.25, t_hold, t_down + 0.1)
        # arms braced, hips up and back, body leaning back against the pitch
        p = blend(p, pose(hips=(-0.10 - 0.04 * lift, 0, 0.03 + 0.10 * lift), hipsrot=(0, -22 * lift, 0), spine=(0, 10 - 6 * lift, 0), head=(0, -8 - 26 * lift, 0)), braking)
        p = blend(p, ABSORB, max(0.0, 0.5 * shock(t - t_down, 0.12, 0.45)))
        for kx in RIDER_KEYS:
            d[kx] = p[kx]
        d['_s'] = s
        out.append(d)
    return out, Terrain(), dict(N=N)

# ================================================================= the list
def animations():
    A = []
    def jump(name, **kw):
        A.append(dict(name=name, stage='air', build=lambda kw=kw, name=name: build_jump(name=name, **kw)))
    jump('Jump', trick=t_plain, angle=30.0, lip=0.5, land_angle=26.0, ramp='dirt')
    jump('Whip', trick=t_whip, angle=32.0, lip=0.55, land_angle=27.0, ramp='dirt', face=0.30)
    # The same jump as the whip (Boss, 14 November): it comes in faster and soaks up the kick, so it covers the same gap
    # at about half the height.
    jump('Scrub', trick=t_scrub, angle=32.0, lip=0.55, land_angle=27.0, ramp='dirt', nose_hold=0.0, pop=0.52, speed=1.38, face=0.40)
    jump('Backflip', trick=t_backflip, flip=1)
    jump('Superman', trick=t_superman)
    jump('SupermanSeatGrab', trick=t_seat_grab)
    jump('NoHander', trick=t_no_hander)
    jump('Nothing', trick=t_nothing)                      # NoFooter was taken out on 14 November (Boss)
    jump('BarHop', trick=t_bar_hop)
    jump('HeelClicker', trick=t_heel_clicker)
    jump('CanCan', trick=t_can_can)
    jump('NacNac', trick=t_nac_nac)
    jump('LazyBoy', trick=t_lazy_boy)
    jump('OneHander', trick=t_one_hander)
    jump('IndianAir', trick=t_indian_air)
    jump('RockSolid', trick=t_rock_solid)
    A.append(dict(name='Whoops', stage='ground', build=build_whoops))
    A.append(dict(name='WhoopsWheelie', stage='ground', build=build_whoops_wheelie))
    A.append(dict(name='Stoppie', stage='ground', build=build_stoppie))
    return A


# ================================================================= scene
def make_props(name, terr):
    obs = []
    for i, (x0, x1, fn, kind) in enumerate(terr.pieces):
        nm = 'DRP_%s_%d' % (name, i)
        ob = steel_ramp_prop(nm, x1 - x0, fn) if kind == 'steel' else dirt_prop(nm, x1 - x0, fn)
        obs.append((ob, x0))
    return obs


def build(side, names=None):
    configure(side)
    sc = bpy.context.scene; sc.render.fps = FPS
    for ob in [o for o in bpy.data.objects if o.name.startswith('DRP_')]:
        me = ob.data; bpy.data.objects.remove(ob); bpy.data.meshes.remove(me)
    rig = Rig(); rig.reset()
    for m in list(sc.timeline_markers):
        sc.timeline_markers.remove(m)
    f0 = 1; table = []
    for a in animations():
        if names and a['name'] not in names:
            continue
        states, terr, info = a['build']()
        N = len(states) - 1
        props = make_props(a['name'], terr)
        for ob, x0 in props:
            ob.location = PARK; ob.keyframe_insert('location', frame=f0 - 1)
        for t, d in enumerate(states):
            f = f0 + t
            rig.apply(f, d)
            for ob, x0 in props:
                ob.location = (x0 - d['_s'], 0, 0); ob.keyframe_insert('location', frame=f)
        for ob, x0 in props:
            ob.location = PARK; ob.keyframe_insert('location', frame=f0 + N + 1)
            for fc in _fcurves(ob):
                for kp in fc.keyframe_points:
                    kp.interpolation = 'CONSTANT' if kp.co[0] in (f0 - 1, f0 + N) else 'LINEAR'
        sc.timeline_markers.new(a['name'], frame=f0)
        table.append(dict(name=a['name'], start=f0, end=f0 + N, frames=N, loop=False, stage=a['stage'],
                          info={k: (round(v, 4) if isinstance(v, float) else v) for k, v in info.items()}))
        f0 += N + 1 + 4
    sc.frame_start = 1; sc.frame_end = max(1, f0 - 5)
    sc['dr_tricks'] = json.dumps(table); sc['dr_hand'] = SIDE['near']
    sc.frame_set(1)
    return table


def _fcurves(ob):
    ad = ob.animation_data
    if not ad or not ad.action:
        return []
    act = ad.action
    if hasattr(act, 'fcurves') and len(getattr(act, 'layers', [])) == 0:
        return list(act.fcurves)
    out = []
    for layer in act.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                out += list(bag.fcurves)
    return out


def camera(side, stage):
    sc = bpy.context.scene; cam = sc.camera
    st = STAGES[stage]
    wpx = STD['w'] + st['left'] + st['right']; hpx = STD['h'] + st['up']
    x0 = STD['x0'] - st['left'] / PPM; z1 = STD['z1'] + st['up'] / PPM
    w = wpx / PPM; h = hpx / PPM
    sc.render.resolution_x = wpx; sc.render.resolution_y = hpx; sc.render.resolution_percentage = 100
    sc.render.film_transparent = True; sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = max(w, h); cx = x0 + w / 2; cz = z1 - h / 2
    if side == 'Right':
        cam.location = (cx, -6, cz); cam.rotation_euler = Euler((math.pi / 2, 0, 0))
    else:
        cam.location = (cx, 6, cz); cam.rotation_euler = Euler((math.pi / 2, 0, math.pi))
    for o in ('race_bike_youth', 'race_rider'):
        bpy.data.objects[o].hide_render = False
    return wpx, hpx


def render(out, side, names=None, samples=16, step=2, only=None, scale=100, force=False):
    """Renders one animation at a time, with only that animation's props in the scene, which is several times quicker."""
    sc = bpy.context.scene
    configure(side)
    wanted = [a['name'] for a in animations() if not names or a['name'] in names]
    done = {}
    for name in wanted:
        a = build(side, [name])[0]
        sc.eevee.taa_render_samples = samples
        d = os.path.join(out, side, a['name']); os.makedirs(d, exist_ok=True)
        camera(side, a['stage']); sc.render.resolution_percentage = scale
        ts = list(range(0, a['frames'] + 1, step))
        for i, t in enumerate(ts):
            if only is not None and i not in only:
                continue
            path = os.path.join(d, '%04d.png' % i)
            if os.path.exists(path) and not force:
                continue
            sc.frame_set(a['start'] + t); sc.render.filepath = path; bpy.ops.render.render(write_still=True)
        done[a['name']] = len(ts)
        json.dump(dict(a, count=len(ts)), open(os.path.join(d, 'info.json'), 'w'), indent=1)
    return done


def main(argv):
    cmd = argv[0]
    opts = {}
    args = []
    it = iter(argv[1:])
    for a in it:
        if a.startswith('--'):
            opts[a[2:]] = next(it)
        else:
            args.append(a)
    bpy.ops.wm.open_mainfile(filepath=args[0])
    if cmd == 'build':
        table = build(opts.get('side', 'Right'))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args[1]))
        print(json.dumps(table))
    elif cmd == 'table':
        print('TABLE ' + json.dumps(build(opts.get('side', 'Right'), args[1:] or None)))
    elif cmd == 'render':
        only = [int(x) for x in opts['only'].split(',')] if 'only' in opts else None
        if 'part' in opts:                       # "i/n": this process renders every n-th frame, starting at i
            i, n = (int(x) for x in opts['part'].split('/'))
            only = [j for j in range(400) if j % n == i]
        print(render(args[1], args[2], args[3:] or None, int(opts.get('samples', 16)), int(opts.get('step', 2)), only, int(opts.get('scale', 100)), opts.get('force') == '1'))


if __name__ == '__main__':
    main(sys.argv[1:])
