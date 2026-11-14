"""Tests for prop_detail.py. Plain Python: python -m unittest discover -s Tools/Blender/tests"""
import math
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import prop_detail as P  # noqa: E402
import terrain_shapes as T  # noqa: E402


def landing_fn():
    return T.landing(0.45)


class DirtMeshTests(unittest.TestCase):
    def setUp(self):
        self.shape = landing_fn()
        self.verts, self.faces = P.dirt_mesh(self.shape.length, self.shape, seed=3)

    def test_is_one_closed_solid_plus_closed_clods(self):
        self.assertEqual(P.open_edges(self.faces), 0)
        self.assertGreater(len(self.faces), 100)

    def test_riding_line_keeps_the_outline_apart_from_the_groove(self):
        worst = 0.0
        for x, z in P.centre_line(self.verts, 0.03):
            if 0.05 < x < self.shape.length - 0.05 and self.shape(x) > 0.01:
                worst = max(worst, abs(z - self.shape(x)))
        self.assertLess(worst, 0.0075)                  # the 6 mm tyre groove, nothing more

    def test_sides_slope_out_and_never_stand_straight_up(self):
        top = max(self.verts, key=lambda v: v[2])
        feet = [v for v in self.verts if v[2] == 0.0 and abs(v[0] - top[0]) < 0.03]
        widest = max(abs(v[1]) for v in feet)
        self.assertGreater(widest, 0.45 + 0.35)         # a 0.45 m mound at 38 degrees reaches well past the top's edge

    def test_uses_all_three_dirt_colours(self):
        used = {m for _, m in self.faces}
        self.assertEqual(used, {P.DIRT, P.DAMP, P.DRY})
        self.assertEqual(len(P.MATERIALS), 3)

    def test_faces_are_damp_and_the_flat_top_is_dry(self):
        deck = 0.5 * (self.shape.marks['deck'] + self.shape.marks['knuckle'])
        face = self.shape.marks['slope'] + 0.2
        def material_near(x):
            for idx, mat in self.faces:
                vs = [self.verts[i] for i in idx]
                if len(vs) == 4 and min(v[0] for v in vs) <= x <= max(v[0] for v in vs) and all(0.15 < abs(v[1]) < 0.3 for v in vs):
                    return mat
        self.assertEqual(material_near(deck), P.DRY)
        self.assertEqual(material_near(face), P.DAMP)

    def test_clods_lie_beside_the_mound_on_the_ground(self):
        clod_verts = self.verts[-10 * 6:]
        self.assertTrue(all(v[2] >= 0.0 for v in clod_verts))
        bottoms = [v for v in clod_verts if v[2] == 0.0]
        self.assertEqual(len(bottoms), 10)
        for x, y, _ in bottoms:
            self.assertGreater(abs(y), 0.45)

    def test_same_seed_same_mesh(self):
        again = P.dirt_mesh(self.shape.length, self.shape, seed=3)
        self.assertEqual(again[0], self.verts)
        other = P.dirt_mesh(self.shape.length, self.shape, seed=4)
        self.assertNotEqual(other[0], self.verts)

    def test_whoops_with_valleys_on_the_ground_stay_one_piece(self):
        w = T.whoops(count=3)
        verts, faces = P.dirt_mesh(w.length, w, clods=0)
        self.assertEqual(P.open_edges(faces), 0)

    def test_flat_ground_makes_nothing(self):
        self.assertEqual(P.dirt_mesh(1.0, lambda x: 0.0), ([], []))

    def test_works_on_a_plain_function_outline(self):
        fn = lambda x: 0.3 * math.sin(math.pi * x / 2.0) if 0 <= x <= 2.0 else 0.0
        verts, faces = P.dirt_mesh(2.0, fn)
        self.assertEqual(P.open_edges(faces), 0)


class TakeoffBackTests(unittest.TestCase):
    def test_takeoff_ending_at_its_lip_gets_a_sloping_back_not_a_wall(self):
        ramp = T.takeoff(0.35)
        lip = ramp.marks['lip']
        cut = lambda x: ramp(x) if x <= lip else 0.0          # the shape ends at the lip, as the trick pieces do
        verts, faces = P.dirt_mesh(lip, cut, clods=0)
        self.assertEqual(P.open_edges(faces), 0)
        far = max(v[0] for v in verts)
        self.assertGreater(far, lip + 0.35 / math.tan(math.radians(38)) * 0.9)
        end = [v for v in verts if abs(v[0] - far) < 1e-9]
        self.assertTrue(all(v[2] < 0.003 for v in end))      # the last slice is at ground level

    def test_back_slope_falls_at_the_dirt_angle(self):
        xs, hs = P.back_slope(0.3, 0.03)
        self.assertEqual(hs[-1], 0.0)
        i = len(xs) // 2
        angle = math.degrees(math.atan2(hs[i - 1] - hs[i + 1], xs[i + 1] - xs[i - 1]))
        self.assertAlmostEqual(angle, 38.0, delta=1.0)

    def test_back_can_be_left_off(self):
        verts, _ = P.dirt_mesh(1.0, lambda x: 0.2 if 0 <= x <= 1.0 else 0.0, back=False, clods=0)
        self.assertLessEqual(max(v[0] for v in verts), 1.0 + 1e-9)


if __name__ == '__main__':
    unittest.main()
