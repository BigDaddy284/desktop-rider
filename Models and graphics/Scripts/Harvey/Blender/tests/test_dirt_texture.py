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

    def test_unknown_kind_is_refused(self):
        with self.assertRaises(ValueError):
            D.make('gravel')


if __name__ == '__main__':
    unittest.main()
