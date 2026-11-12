namespace DesktopRider.Core.Placement;

/// <summary>A point on the desktop in physical screen pixels. Screens left of or above the main one have negative values.</summary>
public readonly record struct PixelPoint(int X, int Y);

public readonly record struct PixelSize(int Width, int Height);

/// <summary>A rectangle on the desktop in physical screen pixels.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public PixelPoint Location => new(X, Y);

    public PixelSize Size => new(Width, Height);

    public static PixelRect From(PixelPoint location, PixelSize size) => new(location.X, location.Y, size.Width, size.Height);

    /// <summary>The number of pixels this rectangle shares with another; zero when they do not overlap.</summary>
    public long OverlapArea(PixelRect other)
    {
        long width = Math.Min(Right, other.Right) - Math.Max(X, other.X);
        long height = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
        return width > 0 && height > 0 ? width * height : 0;
    }
}

/// <summary>One monitor. The working area is the part not covered by the taskbar.</summary>
public sealed record ScreenArea(string DeviceName, PixelRect WorkingArea, bool IsPrimary);

/// <summary>Supplies the monitors currently attached to the computer.</summary>
public interface IScreenProvider
{
    IReadOnlyList<ScreenArea> GetScreens();
}
