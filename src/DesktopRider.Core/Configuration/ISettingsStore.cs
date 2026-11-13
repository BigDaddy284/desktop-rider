namespace DesktopRider.Core.Configuration;

public interface ISettingsStore
{
    /// <summary>Loads the saved settings. Never throws for a missing or damaged file; defaults are used instead.</summary>
    Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public enum SettingsLoadStatus
{
    /// <summary>The settings file was read successfully.</summary>
    Loaded,

    /// <summary>No settings file existed yet, so defaults are in use.</summary>
    Defaults,

    /// <summary>The file could not be read. It was set aside as a backup and defaults are in use.</summary>
    RecoveredFromDamagedFile,
}

public sealed record SettingsLoadResult(AppSettings Settings, SettingsLoadStatus Status, string? BackupFile = null);
