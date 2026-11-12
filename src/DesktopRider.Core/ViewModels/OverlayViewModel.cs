using System.Windows.Input;
using DesktopRider.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopRider.Core.ViewModels;

/// <summary>What the character's window shows and the actions on its right-click menu.</summary>
public sealed partial class OverlayViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly ILogger<OverlayViewModel> _logger;

    public OverlayViewModel(ISettingsService settings, ILogger<OverlayViewModel>? logger = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? NullLogger<OverlayViewModel>.Instance;
        _settings.Changed += OnSettingsChanged;

        ToggleAlwaysOnTopCommand = new RelayCommand(() => _ = SetAlwaysOnTopAsync(!AlwaysOnTop));
        ShowCommand = new RelayCommand(() => _ = SetCharacterVisibleAsync(true));
        HideCommand = new RelayCommand(() => _ = SetCharacterVisibleAsync(false));
        ToggleVisibilityCommand = new RelayCommand(() => _ = SetCharacterVisibleAsync(!CharacterVisible));
        ResetPositionCommand = new RelayCommand(() => ResetPositionRequested?.Invoke(this, EventArgs.Empty));
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Raised when the user asks for the character to go back to its standard spot.</summary>
    public event EventHandler? ResetPositionRequested;

    /// <summary>Raised when the user chooses to close Desktop Rider.</summary>
    public event EventHandler? ExitRequested;

    public string Title { get; } = AppInfo.Name;

    public bool AlwaysOnTop => _settings.Current.Appearance.AlwaysOnTop;

    /// <summary>False while the user has the rider hidden. Desktop Rider keeps running in the notification area.</summary>
    public bool CharacterVisible => _settings.Current.Appearance.CharacterVisible;

    public double CharacterOpacity => _settings.Current.Appearance.Opacity;

    public ICommand ToggleAlwaysOnTopCommand { get; }

    public ICommand ShowCommand { get; }

    public ICommand HideCommand { get; }

    public ICommand ToggleVisibilityCommand { get; }

    public ICommand ResetPositionCommand { get; }

    public ICommand ExitCommand { get; }

    public Task SetAlwaysOnTopAsync(bool value) => ChangeAsync(s => s.Appearance.AlwaysOnTop = value);

    /// <summary>Shows or hides the rider and remembers the choice for next time.</summary>
    public Task SetCharacterVisibleAsync(bool value) =>
        value == CharacterVisible ? Task.CompletedTask : ChangeAsync(s => s.Appearance.CharacterVisible = value);

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

    private async Task ChangeAsync(Action<AppSettings> change)
    {
        try
        {
            await _settings.UpdateAsync(change).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The change still applies for this session; it just could not be remembered for next time.
            LogSaveFailed(_logger, ex);
            OnAllPropertiesChanged();
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => OnAllPropertiesChanged();

    [LoggerMessage(Level = LogLevel.Warning, Message = "A settings change could not be saved. It applies until Desktop Rider is closed.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception);
}
