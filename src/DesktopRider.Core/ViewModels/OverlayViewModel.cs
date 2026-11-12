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
        ResetPositionCommand = new RelayCommand(() => ResetPositionRequested?.Invoke(this, EventArgs.Empty));
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Raised when the user asks for the character to go back to its standard spot.</summary>
    public event EventHandler? ResetPositionRequested;

    /// <summary>Raised when the user chooses to close Desktop Rider.</summary>
    public event EventHandler? ExitRequested;

    public string Title { get; } = AppInfo.Name;

    public bool AlwaysOnTop => _settings.Current.Appearance.AlwaysOnTop;

    public double CharacterOpacity => _settings.Current.Appearance.Opacity;

    public ICommand ToggleAlwaysOnTopCommand { get; }

    public ICommand ResetPositionCommand { get; }

    public ICommand ExitCommand { get; }

    public async Task SetAlwaysOnTopAsync(bool value)
    {
        try
        {
            await _settings.UpdateAsync(s => s.Appearance.AlwaysOnTop = value).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The change still applies for this session; it just could not be remembered for next time.
            LogSaveFailed(_logger, ex);
            OnAllPropertiesChanged();
        }
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

    private void OnSettingsChanged(object? sender, EventArgs e) => OnAllPropertiesChanged();

    [LoggerMessage(Level = LogLevel.Warning, Message = "A settings change could not be saved. It applies until Desktop Rider is closed.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception);
}
