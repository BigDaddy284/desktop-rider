#nullable enable

using System;
using DesktopRider.Core.Imaging;
using DesktopRider.Core.Placement;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class IconLayoutTests
    {
        [Test]
        public void OpaqueBounds_FindsTheDrawnPart()
        {
            var pixels = Picture(10, 8, (3, 2), (6, 5), (4, 3));

            Assert.Equal(new PixelRect(3, 2, 4, 4), IconLayout.OpaqueBounds(pixels, 10, 8, 40));
        }

        [Test]
        public void OpaqueBounds_OfAnEmptyPicture_IsNull()
        {
            Assert.Null(IconLayout.OpaqueBounds(new byte[10 * 8 * 4], 10, 8, 40));
        }

        [Test]
        public void OpaqueBounds_IgnoresNearlyInvisiblePixels()
        {
            var pixels = Picture(10, 8, (5, 5));
            pixels[3] = 15; // A faint speck in the top-left corner, below the threshold.

            Assert.Equal(new PixelRect(5, 5, 1, 1), IconLayout.OpaqueBounds(pixels, 10, 8, 40));
        }

        [Test]
        public void OpaqueBounds_CopesWithPaddedRows_AndFullPictures()
        {
            const int stride = 48; // Two spare pixels at the end of each row.
            var pixels = new byte[stride * 8];
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 12; x++)
                {
                    pixels[(y * stride) + (x * 4) + 3] = 255;
                }
            }

            Assert.Equal(new PixelRect(0, 0, 10, 8), IconLayout.OpaqueBounds(pixels, 10, 8, stride));
        }
        [TestCase(0, 8, 40)]
        [TestCase(10, 0, 40)]
        [TestCase(10, 8, 39)]
        [TestCase(10, 9, 40)]
        public void OpaqueBounds_RejectsSizesThatDoNotMatchTheData(int width, int height, int stride)
        {
            Assert.Throws<ArgumentException>(() => IconLayout.OpaqueBounds(new byte[10 * 8 * 4], width, height, stride));
        }
        [TestCase(400, 300, 32, 0, 4, 32, 24)] // Wide, like the bike: full width, centred vertically.
        [TestCase(300, 400, 32, 4, 0, 24, 32)] // Tall: full height, centred horizontally.
        [TestCase(50, 50, 32, 0, 0, 32, 32)]
        [TestCase(8, 4, 32, 0, 8, 32, 16)] // Smaller pictures are enlarged to fit.
        [TestCase(1000, 1, 16, 0, 7, 16, 1)] // Never thinner than one pixel.
        public void FitInSquare_KeepsTheShapeAndCentres(int width, int height, int size, int x, int y, int fitWidth, int fitHeight)
        {
            Assert.Equal(new PixelRect(x, y, fitWidth, fitHeight), IconLayout.FitInSquare(width, height, size));
        }
        [TestCase(0, 10, 16)]
        [TestCase(10, 0, 16)]
        [TestCase(10, 10, 0)]
        public void FitInSquare_RejectsImpossibleSizes(int width, int height, int size)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => IconLayout.FitInSquare(width, height, size));
        }

        private static byte[] Picture(int width, int height, params (int X, int Y)[] solid)
        {
            var pixels = new byte[width * height * 4];
            foreach (var (x, y) in solid)
            {
                pixels[(((y * width) + x) * 4) + 3] = 255;
            }

            return pixels;
        }
    }
}
