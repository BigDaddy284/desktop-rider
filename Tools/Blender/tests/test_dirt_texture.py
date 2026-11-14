"""Tests for dirt_texture.py. Needs numpy (Blender ships it): python -m unittest discover -s Tools/Blender/tests"""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
try:
    import numpy as np
    import dirt_texture as D
except ImportError:            # numpy missing: skip rather than fail; Blender always has it
    np = None


@unittest.skipIf(np is None, 'numpy is not installed')
class DirtTextureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.images = {k: D.make(k, 256, seed=1) for k in D.KINDS}

    def test_size_and_range(self):
        for k, im in self.images.items():
            self.assertEqual(im.shape, (256, 256, 3), k)
            self.assertGreaterEqual(im.min(), 0.0)
            self.assertLessEqual(im.max(), 1.0)

    def test_damp_is_darkest_and_dry_is_palest(self):
        m = {k: im.mean() for k, im in self.images.items()}
        self.assertLess(m['damp'], m['dirt'])
        self.assertLess(m['dirt'], m['dry'])

    def test_is_brown_not_grey(self):
        for k, im in self.images.items():
            r, g, b = im[..., 0].mean(), im[..., 1].mean(), im[..., 2].mean()
            self.assertGreater(r, g, k); self.assertGreater(g, b, k)

    def test_has_real_texture_not_flat_colour(self):
        for k, im in self.images.items():
            lum = im.mean(axis=2)
            self.assertGreater(lum.std(), 0.012, k)        # damp, packed dirt is meant to be the smoothest
            # fine detail: neighbouring pixels differ, as grain does
            self.assertGreater(np.abs(np.diff(lum, axis=1)).mean(), 0.005, k)

    def test_tiles_without_a_seam(self):
        for k, im in self.images.items():
            lum = im.mean(axis=2)
            inside = np.abs(lum[:, 1:] - lum[:, :-1]).mean()
            across = np.abs(lum[:, 0] - lum[:, -1]).mean()
            self.assertLess(across, inside * 2.5, k)
            inside_v = np.abs(lum[1:] - lum[:-1]).mean()
            across_v = np.abs(lum[0] - lum[-1]).mean()
            self.assertLess(across_v, inside_v * 2.5, k)

    def test_same_seed_same_image(self):
        self.assertTrue(np.array_equal(D.make('dirt', 64, 3), D.make('dirt', 64, 3)))
        self.assertFalse(np.array_equal(D.make('dirt', 64, 3), D.make('dirt', 64, 4)))

    def test_normal_map_is_unit_length_and_mostly_facing_up(self):
        for k in D.KINDS:
            _, n = D.make_maps(k, 128, seed=2)
            v = n * 2 - 1
            self.assertTrue(np.allclose(np.linalg.norm(v, axis=-1), 1.0, atol=1e-6), k)
            self.assertGreater(v[..., 2].mean(), 0.8, k)
            self.assertGreater(v[..., :2].std(), 0.02, k)          # it has bumps in it

    def test_normal_map_matches_the_height(self):
        # a single bump: on its right side the surface leans right (red up), above it leans up (green up)
        h = np.zeros((32, 32)); yy, xx = np.mgrid[0:32, 0:32]
        h += np.exp(-((xx - 16) ** 2 + (yy - 16) ** 2) / 20.0) * 0.01
        n = D.normal_map(h, 100.0)
        self.assertGreater(n[16, 20, 0], 0.5); self.assertLess(n[16, 12, 0], 0.5)
        self.assertGreater(n[12, 16, 1], 0.5); self.assertLess(n[20, 16, 1], 0.5)   # row 12 is above the bump

    def test_colours_are_real_dark_dirt_not_sand(self):
        # sRGB averages within the range of photographed wet-to-dry track dirt
        for k, im in self.images.items():
            self.assertLess(im.mean(), 0.6, k)
            self.assertGreater(im.mean(), 0.12, k)

    def test_unknown_kind_is_refused(self):
        with self.assertRaises(ValueError):
            D.make('gravel')


if __name__ == '__main__':
    unittest.main()
