using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopRider.Core.Infrastructure;

/// <summary>What was found when a local file was read.</summary>
public enum LocalFileStatus
{
    /// <summary>The file was read successfully.</summary>
    Loaded,

    /// <summary>There was no file yet.</summary>
    Missing,

    /// <summary>The file could not be understood. It was set aside as a backup where possible.</summary>
    Damaged,
}

/// <summary>The result of reading a local file. <see cref="Value"/> is null unless the file was loaded.</summary>
public sealed record LocalFileRead<T>(T? Value, LocalFileStatus Status, string? BackupFile = null, Exception? Problem = null)
    where T : class;

/// <summary>
/// One readable JSON file on this computer. Reading never throws for a missing or damaged file, and writing
/// goes through a temporary file so a crash or power cut can never leave a half-written one.
/// </summary>
public sealed class LocalJsonFile<T>(string file, TimeProvider? timeProvider = null) : IDisposable
    where T : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _file = Path.GetFullPath(file ?? throw new ArgumentNullException(nameof(file)));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<LocalFileRead<T>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_file))
            {
                return new LocalFileRead<T>(null, LocalFileStatus.Missing);
            }

            try
            {
                T? loaded;
                var stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                await using (stream.ConfigureAwait(false))
                {
                    loaded = await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
                }

                if (loaded is null)
                {
                    throw new JsonException("The file was empty.");
                }

                return new LocalFileRead<T>(loaded, LocalFileStatus.Loaded);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return new LocalFileRead<T>(null, LocalFileStatus.Damaged, TrySetAside(), ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteAsync(T value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            var temporaryFile = _file + ".tmp";
            try
            {
                var stream = new FileStream(temporaryFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await using (stream.ConfigureAwait(false))
                {
                    await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken).ConfigureAwait(false);
                }

                File.Move(temporaryFile, _file, overwrite: true);
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

    /// <summary>Removes the file. Does nothing if there is none.</summary>
    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            File.Delete(_file);
        }
        catch (DirectoryNotFoundException)
        {
            // Nothing was ever saved, so there is nothing to remove.
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private string? TrySetAside()
    {
        try
        {
            var stamp = _timeProvider.GetLocalNow().ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var backup = Path.Combine(
                Path.GetDirectoryName(_file)!,
                $"{Path.GetFileNameWithoutExtension(_file)}.damaged-{stamp}{Path.GetExtension(_file)}");
            File.Move(_file, backup, overwrite: true);
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
}
