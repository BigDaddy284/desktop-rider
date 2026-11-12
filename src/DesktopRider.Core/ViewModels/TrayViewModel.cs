using System.ComponentModel;
using System.Windows.Input;
using DesktopRider.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopRider.Core.ViewModels;

/// <summary>One line of the notification-area menu. A line with no command is a divider.</summary>
public sealed record TrayMenuEntry(string Text, ICommand? Command, bool IsChecked = false)
{
    public static TrayMenuEntry Separator { get; } = new(string.Empty, null);

    public bool IsSeparator => Command is null;
}

/// <summary>A short message shown beside the notification-area icon.</summary>
public sealed class TrayHintEventArgs(string title, string message) : EventArgs
{
    public string Title { get; } = title;

    public string Message { get; } = message;
}

/// <summary>
/// What the icon in the notification area (beside the clock) says and does: its menu, what a click does,
/// and the reminder shown when the rider is hidden. It knows nothing about Windows, so it is unit tested.
/// </summary>
public sealed partial class TrayViewModel : IDisposable
{
    public const string ShowText = "Show the rider";
    public const string HideText = "Hide the rider";
    public const string AlwaysOnTopText = "Always on top";
    public const string ResetPositionText = "Move back to the corner";
    public const string ExitText = "Exit Desktop Rider";
    public const string HintTitle = "The rider is hidden";
    public const string HintMessage = "Desktop Rider is still running. Click this icon to bring the rider back.";

    private readonly OverlayViewModel _overlay;
    private readonly ISettingsService _settings;
    private readonly ILogger<TrayViewModel> _logger;
    private bool _wasVisible;

    public TrayViewModel(OverlayViewModel overlay, ISettingsService settings, ILogger<TrayViewModel>? logger = null)
    {
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? NullLogger<TrayViewModel>.Instance;
        _wasVisible = overlay.CharacterVisible;
        _overlay.PropertyChanged += OnOverlayChanged;
    }

    /// <summary>Raised when the user should be reminded where the rider has gone.</summary>
    public event EventHandler<TrayHintEventArgs>? HintRequested;

    /// <summary>The words shown when the mouse rests on the icon.</summary>
    public string ToolTip => _overlay.CharacterVisible ? AppInfo.Name : $"{AppInfo.Name} (hidden)";

    /// <summary>What a click or double-click on the icon does: hides the rider, or brings it back.</summary>
    public ICommand DefaultCommand => _overlay.ToggleVisibilityCommand;

    /// <summary>What clicking the reminder does: brings the rider back.</summary>
    public ICommand HintClickedCommand => _overlay.ShowCommand;

    /// <summary>The menu as it should look right now.</summary>
    public IReadOnlyList<TrayMenuEntry> BuildMenu()
    {
        var visible = _overlay.CharacterVisible;
        var menu = new List<TrayMenuEntry>
        {
            visible ? new TrayMenuEntry(HideText, _overlay.HideCommand) : new TrayMenuEntry(ShowText, _overlay.ShowCommand),
            new(AlwaysOnTopText, _overlay.ToggleAlwaysOnTopCommand, _overlay.AlwaysOnTop),
        };
        if (visible)
        {
            menu.Add(new TrayMenuEntry(ResetPositionText, _overlay.ResetPositionCommand));
        }

        menu.Add(TrayMenuEntry.Separator);
        menu.Add(new TrayMenuEntry(ExitText, _overlay.ExitCommand));
        return menu;
    }

    /// <summary>
    /// Call once at start-up. If the rider was left hidden last time, nothing appears on the desktop,
    /// so the reminder is shown every time to make it clear that Desktop Rider did start.
    /// </summary>
    public void AnnounceIfStartedHidden()
    {
        if (!_overlay.CharacterVisible)
        {
            HintRequested?.Invoke(this, new TrayHintEventArgs(HintTitle, HintMessage));
        }
    }

    public void Dispose() => _overlay.PropertyChanged -= OnOverlayChanged;

    private void OnOverlayChanged(object? sender, PropertyChangedEventArgs e)
    {
        var visible = _overlay.CharacterVisible;
        var justHidden = _wasVisible && !visible;
        _wasVisible = visible;
        if (justHidden && !_settings.Current.Application.TrayHintShown)
        {
            // Marked before raising the event so the save's own change notice cannot trigger a second reminder.
            _settings.Current.Application.TrayHintShown = true;
            HintRequested?.Invoke(this, new TrayHintEventArgs(HintTitle, HintMessage));
            _ = RememberHintShownAsync();
        }
    }

    private async Task RememberHintShownAsync()
    {
        try
        {
            await _settings.UpdateAsync(s => s.Application.TrayHintShown = true).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(_logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not record that the hidden-rider reminder was shown. It may appear once more.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception);
}
