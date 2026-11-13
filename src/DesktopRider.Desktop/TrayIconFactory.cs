using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopRider.Core.Imaging;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DesktopRider.Desktop;

/// <summary>Makes the notification-area icon from one frame of the rider's own artwork, so no separate picture is invented.</summary>
public static class TrayIconFactory
{
    /// <summary>
    /// Shrinks the drawn part of a frame to icon size. Returns null if the frame is empty or cannot be converted;
    /// the caller then falls back to the standard Windows icon.
    /// </summary>
    public static Drawing.Icon? TryCreate(BitmapSource? frame)
    {
        if (frame is null)
        {
            return null;
        }

        try
        {
            var source = new FormatConvertedBitmap(frame, PixelFormats.Pbgra32, null, 0);
            int width = source.PixelWidth, height = source.PixelHeight, stride = width * 4;
            var pixels = new byte[stride * height];
            source.CopyPixels(pixels, stride, 0);

            if (IconLayout.OpaqueBounds(pixels, width, height, stride) is not { } bounds)
            {
                return null;
            }

            // Twice the standard small-icon size keeps it sharp on high-resolution screens; Windows scales it down as needed.
            var size = Math.Max(Forms.SystemInformation.SmallIconSize.Width * 2, 32);
            var target = IconLayout.FitInSquare(bounds.Width, bounds.Height, size);

            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                using var full = new Drawing.Bitmap(width, height, stride, Drawing.Imaging.PixelFormat.Format32bppPArgb, handle.AddrOfPinnedObject());
                using var small = new Drawing.Bitmap(size, size, Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var graphics = Drawing.Graphics.FromImage(small))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.DrawImage(
                        full,
                        new Drawing.Rectangle(target.X, target.Y, target.Width, target.Height),
                        new Drawing.Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                        Drawing.GraphicsUnit.Pixel);
                }

                var iconHandle = small.GetHicon();
                try
                {
                    // Clone so the icon owns its own copy and the temporary handle can be released.
                    using var temporary = Drawing.Icon.FromHandle(iconHandle);
                    return (Drawing.Icon)temporary.Clone();
                }
                finally
                {
                    NativeMethods.DestroyIcon(iconHandle);
                }
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ExternalException or NotSupportedException)
        {
            return null;
        }
    }
}
