using DesktopRider.Core.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopRider.Core.Configuration;

/// <summary>Stores settings as a readable JSON file in the user's local data folder.</summary>
public sealed partial class JsonSettingsStore : ISettingsStore, IDisposable
{
    private readonly LocalJsonFile<AppSettings> _file;
    private readonly ILogger<JsonSettingsStore> _logger;

    public JsonSettingsStore(AppPaths paths, ILogger<JsonSettingsStore>? logger = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _file = new LocalJsonFile<AppSettings>(paths.SettingsFile, timeProvider);
        _logger = logger ?? NullLogger<JsonSettingsStore>.Instance;
    }

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var read = await _file.ReadAsync(cancellationToken).ConfigureAwait(false);
        switch (read.Status)
        {
            case LocalFileStatus.Loaded:
                return new SettingsLoadResult(SettingsNormalizer.Normalize(read.Value), SettingsLoadStatus.Loaded);
            case LocalFileStatus.Missing:
                LogUsingDefaults(_logger);
                return new SettingsLoadResult(SettingsNormalizer.Normalize(null), SettingsLoadStatus.Defaults);
            default:
                LogDamagedFile(_logger, read.Problem, read.BackupFile);
                return new SettingsLoadResult(SettingsNormalizer.Normalize(null), SettingsLoadStatus.RecoveredFromDamagedFile, read.BackupFile);
        }
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        SettingsNormalizer.Normalize(settings);
        return _file.WriteAsync(settings, cancellationToken);
    }

    public void Dispose() => _file.Dispose();

    [LoggerMessage(Level = LogLevel.Information, Message = "No settings file found. Using default settings.")]
    private static partial void LogUsingDefaults(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The settings file could not be read and default settings are being used. Backup of the damaged file: {BackupFile}")]
    private static partial void LogDamagedFile(ILogger logger, Exception? exception, string? backupFile);
}
