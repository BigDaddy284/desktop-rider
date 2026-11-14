"""Tests for terrain_shapes.py. Plain Python, no Blender: python -m unittest discover -s Tools/Blender/tests"""
import math
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import terrain_shapes as T  # noqa: E402


def steps(profile, dx=0.005):
    n = int(profile.length / dx)
    return [i * dx for i in range(n + 1)]


class ProfileTests(unittest.TestCase):
    def test_interpolates_and_is_zero_outside(self):
        p = T.Profile([0, 1, 2], [0, 1, 0])
        self.assertAlmostEqual(p(0.5), 0.5)
        self.assertAlmostEqual(p(1.5), 0.5)
        self.assertEqual(p(-0.1), 0.0)
        self.assertEqual(p(2.1), 0.0)

    def test_rejects_points_going_backwards(self):
        with self.assertRaises(ValueError):
            T.Profile([0, 1, 1], [0, 1, 0])

    def test_then_joins_with_a_gap_and_moves_marks(self):
        a = T.Profile([0, 1], [0.2, 0.0], {'a': 0.5})
        b = T.Profile([0, 1], [0.0, 0.3], {'b': 0.25})
        c = a.then(b, gap=2.0)
        self.assertAlmostEqual(c.length, 4.0)
        self.assertAlmostEqual(c.marks['a'], 0.5)
        self.assertAlmostEqual(c.marks['b'], 3.25)
        self.assertEqual(c(2.0), 0.0)

    def test_plus_leaves_flat_ground_alone(self):
        p = T.Profile([0, 1, 2], [0, 0.5, 0])
        q = p.plus(lambda x: 0.1)
        self.assertEqual(q(0), 0.0)
        self.assertAlmostEqual(q(1), 0.6)


class TakeoffTests(unittest.TestCase):
    def test_leaves_at_the_face_angle_and_height(self):
        p = T.takeoff(0.35, angle=30)
        lip = p.marks['lip']
        self.assertAlmostEqual(p(lip), 0.35, places=3)
        self.assertAlmostEqual(p.slope(lip - 0.05), 30.0, delta=0.5)
        self.assertAlmostEqual(p.slope(p.marks['face'] + 0.02), 30.0, delta=0.5)

    def test_curve_tightens_as_it_rises(self):
        # A clothoid: the angle grows slowly at first, then faster. A single circle would grow evenly.
        p = T.takeoff(0.35, angle=30)
        face = p.marks['face']
        a1 = p.slope(face * 0.25); a2 = p.slope(face * 0.5); a3 = p.slope(face * 0.75)
        self.assertLess(a1, a2); self.assertLess(a2, a3)
        self.assertLess(a2 - a1, a3 - a2)

    def test_starts_flat_with_no_step(self):
        p = T.takeoff(0.35)
        self.assertLess(p(0.05), 0.002)
        self.assertLess(p.slope(0.02), 3.0)

    def test_back_slopes_at_the_dirt_angle_and_reaches_the_ground(self):
        p = T.takeoff(0.35)
        back = p.marks['lip'] + 0.15
        self.assertAlmostEqual(p.slope(back), -T.SIDE_ANGLE, delta=1.0)
        self.assertEqual(p.zs[-1], 0.0)

    def test_no_sudden_jumps_in_height(self):
        p = T.takeoff(0.35)
        xs = steps(p)
        for a, b in zip(xs, xs[1:]):
            self.assertLess(abs(p(b) - p(a)), 0.006)


class LandingTests(unittest.TestCase):
    def test_deck_is_flat_at_the_height(self):
        p = T.landing(0.45)
        mid = (p.marks['deck'] + p.marks['knuckle']) / 2
        self.assertAlmostEqual(p(mid), 0.45, places=3)
        self.assertAlmostEqual(p.slope(mid), 0.0, delta=0.5)

    def test_knuckle_is_rounded_not_a_corner(self):
        p = T.landing(0.45, face_angle=20)
        k = p.marks['knuckle']
        angles = [p.slope(k + d, 0.005) for d in (0.05, 0.12, 0.2, 0.28)]
        for a, b in zip(angles, angles[1:]):
            self.assertLess(b, a)                      # getting steeper downhill
        self.assertGreater(angles[0], -8.0)            # gently at first

    def test_face_steepens_slightly_then_eases_out(self):
        p = T.landing(0.45, face_angle=20, steepen=4)
        start = p.marks['slope']
        self.assertAlmostEqual(p.slope(start + 0.03), -20.0, delta=1.5)
        steepest = min(p.slope(x) for x in steps(p))
        self.assertGreater(steepest, -25.5)
        self.assertLess(steepest, -21.0)
        # the last part before the toe is much gentler: a smooth run-out, no ditch
        self.assertGreater(p.slope(p.marks['toe'] - 0.05), -8.0)
        self.assertLess(p(p.marks['toe']), 0.003)

    def test_never_peaked(self):
        p = T.landing(0.45)
        top = [x for x in steps(p) if p(x) > 0.449]
        self.assertGreater(top[-1] - top[0], 0.3)      # a flat top, not a point


class TabletopTests(unittest.TestCase):
    def test_takeoff_top_and_landing_in_order(self):
        p = T.tabletop(0.35, top=1.2)
        m = p.marks
        self.assertLess(m['face'], m['lip']); self.assertLess(m['lip'], m['knuckle'])
        self.assertLess(m['knuckle'], m['slope']); self.assertLess(m['slope'], m['toe'])
        self.assertAlmostEqual(m['knuckle'] - m['lip'], 1.2 + 0.01, delta=0.02)
        self.assertAlmostEqual(p((m['lip'] + m['knuckle']) / 2), 0.35, places=3)

    def test_jump_clears_the_top_at_thirty_degrees(self):
        # A 30 degree take-off in our world travels about 3 m, so a 1.2 m top is cleared with room to land on the slope.
        p = T.tabletop(0.35, top=1.2, angle=30)
        distance, apex, _ = T.flight(30, drop=0.1)
        self.assertGreater(p.marks['lip'] + distance, p.marks['knuckle'] + 0.3)
        self.assertAlmostEqual(apex, 0.433, places=2)


class WhoopsTests(unittest.TestCase):
    def test_count_spacing_and_size(self):
        p = T.whoops(count=7, pitch=1.3, height=0.28, seed=1)
        crests = [p.marks['crest%d' % k] for k in range(7)]
        gaps = [b - a for a, b in zip(crests, crests[1:])]
        for g in gaps:
            self.assertAlmostEqual(g, 1.3, delta=1.3 * 0.09)
        self.assertGreater(p.height, 0.2); self.assertLess(p.height, 0.31)

    def test_not_all_identical(self):
        p = T.whoops(seed=1)
        heights = [max(p(c + d) for d in (-0.08, -0.04, 0, 0.04, 0.08)) for c in (p.marks['crest%d' % k] for k in range(7))]
        self.assertGreater(max(heights) - min(heights), 0.003)

    def test_back_side_steeper_than_front(self):
        p = T.whoops(count=1, pitch=1.3, height=0.28, jitter=0.0, cup=0.0, skew=0.12)
        c = p.marks['crest0']
        up = max(p.slope(x) for x in steps(p) if x < c)
        down = min(p.slope(x) for x in steps(p) if x > c)
        self.assertGreater(abs(down), abs(up))

    def test_tops_are_cupped(self):
        p = T.whoops(count=1, pitch=1.3, height=0.28, jitter=0.0, cup=0.25)
        c = p.marks['crest0']
        self.assertLess(p(c), max(p(c - 0.1), p(c + 0.1)))

    def test_same_seed_same_shape(self):
        self.assertEqual(T.whoops(seed=5).zs, T.whoops(seed=5).zs)
        self.assertNotEqual(T.whoops(seed=5).zs, T.whoops(seed=6).zs)


class DetailTests(unittest.TestCase):
    def test_braking_bumps_grow_towards_the_stop(self):
        bumps = T.braking_bumps(length=1.2, spacing=0.22, height=0.012)
        early = max(bumps(x / 1000) for x in range(150, 400))
        late = max(bumps(x / 1000) for x in range(800, 1100))
        self.assertGreater(late, early)
        self.assertLessEqual(late, 0.0121)
        self.assertEqual(bumps(-0.1), 0.0); self.assertEqual(bumps(1.3), 0.0)

    def test_wobble_is_small_and_repeatable(self):
        w = T.wobble(seed=3, amount=0.004)
        values = [w(x / 100) for x in range(500)]
        self.assertLessEqual(max(abs(v) for v in values), 0.0041)
        self.assertGreater(max(values) - min(values), 0.002)
        self.assertEqual(values, [T.wobble(seed=3, amount=0.004)(x / 100) for x in range(500)])

    def test_section_has_sloped_sides_and_a_groove(self):
        s = T.Section(half_top=0.42, side_angle=38, groove_depth=0.012)
        pts = s.points(0.3)
        self.assertEqual(pts[0][1], 0.0); self.assertEqual(pts[-1][1], 0.0)
        self.assertAlmostEqual(pts[-1][0], s.half_base(0.3), places=6)
        self.assertAlmostEqual(-pts[0][0], s.half_base(0.3), places=6)
        foot, edge = pts[0], pts[4]
        side = math.degrees(math.atan2(edge[1] - foot[1], edge[0] - foot[0]))
        self.assertAlmostEqual(side, 38.0, delta=0.5)
        middle = min(pts, key=lambda p: abs(p[0]))
        self.assertLess(middle[1], 0.3)              # the tyre line is worn down
        self.assertGreater(max(z for _, z in pts), 0.3)   # with a small lip of pushed-up dirt beside it
        ys = [y for y, _ in pts]
        self.assertEqual(ys, sorted(ys))

    def test_berm_raises_the_outside(self):
        s = T.Section(berm=0.1, groove_depth=0.0)
        pts = s.points(0.3)
        tops = [p for p in pts if abs(p[0]) < 0.4]
        self.assertGreater(tops[-1][1], tops[0][1] + 0.08)

    def test_wear_colours(self):
        self.assertEqual(T.wear(25.0, 0.0, 0.3), 'damp')
        self.assertEqual(T.wear(0.0, -0.01, 0.3), 'damp')
        self.assertEqual(T.wear(1.0, 0.0, 0.3), 'dry')
        self.assertEqual(T.wear(8.0, 0.0, 0.3), 'dirt')

    def test_clods_gather_at_the_foot_of_the_landing(self):
        p = T.landing(0.45)
        lumps = T.clods(p, seed=4, count=40)
        self.assertEqual(len(lumps), 40)
        near = sum(1 for x, _, _ in lumps if abs(x - p.marks['toe']) < 0.8 or abs(x - p.marks['slope']) < 0.8)
        self.assertGreater(near, 30)
        self.assertEqual(lumps, T.clods(p, seed=4, count=40))

    def test_flight_matches_the_reference_table(self):
        for angle, dist, apex in ((25, 2.65, 0.31), (30, 3.00, 0.43), (38, 3.36, 0.66)):
            d, a, _ = T.flight(angle)
            self.assertAlmostEqual(d, dist, delta=0.01)
            self.assertAlmostEqual(a, apex, delta=0.01)


if __name__ == '__main__':
    unittest.main()
