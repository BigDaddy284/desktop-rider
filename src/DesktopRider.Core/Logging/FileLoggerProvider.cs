using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace DesktopRider.Core.Logging;

/// <summary>
/// Writes log entries to one plain-text file per day in the local Logs folder and removes old files.
/// Logs never leave this computer.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public const string FilePrefix = "desktop-rider-";

    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly Lock _writeLock = new();
    private readonly string _directory;
    private readonly TimeProvider _timeProvider;
    private readonly int _retainedDays;
    private DateOnly _lastCleanup;
    private bool _disposed;

    public FileLoggerProvider(string directory, LogLevel minimumLevel = LogLevel.Information, int retainedDays = 14, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfLessThan(retainedDays, 1);
        _directory = directory;
        MinimumLevel = minimumLevel;
        _retainedDays = retainedDays;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public LogLevel MinimumLevel { get; }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, static (name, provider) => new FileLogger(provider, name), this);

    public void Dispose() => _disposed = true;

    internal static string FileNameFor(DateOnly day) =>
        FilePrefix + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log";

    internal void Write(string category, LogLevel level, string message, Exception? exception)
    {
        if (_disposed)
        {
            return;
        }

        var now = _timeProvider.GetLocalNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{now:yyyy-MM-dd HH:mm:ss.fff} [{LevelName(level)}] {category}: {message}{Environment.NewLine}");
        if (exception is not null)
        {
            line += exception + Environment.NewLine;
        }

        try
        {
            lock (_writeLock)
            {
                Directory.CreateDirectory(_directory);
                File.AppendAllText(Path.Combine(_directory, FileNameFor(today)), line);
                if (_lastCleanup != today)
                {
                    _lastCleanup = today;
                    RemoveOldFiles(today);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never bring the application down. A full disk or locked file is ignored.
        }
    }

    private void RemoveOldFiles(DateOnly today)
    {
        var oldestKept = today.AddDays(-(_retainedDays - 1));
        foreach (var file in Directory.EnumerateFiles(_directory, FilePrefix + "*.log"))
        {
            var stamp = Path.GetFileNameWithoutExtension(file)[FilePrefix.Length..];
            if (DateOnly.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                && day < oldestKept)
            {
                File.Delete(file);
            }
        }
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "FATAL",
        _ => "NONE",
    };

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= provider.MinimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (IsEnabled(logLevel))
            {
                provider.Write(category, logLevel, formatter(state, exception), exception);
            }
        }
    }
}
