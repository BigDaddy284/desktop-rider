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
    /// <summary>
    /// The "Ride around freely" line in the tray menu. Ticked, the rider rides about every screen by itself;
    /// unticked, it stays parked where it was put. Off until the user ticks it.
    /// </summary>
    public sealed class FreeRidingMenu : ITrayMenuContributor
    {
        public const string MenuText = "Ride around freely";

        private readonly ISettingsService _settings;
        private readonly ILogger<FreeRidingMenu> _logger;

        public FreeRidingMenu(ISettingsService settings, ILogger<FreeRidingMenu>? logger = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? NullLogger<FreeRidingMenu>.Instance;
        }

        public bool Enabled => _settings.Current.Behaviour.FreeRiding;

        public IReadOnlyList<TrayMenuEntry> BuildEntries() =>
            new[] { new TrayMenuEntry(MenuText, new RelayCommand(() => _ = SetAsync(!Enabled)), Enabled) };

        public async Task SetAsync(bool enabled)
        {
            try
            {
                await _settings.UpdateAsync(s => s.Behaviour.FreeRiding = enabled).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The change applies for this session (the settings object already holds it); it just is not remembered.
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.Log(LogLevel.Warning, "The free-riding choice could not be saved. It applies until Desktop Rider is closed.", ex);
                }
            }
        }
    }
}
