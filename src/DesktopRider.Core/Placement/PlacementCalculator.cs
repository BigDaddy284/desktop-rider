using DesktopRider.Core.Configuration;

namespace DesktopRider.Core.Placement;

/// <summary>
/// Decides where the character sits on the desktop so that it always stays fully visible:
/// when first shown, after being dragged, and after monitors are added, removed or resized.
/// </summary>
public static class PlacementCalculator
{
    /// <summary>Gap, in pixels, left between the character and the screen corner in its default spot.</summary>
    public const int DefaultMargin = 48;

    /// <summary>The standard starting spot: the bottom-right corner of the main screen, just above the taskbar.</summary>
    public static PixelPoint DefaultPosition(PixelSize size, IReadOnlyList<ScreenArea> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);
        if (screens.Count == 0)
        {
            return new PixelPoint(0, 0);
        }

        var screen = screens.FirstOrDefault(s => s.IsPrimary) ?? screens[0];
        var area = screen.WorkingArea;
        var corner = new PixelPoint(area.Right - size.Width - DefaultMargin, area.Bottom - size.Height - DefaultMargin);
        return ClampTo(area, corner, size);
    }

    /// <summary>
    /// Works out where to show the character at start-up from the position saved last time.
    /// If that position is no longer on any screen (for example a monitor was unplugged), the default spot is used.
    /// </summary>
    public static PixelPoint Restore(SavedPosition? saved, PixelSize size, IReadOnlyList<ScreenArea> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);
        if (saved is null || screens.Count == 0 || !double.IsFinite(saved.X) || !double.IsFinite(saved.Y))
        {
            return DefaultPosition(size, screens);
        }

        var point = new PixelPoint(ToPixel(saved.X), ToPixel(saved.Y));
        var rect = PixelRect.From(point, size);

        // Prefer the monitor the character was on last time, as long as it is still at least partly there.
        var remembered = screens.FirstOrDefault(s => string.Equals(s.DeviceName, saved.Monitor, StringComparison.OrdinalIgnoreCase));
        if (remembered is not null && remembered.WorkingArea.OverlapArea(rect) > 0)
        {
            return ClampTo(remembered.WorkingArea, point, size);
        }

        return screens.Any(s => s.WorkingArea.OverlapArea(rect) > 0)
            ? KeepOnScreen(point, size, screens)
            : DefaultPosition(size, screens);
    }

    /// <summary>Nudges a position so the whole character is inside one screen, choosing the screen it is mostly on.</summary>
    public static PixelPoint KeepOnScreen(PixelPoint position, PixelSize size, IReadOnlyList<ScreenArea> screens)
    {
        var screen = ScreenFor(position, size, screens);
        return screen is null ? position : ClampTo(screen.WorkingArea, position, size);
    }

    /// <summary>The screen the character is mostly on, or the nearest one if it is on none. Null when there are no screens.</summary>
    public static ScreenArea? ScreenFor(PixelPoint position, PixelSize size, IReadOnlyList<ScreenArea> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);
        if (screens.Count == 0)
        {
            return null;
        }

        var rect = PixelRect.From(position, size);
        var mostOverlap = screens.MaxBy(s => s.WorkingArea.OverlapArea(rect))!;
        if (mostOverlap.WorkingArea.OverlapArea(rect) > 0)
        {
            return mostOverlap;
        }

        var centreX = rect.X + (rect.Width / 2.0);
        var centreY = rect.Y + (rect.Height / 2.0);
        return screens.MinBy(s => DistanceSquared(s.WorkingArea, centreX, centreY));
    }

    /// <summary>
    /// Works out where a speech bubble or the conversation window goes so that it sits next to the character
    /// and stays fully on the character's screen. Above is preferred, then left, right and below.
    /// If it fits nowhere, it goes above and is pushed back onto the screen, overlapping the character.
    /// </summary>
    /// <param name="anchor">Where the character is.</param>
    /// <param name="size">The size of the bubble or window to place.</param>
    /// <param name="screens">The screens available.</param>
    /// <param name="gap">Space left between the two, in pixels. It may be negative to tuck one into the other.</param>
    public static CompanionPlacement Beside(PixelRect anchor, PixelSize size, IReadOnlyList<ScreenArea> screens, int gap = 0)
    {
        ArgumentNullException.ThrowIfNull(screens);
        var centredX = anchor.X + ((anchor.Width - size.Width) / 2);
        var centredY = anchor.Y + ((anchor.Height - size.Height) / 2);
        var above = new PixelPoint(centredX, anchor.Y - gap - size.Height);

        var screen = ScreenFor(anchor.Location, anchor.Size, screens);
        if (screen is null)
        {
            return new CompanionPlacement(above, CompanionSide.Above);
        }

        var area = screen.WorkingArea;
        if (above.Y >= area.Y)
        {
            return new CompanionPlacement(ClampTo(area, above, size), CompanionSide.Above);
        }

        var left = new PixelPoint(anchor.X - gap - size.Width, centredY);
        if (left.X >= area.X)
        {
            return new CompanionPlacement(ClampTo(area, left, size), CompanionSide.Left);
        }

        var right = new PixelPoint(anchor.Right + gap, centredY);
        if (right.X + size.Width <= area.Right)
        {
            return new CompanionPlacement(ClampTo(area, right, size), CompanionSide.Right);
        }

        var below = new PixelPoint(centredX, anchor.Bottom + gap);
        if (below.Y + size.Height <= area.Bottom)
        {
            return new CompanionPlacement(ClampTo(area, below, size), CompanionSide.Below);
        }

        return new CompanionPlacement(ClampTo(area, above, size), CompanionSide.Above);
    }

    private static PixelPoint ClampTo(PixelRect area, PixelPoint position, PixelSize size)
    {
        // If the character is larger than the screen, its top-left corner stays visible.
        var maxX = Math.Max(area.X, area.Right - size.Width);
        var maxY = Math.Max(area.Y, area.Bottom - size.Height);
        return new PixelPoint(Math.Clamp(position.X, area.X, maxX), Math.Clamp(position.Y, area.Y, maxY));
    }

    private static double DistanceSquared(PixelRect area, double x, double y)
    {
        var dx = Math.Max(Math.Max(area.X - x, 0), x - area.Right);
        var dy = Math.Max(Math.Max(area.Y - y, 0), y - area.Bottom);
        return (dx * dx) + (dy * dy);
    }

    private static int ToPixel(double value) =>
        (int)Math.Clamp(Math.Round(value), int.MinValue / 2, int.MaxValue / 2);
}
