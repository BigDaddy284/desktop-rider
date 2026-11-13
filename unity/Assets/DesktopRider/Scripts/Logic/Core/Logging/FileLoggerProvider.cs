#nullable enable

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;

namespace DesktopRider.Core.Logging
{
    /// <summary>
    /// Writes log entries to one plain-text file per day in the local Logs folder and removes old files.
    /// Logs never leave this computer.
    /// </summary>
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        public const string FilePrefix = "desktop-rider-";

        private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);
        private readonly object _writeLock = new();
        private readonly string _directory;
        private readonly TimeProvider _timeProvider;
        private readonly int _retainedDays;
        private DateTime _lastCleanup;
        private bool _disposed;

        public FileLoggerProvider(string directory, LogLevel minimumLevel = LogLevel.Information, int retainedDays = 14, TimeProvider? timeProvider = null)
        {
            Guard.NotBlank(directory, nameof(directory));
            if (retainedDays < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(retainedDays), retainedDays, "At least one day of logs must be kept.");
            }

            _directory = directory;
            MinimumLevel = minimumLevel;
            _retainedDays = retainedDays;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public LogLevel MinimumLevel { get; }

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

        public void Dispose() => _disposed = true;

        internal static string FileNameFor(DateTime day) =>
            FilePrefix + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log";

        internal void Write(string category, LogLevel level, string message, Exception? exception)
        {
            if (_disposed)
            {
                return;
            }

            var now = _timeProvider.GetLocalNow();
            var today = now.Date;
            var line = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}: {3}{4}",
                now,
                LevelName(level),
                category,
                message,
                Environment.NewLine);
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

        private void RemoveOldFiles(DateTime today)
        {
            var oldestKept = today.AddDays(-(_retainedDays - 1));
            foreach (var file in Directory.EnumerateFiles(_directory, FilePrefix + "*.log"))
            {
                var stamp = Path.GetFileNameWithoutExtension(file)[FilePrefix.Length..];
                if (DateTime.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
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

        private sealed class FileLogger : ILogger
        {
            private readonly FileLoggerProvider _provider;
            private readonly string _category;

            public FileLogger(FileLoggerProvider provider, string category)
            {
                _provider = provider;
                _category = category;
            }

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= _provider.MinimumLevel;

            public void Log(LogLevel logLevel, string message, Exception? exception = null)
            {
                Guard.NotNull(message, nameof(message));
                if (IsEnabled(logLevel))
                {
                    _provider.Write(_category, logLevel, message, exception);
                }
            }
        }
    }
}
