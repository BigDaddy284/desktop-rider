using System.Windows;
using System.Windows.Interop;

namespace DesktopRider.Desktop;

/// <summary>
/// Makes a window that is only there to be looked at: every click goes straight through it to whatever is behind,
/// it never takes the keyboard, and it does not appear when switching between programs.
/// </summary>
public static class ClickThroughWindow
{
    /// <summary>Call once the window has been created by Windows (from SourceInitialized or later).</summary>
    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        style |= NativeMethods.WsExTransparent | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new IntPtr(style));
    }
}
