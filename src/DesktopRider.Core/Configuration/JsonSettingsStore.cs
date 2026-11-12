using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopRider.Core.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopRider.Core.Configuration;

/// <summary>Stores settings as a readable JSON file in the user's local data folder.</summary>
public sealed partial class JsonSettingsStore : ISettingsStore, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly AppPaths _paths;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSettingsStore(AppPaths paths, ILogger<JsonSettingsStore>? logger = null, TimeProvider? timeProvider = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _logger = logger ?? NullLogger<JsonSettingsStore>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var file = _paths.SettingsFile;
            if (!File.Exists(file))
            {
                LogUsingDefaults(_logger);
                return new SettingsLoadResult(SettingsNormalizer.Normalize(null), SettingsLoadStatus.Defaults);
            }

            try
            {
                AppSettings? loaded;
                var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                await using (stream.ConfigureAwait(false))
                {
                    loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
                }

                if (loaded is null)
                {
                    throw new JsonException("The settings file was empty.");
                }

                return new SettingsLoadResult(SettingsNormalizer.Normalize(loaded), SettingsLoadStatus.Loaded);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                var backup = TrySetAsideDamagedFile(file);
                LogDamagedFile(_logger, ex, backup);
                return new SettingsLoadResult(SettingsNormalizer.Normalize(null), SettingsLoadStatus.RecoveredFromDamagedFile, backup);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        SettingsNormalizer.Normalize(settings);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.DataDirectory);

            // Write to a temporary file first so a crash or power cut can never leave a half-written settings file.
            var temporaryFile = _paths.SettingsFile + ".tmp";
            try
            {
                var stream = new FileStream(temporaryFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await using (stream.ConfigureAwait(false))
                {
                    await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken).ConfigureAwait(false);
                }

                File.Move(temporaryFile, _paths.SettingsFile, overwrite: true);
            }
            catch
            {
                TryDelete(temporaryFile);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private string? TrySetAsideDamagedFile(string file)
    {
        try
        {
            var stamp = _timeProvider.GetLocalNow().ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var backup = Path.Combine(_paths.DataDirectory, $"settings.damaged-{stamp}.json");
            File.Move(file, backup, overwrite: true);
            return backup;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing useful can be done; the leftover file is overwritten by the next save.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "No settings file found. Using default settings.")]
    private static partial void LogUsingDefaults(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The settings file could not be read and default settings are being used. Backup of the damaged file: {BackupFile}")]
    private static partial void LogDamagedFile(ILogger logger, Exception exception, string? backupFile);
}
