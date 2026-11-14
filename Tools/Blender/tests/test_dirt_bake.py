"""Tests for dirt_bake.py. Needs numpy (Blender ships it): python -m unittest discover -s Tools/Blender/tests"""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
try:
    import numpy as np
    import dirt_bake as B
    import dirt_texture as D
except ImportError:            # numpy missing: skip rather than fail; Blender always has it
    np = None
import prop_detail as P  # noqa: E402
import terrain_shapes as T  # noqa: E402


@unittest.skipIf(np is None, 'numpy is not installed')
class DirtBakeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.shape = T.landing(0.45)
        cls.xs, cls.hs = P.outline(cls.shape.length, cls.shape)
        cls.foot = B.Footprint(-0.1, -1.0, cls.xs[-1] + 0.1, 1.0, ppm=100)
        cls.weights = B.wear_weights(cls.foot, cls.xs, cls.hs, 0.45, 0.006, seed=3)

    def column(self, x):
        return int((x - self.foot.x0) / self.foot.sx)

    def row(self, y):
        return int((self.foot.y1 - y) / self.foot.sy)

    def test_weights_add_up_to_one(self):
        total = sum(self.weights)
        self.assertTrue(np.allclose(total, 1.0))
        for w in self.weights:
            self.assertGreaterEqual(w.min(), -1e-9)

    def test_face_is_damp_top_is_dry_sides_are_loose(self):
        damp, dry, dirt = self.weights
        face = self.shape.marks['slope'] + 0.2
        deck = 0.5 * (self.shape.marks['deck'] + self.shape.marks['knuckle'])
        r = self.row(0.25)
        self.assertGreater(damp[r, self.column(face)], 0.5)
        self.assertGreater(dry[r, self.column(deck)], 0.5)
        self.assertGreater(dirt[self.row(0.8), self.column(deck)], 0.9)          # the side, beyond the top
        self.assertGreater(dirt[r, 0], 0.9)                                      # flat ground before the mound

    def test_wet_and_dry_fade_into_each_other(self):
        # somewhere between face and deck there are pixels that are partly both: no hard edge
        damp, dry, _ = self.weights
        mixed = (damp > 0.15) & (dry > 0.15)
        self.assertGreater(mixed.sum(), 10)

    def test_boundary_is_not_a_straight_line(self):
        damp, _, _ = self.weights
        cols = []
        for y in np.linspace(-0.35, 0.35, 15):
            line = damp[self.row(y)]
            on = np.where(line > 0.5)[0]
            cols.append(on.max())
        self.assertGreater(max(cols) - min(cols), 2)

    def test_bake_gives_images_of_the_footprint_size(self):
        tiles = {k: D.make_maps(k, 128, seed=1) for k in D.KINDS}
        colour, normal = B.bake(self.foot, self.weights, tiles)
        self.assertEqual(colour.shape, (self.foot.height, self.foot.width, 3))
        self.assertEqual(normal.shape, colour.shape)
        v = normal * 2 - 1
        self.assertTrue(np.allclose(np.linalg.norm(v, axis=-1), 1.0, atol=1e-6))
        # the damp face is darker than the dry top
        face = self.shape.marks['slope'] + 0.2
        deck = 0.5 * (self.shape.marks['deck'] + self.shape.marks['knuckle'])
        r = self.row(0.0)
        near = lambda x: colour[r - 3:r + 3, self.column(x) - 3:self.column(x) + 3].mean()
        self.assertLess(near(face), near(deck))

    def test_fast_groove_matches_the_shape(self):
        import terrain_shapes as TS
        s = TS.Section(half_top=0.45, groove_depth=0.006, groove_width=0.075)
        ys = np.linspace(-0.3, 0.3, 61)
        self.assertTrue(np.allclose(B.groove_depth_at(s, ys), [s.groove(y) for y in ys]))

    def test_footprint_uvs_cover_zero_to_one(self):
        f = self.foot
        self.assertEqual(f.uv(f.x0, f.y0), (0.0, 0.0))
        u, v = f.uv(f.x1, f.y1)
        self.assertAlmostEqual(u, 1.0); self.assertAlmostEqual(v, 1.0)
        self.assertLessEqual(B.Footprint(0, 0, 100, 1).width, B.MAX_SIZE)


if __name__ == '__main__':
    unittest.main()
