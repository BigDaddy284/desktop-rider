#nullable enable

using System;
using DesktopRider.Core.Placement;

namespace DesktopRider.Core.Imaging
{
    /// <summary>Works out how to turn one frame of the rider into a small square icon without distorting it.</summary>
    public static class IconLayout
    {
        /// <summary>
        /// Finds the smallest rectangle containing every pixel that is not (almost) transparent.
        /// Pixels are four bytes each with the alpha value last (BGRA). Returns null if the picture is empty.
        /// </summary>
        public static PixelRect? OpaqueBounds(ReadOnlySpan<byte> pixels, int width, int height, int stride, byte alphaThreshold = 16)
        {
            if (width <= 0 || height <= 0 || stride < width * 4 || pixels.Length < ((height - 1) * stride) + (width * 4))
            {
                throw new ArgumentException("The pixel data does not match the picture size.", nameof(pixels));
            }

            int left = width, top = height, right = -1, bottom = -1;
            for (var y = 0; y < height; y++)
            {
                var row = pixels.Slice(y * stride, width * 4);
                for (var x = 0; x < width; x++)
                {
                    if (row[(x * 4) + 3] >= alphaThreshold)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }
            }

            return right < 0 ? null : new PixelRect(left, top, right - left + 1, bottom - top + 1);
        }

        /// <summary>Where a picture of the given size should be drawn inside a square icon: as large as fits, centred, same shape.</summary>
        public static PixelRect FitInSquare(int sourceWidth, int sourceHeight, int iconSize)
        {
            Guard.Positive(sourceWidth, nameof(sourceWidth));
            Guard.Positive(sourceHeight, nameof(sourceHeight));
            Guard.Positive(iconSize, nameof(iconSize));

            var scale = Math.Min((double)iconSize / sourceWidth, (double)iconSize / sourceHeight);
            var width = Math.Clamp((int)Math.Round(sourceWidth * scale), 1, iconSize);
            var height = Math.Clamp((int)Math.Round(sourceHeight * scale), 1, iconSize);
            return new PixelRect((iconSize - width) / 2, (iconSize - height) / 2, width, height);
        }
    }
}
