using System.Windows;
using System.Windows.Interop;
using DesktopRider.Core.Placement;

namespace DesktopRider.Desktop;

/// <summary>
/// Keeps a small window, such as the speech bubble or the conversation window, next to the character:
/// when it appears, when it changes size and while the character is being dragged.
/// <see cref="PlacementCalculator.Beside"/> decides where; this class only carries it out.
/// </summary>
public sealed class CompanionWindowPlacer : IDisposable
{
    private readonly Window _companion;
    private readonly Window _anchor;
    private readonly IScreenProvider _screens;
    private readonly int _gap;
    private bool _attached;

    /// <param name="companion">The window to keep beside the character.</param>
    /// <param name="anchor">The character's window.</param>
    /// <param name="screens">The screens available.</param>
    /// <param name="gap">Space between the two in pixels. Negative tucks the companion into the character's empty margin.</param>
    public CompanionWindowPlacer(Window companion, Window anchor, IScreenProvider screens, int gap = 0)
    {
        _companion = companion ?? throw new ArgumentNullException(nameof(companion));
        _anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
        _screens = screens ?? throw new ArgumentNullException(nameof(screens));
        _gap = gap;
    }

    /// <summary>Raised when the companion moves to a different side of the character.</summary>
    public event EventHandler? SideChanged;

    /// <summary>Which side of the character the companion is on.</summary>
    public CompanionSide Side { get; private set; } = CompanionSide.Above;

    public void Attach()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _anchor.LocationChanged += OnChanged;
        _anchor.SizeChanged += OnSizeChanged;
        _companion.SizeChanged += OnSizeChanged;
        _companion.IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>Puts the companion where it belongs now. Does nothing while either window is not showing.</summary>
    public void Place()
    {
        if (!_attached || !_companion.IsVisible || !_anchor.IsVisible)
        {
            return;
        }

        var companion = new WindowInteropHelper(_companion).Handle;
        var anchor = new WindowInteropHelper(_anchor).Handle;
        if (companion == IntPtr.Zero
            || anchor == IntPtr.Zero
            || !NativeMethods.GetWindowRect(companion, out var own)
            || !NativeMethods.GetWindowRect(anchor, out var target))
        {
            return;
        }

        var placement = PlacementCalculator.Beside(
            new PixelRect(target.Left, target.Top, target.Right - target.Left, target.Bottom - target.Top),
            new PixelSize(own.Right - own.Left, own.Bottom - own.Top),
            _screens.GetScreens(),
            _gap);
        if (placement.Position != new PixelPoint(own.Left, own.Top))
        {
            NativeMethods.SetWindowPos(
                companion,
                IntPtr.Zero,
                placement.Position.X,
                placement.Position.Y,
                0,
                0,
                NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        }

        if (placement.Side != Side)
        {
            Side = placement.Side;
            SideChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_attached)
        {
            _attached = false;
            _anchor.LocationChanged -= OnChanged;
            _anchor.SizeChanged -= OnSizeChanged;
            _companion.SizeChanged -= OnSizeChanged;
            _companion.IsVisibleChanged -= OnIsVisibleChanged;
        }
    }

    private void OnChanged(object? sender, EventArgs e) => Place();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Place();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => Place();
}
