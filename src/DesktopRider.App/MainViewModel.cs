using System.ComponentModel;
using System.Runtime.CompilerServices;
using DesktopRider.Core;
using DesktopRider.Core.Configuration;

namespace DesktopRider.App;

/// <summary>What the foundation window shows. It is replaced by the rider overlay in the next phase.</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _statusMessage = "Getting ready…";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; } = AppInfo.Name;

    public string VersionText { get; } = $"Version {AppInfo.Version}";

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public void ApplySettingsStatus(SettingsLoadStatus status) =>
        StatusMessage = status switch
        {
            SettingsLoadStatus.Loaded => "Your settings were loaded.",
            SettingsLoadStatus.Defaults => "Welcome! Standard settings are in use.",
            SettingsLoadStatus.RecoveredFromDamagedFile =>
                "Your saved settings could not be read, so standard settings are in use. A backup of the old file was kept.",
            _ => string.Empty,
        };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
