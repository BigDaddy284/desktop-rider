using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Placement;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DesktopRider.Desktop;

/// <summary>
/// Looks after where the character's window sits: restores it at start-up, lets the user drag it,
/// keeps it fully on a screen, and remembers the spot for next time.
/// </summary>
public sealed partial class WindowPlacementController : IDisposable
{
    private readonly Window _window;
    private readonly ISettingsService _settings;
    private readonly IScreenProvider _screens;
    private readonly ILogger<WindowPlacementController> _logger;
    private bool _attached;

    public WindowPlacementController(Window window, ISettingsService settings, IScreenProvider screens, ILogger<WindowPlacementController> logger)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _screens = screens ?? throw new ArgumentNullException(nameof(screens));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private IntPtr Handle => new WindowInteropHelper(_window).Handle;

    public void Attach()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _window.Loaded += OnLoaded;
        _window.MouseLeftButtonDown += OnMouseLeftButtonDown;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        if (_window.IsLoaded)
        {
            RestorePosition();
        }
    }

    /// <summary>Sends the character back to its standard spot and forgets the saved position.</summary>
    public async Task ResetPositionAsync()
    {
        if (TryGetBounds(out var bounds))
        {
            MoveTo(PlacementCalculator.DefaultPosition(bounds.Size, _screens.GetScreens()));
        }

        await SaveAsync(s => s.Appearance.Position = null);
    }

    public void Dispose()
    {
        if (_attached)
        {
            _attached = false;
            _window.Loaded -= OnLoaded;
            _window.MouseLeftButtonDown -= OnMouseLeftButtonDown;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => RestorePosition();

    private void RestorePosition()
    {
        if (TryGetBounds(out var bounds))
        {
            MoveTo(PlacementCalculator.Restore(_settings.Current.Appearance.Position, bounds.Size, _screens.GetScreens()));
        }
    }

    private async void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!TryGetBounds(out var before))
        {
            return;
        }

        try
        {
            // Returns when the user lets go of the mouse button.
            _window.DragMove();
        }
        catch (InvalidOperationException)
        {
            // The button was released before the drag could begin.
            return;
        }

        if (!TryGetBounds(out var after) || after.Location == before.Location)
        {
            return;
        }

        var screens = _screens.GetScreens();
        var position = PlacementCalculator.KeepOnScreen(after.Location, after.Size, screens);
        if (position != after.Location)
        {
            MoveTo(position);
        }

        var monitor = PlacementCalculator.ScreenFor(position, after.Size, screens)?.DeviceName;
        await SaveAsync(s => s.Appearance.Position = new SavedPosition { X = position.X, Y = position.Y, Monitor = monitor });
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        _window.Dispatcher.BeginInvoke(() =>
        {
            // A monitor was added, removed or resized: make sure the character has not been left off-screen.
            if (_attached && TryGetBounds(out var bounds))
            {
                var position = PlacementCalculator.KeepOnScreen(bounds.Location, bounds.Size, _screens.GetScreens());
                if (position != bounds.Location)
                {
                    MoveTo(position);
                }
            }
        });

    private async Task SaveAsync(Action<AppSettings> change)
    {
        try
        {
            await _settings.UpdateAsync(change);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(_logger, ex);
        }
    }

    private bool TryGetBounds(out PixelRect bounds)
    {
        var handle = Handle;
        if (handle != IntPtr.Zero && NativeMethods.GetWindowRect(handle, out var rect))
        {
            bounds = new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            return true;
        }

        bounds = default;
        return false;
    }

    private void MoveTo(PixelPoint position)
    {
        var handle = Handle;
        if (handle != IntPtr.Zero)
        {
            NativeMethods.SetWindowPos(
                handle,
                IntPtr.Zero,
                position.X,
                position.Y,
                0,
                0,
                NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The character's position could not be saved.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception);
}
