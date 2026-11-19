#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DesktopRider.Core.Logging
{
    /// <summary>How serious a log entry is. The names and order match the ones version 1 used.</summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5,
        None = 6,
    }

    /// <summary>Somewhere to write log entries. Kept deliberately small so the logic needs no logging package.</summary>
    public interface ILogger
    {
        /// <summary>Whether an entry of this level would be written at all.</summary>
        bool IsEnabled(LogLevel logLevel);

        /// <summary>Writes one entry.</summary>
        void Log(LogLevel logLevel, string message, Exception? exception = null);
    }

    /// <summary>A logger whose entries are labelled with the name of <typeparamref name="T"/>.</summary>
    public interface ILogger<out T> : ILogger
    {
    }

    /// <summary>Makes loggers that all write to the same place.</summary>
    public interface ILoggerProvider : IDisposable
    {
        ILogger CreateLogger(string categoryName);
    }

    /// <summary>A logger that writes nothing. Used when no logger is supplied.</summary>
    public sealed class NullLogger<T> : ILogger<T>
    {
        public static NullLogger<T> Instance { get; } = new();

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log(LogLevel logLevel, string message, Exception? exception = null)
        {
        }
    }

    /// <summary>Hands out loggers that write to every provider it was given.</summary>
    public sealed class LoggerFactory : IDisposable
    {
        private readonly IReadOnlyList<ILoggerProvider> _providers;

        public LoggerFactory(IEnumerable<ILoggerProvider> providers, LogLevel minimumLevel = LogLevel.Information)
        {
            Guard.NotNull(providers, nameof(providers));
            _providers = new List<ILoggerProvider>(providers);
            MinimumLevel = minimumLevel;
        }

        public LogLevel MinimumLevel { get; }

        /// <summary>A logger labelled with the full name of <typeparamref name="T"/>, as version 1's log files were.</summary>
        public ILogger<T> CreateLogger<T>()
        {
            var category = typeof(T).FullName ?? typeof(T).Name;
            var loggers = new List<ILogger>(_providers.Count);
            foreach (var provider in _providers)
            {
                loggers.Add(provider.CreateLogger(category));
            }

            return new Logger<T>(loggers, MinimumLevel);
        }

        public void Dispose()
        {
            foreach (var provider in _providers)
            {
                provider.Dispose();
            }
        }

        private sealed class Logger<T> : ILogger<T>
        {
            private readonly IReadOnlyList<ILogger> _loggers;
            private readonly LogLevel _minimumLevel;

            public Logger(IReadOnlyList<ILogger> loggers, LogLevel minimumLevel)
            {
                _loggers = loggers;
                _minimumLevel = minimumLevel;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                if (logLevel == LogLevel.None || logLevel < _minimumLevel)
                {
                    return false;
                }

                foreach (var logger in _loggers)
                {
                    if (logger.IsEnabled(logLevel))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Log(LogLevel logLevel, string message, Exception? exception = null)
            {
                if (logLevel == LogLevel.None || logLevel < _minimumLevel)
                {
                    return;
                }

                foreach (var logger in _loggers)
                {
                    if (logger.IsEnabled(logLevel))
                    {
                        logger.Log(logLevel, message, exception);
                    }
                }
            }
        }
    }

    /// <summary>Turns a value into the text that goes in a log entry.</summary>
    internal static class LogText
    {
        public static string Of(object? value) => value switch
        {
            null => "(null)",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
