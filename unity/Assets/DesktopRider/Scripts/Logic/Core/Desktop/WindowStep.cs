#nullable enable

using System;

namespace DesktopRider.Core.Desktop
{
    /// <summary>A window's place and size in desktop pixels: top-left corner, width and height.</summary>
    public readonly struct WindowBox : IEquatable<WindowBox>
    {
        public WindowBox(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public bool SameSize(WindowBox other) => Width == other.Width && Height == other.Height;
        public bool Equals(WindowBox other) => X == other.X && Y == other.Y && SameSize(other);
        public override bool Equals(object? obj) => obj is WindowBox other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public override string ToString() => $"({X}, {Y}, {Width} x {Height})";
    }

    /// <summary>
    /// How the rider's window changes size without a flash. When the window changes size, Windows shows the last picture
    /// drawn, and Unity's next one, at the old size, pinned to the window's top-left corner, until Unity catches up a frame
    /// later (measured on Boss's PC, 14 November). So a change is made in steps that keep that picture right:
    /// a window that fits inside the picture Unity is drawing can move and shrink at once (the next picture is drawn for
    /// it, pinned to its corner); one that needs more room first grows where it is, so the old picture stays in place,
    /// and moves the frame after, once Unity draws at the bigger size.
    /// </summary>
    public static class WindowStep
    {
        /// <summary>
        /// The next place and size to give the window, going from <paramref name="now"/> towards <paramref name="wanted"/>,
        /// while Unity draws pictures of <paramref name="drawnWidth"/> x <paramref name="drawnHeight"/>.
        /// </summary>
        public static WindowBox Next(WindowBox now, WindowBox wanted, int drawnWidth, int drawnHeight)
        {
            if (wanted.Width <= drawnWidth && wanted.Height <= drawnHeight) return wanted;
            // Grow where it is, to hold both the picture being drawn and the size wanted; move once Unity has caught up.
            return new WindowBox(now.X, now.Y, Math.Max(drawnWidth, wanted.Width), Math.Max(drawnHeight, wanted.Height));
        }

        /// <summary>
        /// The part of the window a picture of <paramref name="drawnWidth"/> x <paramref name="drawnHeight"/> covers, pinned
        /// to the window's top-left corner, in pixels from the window's centre (x to the right, y up): left, right, bottom, top.
        /// Drawing the scene over exactly this part keeps everything where it belongs while the sizes differ.
        /// </summary>
        public static (double Left, double Right, double Bottom, double Top) DrawnPart(int windowWidth, int windowHeight, int drawnWidth, int drawnHeight)
        {
            double halfWidth = windowWidth / 2.0, halfHeight = windowHeight / 2.0;
            return (-halfWidth, drawnWidth - halfWidth, halfHeight - drawnHeight, halfHeight);
        }
    
        /// <summary>
        /// Where the window's top-left corner goes so the rider stays in it: it moves only as far as it must to keep the
        /// rider's own area (<paramref name="riderWidth"/> x <paramref name="riderHeight"/> round <paramref name="centreX"/>,
        /// <paramref name="centreY"/>) inside, and stays on the desktop. A window the size of the rider's area is centred on
        /// him; a bigger one (kept after a trick, so it never has to shrink) mostly stays still while he rides about in it.
        /// </summary>
        public static (int X, int Y) Follow(int x, int y, int width, int height, double centreX, double centreY,
            int riderWidth, int riderHeight, int desktopWidth, int desktopHeight)
        {
            x = Keep(x, width, centreX, Math.Min(riderWidth, width), desktopWidth);
            y = Keep(y, height, centreY, Math.Min(riderHeight, height), desktopHeight);
            return (x, y);
        }

        static int Keep(int at, int size, double centre, int rider, int desktop)
        {
            int lowest = (int)Math.Round(centre + rider * 0.5 - size, MidpointRounding.AwayFromZero);   // rider at the far edge
            int highest = (int)Math.Round(centre - rider * 0.5, MidpointRounding.AwayFromZero);        // rider at the near edge
            at = Math.Min(Math.Max(at, lowest), highest);
            return Math.Min(Math.Max(at, 0), Math.Max(0, desktop - size));
        }
    }
}
