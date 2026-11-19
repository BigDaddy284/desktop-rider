#nullable enable

using System;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using DesktopRider.Core.Logging;

namespace DesktopRider.Core
{
    /// <summary>
    /// The parts every other part of Desktop Rider relies on: file locations, the clock, logging and settings.
    /// Each is made once, the first time it is asked for.
    /// </summary>
    public sealed class CoreServices : ServiceSet
    {
        private readonly LogLevel _minimumLogLevel;
        private LoggerFactory? _loggers;
        private ISettingsStore? _settingsStore;
        private ISettingsService? _settings;

        public CoreServices(AppPaths paths, LogLevel minimumLogLevel = LogLevel.Information, TimeProvider? timeProvider = null)
        {
            Paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _minimumLogLevel = minimumLogLevel;
            Time = timeProvider ?? TimeProvider.System;
        }

        public AppPaths Paths { get; }

        public TimeProvider Time { get; }

        /// <summary>Makes loggers that write to the local Logs folder.</summary>
        public LoggerFactory Loggers => Once(ref _loggers, () => new LoggerFactory(
            new ILoggerProvider[] { new FileLoggerProvider(Paths.LogDirectory, _minimumLogLevel, timeProvider: Time) },
            _minimumLogLevel));

        public ISettingsStore SettingsStore => Once(ref _settingsStore, () =>
            new JsonSettingsStore(Paths, Loggers.CreateLogger<JsonSettingsStore>(), Time));

        public ISettingsService Settings => Once(ref _settings, () => new SettingsService(SettingsStore));
    }
}
