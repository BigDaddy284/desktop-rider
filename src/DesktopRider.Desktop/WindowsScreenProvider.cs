using DesktopRider.Core.Placement;

namespace DesktopRider.Desktop;

/// <summary>Reads the attached monitors from Windows.</summary>
public sealed class WindowsScreenProvider : IScreenProvider
{
    public IReadOnlyList<ScreenArea> GetScreens() =>
        [.. System.Windows.Forms.Screen.AllScreens.Select(screen => new ScreenArea(
            screen.DeviceName,
            new PixelRect(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height),
            screen.Primary))];
}
