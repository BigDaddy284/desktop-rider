#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using DesktopRider.Core.Placement;

namespace DesktopRider.Core.Desktop
{
    /// <summary>
    /// The screens the rider can ride on, in desktop pixels measured from the top-left corner of the area that holds
    /// every screen, so every value is zero or more. Each rectangle is a screen's work area: the part the taskbar
    /// does not cover. Screens of different sizes can leave gaps between them; the rider is kept off those.
    /// </summary>
    public sealed class RidingArea
    {
        private readonly PixelRect[] _screens;

        /// <param name="screens">Work areas in desktop pixels. The first one is the main screen.</param>
        public RidingArea(IReadOnlyList<PixelRect> screens)
        {
            if (screens is null || screens.Count == 0)
            {
                throw new ArgumentException("At least one screen is needed.", nameof(screens));
            }

            _screens = new PixelRect[screens.Count];
            int right = 0, bottom = 0;
            for (int i = 0; i < screens.Count; i++)
            {
                _screens[i] = screens[i];
                right = Math.Max(right, screens[i].Right);
                bottom = Math.Max(bottom, screens[i].Bottom);
            }

            Size = new PixelSize(right, bottom);
        }

        /// <summary>One screen of the given size, with no taskbar.</summary>
        public static RidingArea Single(int width, int height) => new(new[] { new PixelRect(0, 0, width, height) });

        /// <summary>
        /// Takes work areas as Windows reports them, where the main screen's top-left is 0,0 and screens to its left or
        /// above have negative values, and moves them so the whole area starts at 0,0. The main screen goes first.
        /// <paramref name="offset"/> is what to add to a Windows position to get a desktop pixel.
        /// </summary>
        public static RidingArea FromWindows(IReadOnlyList<ScreenArea> screens, out PixelPoint offset)
        {
            if (screens is null || screens.Count == 0)
            {
                throw new ArgumentException("At least one screen is needed.", nameof(screens));
            }

            int left = int.MaxValue, top = int.MaxValue;
            foreach (var s in screens)
            {
                left = Math.Min(left, s.WorkingArea.X);
                top = Math.Min(top, s.WorkingArea.Y);
            }

            offset = new PixelPoint(-left, -top);
            var moved = new List<PixelRect>(screens.Count);
            foreach (var s in screens)
            {
                var r = new PixelRect(s.WorkingArea.X - left, s.WorkingArea.Y - top, s.WorkingArea.Width, s.WorkingArea.Height);
                if (s.IsPrimary)
                {
                    moved.Insert(0, r);
                }
                else
                {
                    moved.Add(r);
                }
            }

            return new RidingArea(moved);
        }

        public IReadOnlyList<PixelRect> Screens => _screens;

        /// <summary>The main screen's work area.</summary>
        public PixelRect Main => _screens[0];

        /// <summary>Width and height of the area that holds every screen.</summary>
        public PixelSize Size { get; }

        /// <summary>The screen the point is on, or the nearest one if it is in a gap or off every screen.</summary>
        public int ScreenAt(Vector2 point)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _screens.Length; i++)
            {
                float d = Vector2.DistanceSquared(Clamp(point, _screens[i]), point);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>True if the point is on a screen and at least <paramref name="margin"/> pixels in from its edges.</summary>
        public bool IsOnScreen(Vector2 point, float margin = 0f)
        {
            foreach (var s in _screens)
            {
                if (point.X >= s.X + margin && point.X <= s.X + s.Width - margin &&
                    point.Y >= s.Y + margin && point.Y <= s.Y + s.Height - margin)
                    return true;
            }

            return false;
        }

        /// <summary>The point itself if it is on a screen, otherwise the nearest point that is.</summary>
        public Vector2 KeepOnScreen(Vector2 point) => Clamp(point, _screens[ScreenAt(point)]);

        /// <summary>
        /// Somewhere to ride to: usually on the screen the rider is on, and with <paramref name="otherScreenChance"/>
        /// on one of the others. <paramref name="margin"/> is the share of each side kept clear so the rider is never half off.
        /// </summary>
        public Vector2 PickTarget(Vector2 from, Random random, float margin, double otherScreenChance = 0.25)
        {
            if (random is null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            int index = ScreenAt(from);
            if (_screens.Length > 1 && random.NextDouble() < otherScreenChance)
            {
                int other = random.Next(_screens.Length - 1);
                index = other >= index ? other + 1 : other;
            }

            var s = _screens[index];
            float x = Lerp(s.X + s.Width * margin, s.Right - s.Width * margin, (float)random.NextDouble());
            float y = Lerp(s.Y + s.Height * margin, s.Bottom - s.Height * margin, (float)random.NextDouble());
            return new Vector2(x, y);
        }

        /// <summary>Where a window of this size goes to be centred on a point, kept inside the area that holds every screen.</summary>
        public PixelPoint WindowCorner(Vector2 centre, PixelSize window)
        {
            int x = (int)Math.Round(centre.X - window.Width * 0.5f, MidpointRounding.AwayFromZero);
            int y = (int)Math.Round(centre.Y - window.Height * 0.5f, MidpointRounding.AwayFromZero);
            x = Math.Min(Math.Max(x, 0), Math.Max(0, Size.Width - window.Width));
            y = Math.Min(Math.Max(y, 0), Math.Max(0, Size.Height - window.Height));
            return new PixelPoint(x, y);
        }

        /// <summary>The middle of the main screen, where "Move back" puts the rider.</summary>
        public Vector2 MainCentre => new(Main.X + Main.Width * 0.5f, Main.Y + Main.Height * 0.5f);

        private static Vector2 Clamp(Vector2 p, PixelRect r) =>
            new(Math.Min(Math.Max(p.X, r.X), r.Right), Math.Min(Math.Max(p.Y, r.Y), r.Bottom));

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
