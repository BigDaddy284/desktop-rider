#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Logging;
using DesktopRider.Core.ViewModels;

namespace DesktopRider.Core.Desktop
{
    /// <summary>The per-user list of programs Windows starts at sign-in (the Run key). Only Desktop Rider's own line is touched.</summary>
    public interface IStartupRegistry
    {
        /// <summary>The command stored under this name, or null if there is none.</summary>
        string? Read(string name);

        void Write(string name, string command);

        void Remove(string name);
    }

    /// <summary>
    /// The "Start with Windows" line in the tray menu, and keeping the sign-in list in step with the setting.
    /// Off by default; nothing is written until the user ticks it.
    /// </summary>
    public sealed class StartWithWindows : ITrayMenuContributor
    {
        public const string MenuText = "Start with Windows";

        /// <summary>The name of Desktop Rider's line in the sign-in list.</summary>
        public const string EntryName = "Desktop Rider 3D";

        private readonly ISettingsService _settings;
        private readonly IStartupRegistry _registry;
        private readonly string _command;
        private readonly ILogger<StartWithWindows> _logger;

        public StartWithWindows(ISettingsService settings, IStartupRegistry registry, string executablePath, ILogger<StartWithWindows>? logger = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException("The program's path is needed.", nameof(executablePath));
            }

            _command = CommandFor(executablePath);
            _logger = logger ?? NullLogger<StartWithWindows>.Instance;
        }

        /// <summary>The program's path in quotes, so a path with spaces in it still starts.</summary>
        public static string CommandFor(string executablePath) => "\"" + executablePath.Trim().Trim('"') + "\"";

        public bool Enabled => _settings.Current.Application.StartWithWindows;

        public IReadOnlyList<TrayMenuEntry> BuildEntries() =>
            new[] { new TrayMenuEntry(MenuText, new RelayCommand(() => _ = SetAsync(!Enabled)), Enabled) };

        /// <summary>
        /// Call once at start-up. Makes the sign-in list match the setting, which also fixes the stored path if
        /// Desktop Rider has been moved since it was ticked.
        /// </summary>
        public void Apply() => Apply(Enabled);

        public async Task SetAsync(bool enabled)
        {
            if (!Apply(enabled))
            {
                return;
            }

            try
            {
                await _settings.UpdateAsync(s => s.Application.StartWithWindows = enabled).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log(LogLevel.Warning, "The start-with-Windows choice could not be saved. It still applies until changed.", ex);
            }
        }

        /// <summary>Returns false if Windows refused the change, in which case the setting is left as it was.</summary>
        private bool Apply(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    if (!string.Equals(_registry.Read(EntryName), _command, StringComparison.OrdinalIgnoreCase))
                    {
                        _registry.Write(EntryName, _command);
                    }
                }
                else if (_registry.Read(EntryName) is not null)
                {
                    _registry.Remove(EntryName);
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Log(LogLevel.Warning, "Windows did not allow the start-with-Windows change.", ex);
                return false;
            }
        }

        private void Log(LogLevel level, string message, Exception exception)
        {
            if (_logger.IsEnabled(level))
            {
                _logger.Log(level, message, exception);
            }
        }
    }
}
