using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using DesktopRider.Core;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Desktop;
using DesktopRider.Core.Infrastructure;
using DesktopRider.Core.Logging;
using DesktopRider.Core.ViewModels;
using DesktopRider.Riding;
using UnityEngine;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// Puts Desktop Rider together when it starts: loads the settings, adds the tray icon and its menu, and applies the
    /// settings to the rider and its window (shown or hidden, always on top, size, free riding or parked, last position).
    /// Settings changes made from the menu are applied as they happen. Nothing here runs in the background.
    /// </summary>
    public sealed class AppHost : MonoBehaviour
    {
        /// <summary>Version 2 keeps its own settings, so it can run beside version 1 without either changing the other's.</summary>
        public const string DataFolderName = "DesktopRider3D";

        public FreeRider rider;
        public Camera viewCamera;
        public OverlayWindow overlayWindow;
        [Tooltip("Frames a second while the rider is hidden; nothing is drawn, this only keeps the menu answering.")]
        public int hiddenFrameRate = 4;

        CoreServices services;
        ISettingsService settings;
        OverlayViewModel overlay;
        TrayViewModel trayMenu;
        TrayIcon trayIcon;
        ILogger<AppHost> log;
        bool ready;

        async void Start()
        {
            try
            {
                var paths = new AppPaths(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
                    DataFolderName));
                paths.EnsureDirectoriesExist();
                services = new CoreServices(paths);
                log = services.Loggers.CreateLogger<AppHost>();
                settings = services.Settings;
                // Reading the file happens off the main thread; the rest of Start carries on on the main thread afterwards.
                await Task.Run(() => settings.InitializeAsync());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning("Desktop Rider could not read its settings and is using the defaults: " + ex.Message);
            }

            if (this == null) return;     // closed while the settings were loading
            if (settings == null) return; // could not even make the settings folder; the rider still rides with its defaults
            BuildMenu();
            RestorePosition();
            ApplySettings();
            if (rider != null) rider.Dropped += SavePositionSoon;
            ready = true;
        }

        void BuildMenu()
        {
            overlay = new OverlayViewModel(settings, Logger<OverlayViewModel>());
            overlay.PropertyChanged += OnOverlayChanged;
            overlay.ExitRequested += (s, e) => MainThread.Post(Application.Quit);
            overlay.ResetPositionRequested += (s, e) => MainThread.Post(MoveToMiddle);
            settings.Changed += OnSettingsChanged;

            string exe;
            using (var process = System.Diagnostics.Process.GetCurrentProcess()) exe = process.MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exe)) exe = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, "DesktopRider3D.exe");
            var startup = new StartWithWindows(settings, new WindowsStartupRegistry(), exe, Logger<StartWithWindows>());
            var freeRiding = new FreeRidingMenu(settings, Logger<FreeRidingMenu>());
            trayMenu = new TrayViewModel(overlay, settings, new ITrayMenuContributor[] { freeRiding, startup }, Logger<TrayViewModel>());

            // The tray icon and the sign-in list are only for the real program, not while working in the editor.
            if (Application.isEditor) return;
            startup.Apply();
            trayIcon = new TrayIcon(trayMenu, rider != null ? RiderIcon.Create(rider.leanPivot != null ? rider.leanPivot : rider.transform) : IntPtr.Zero);
            trayIcon.DisplaysChanged += OverlayWindow.RefreshScreens;
            trayIcon.CommandFailed += ex => Log(LogLevel.Error, "A tray menu action failed.", ex);
            if (!trayIcon.Show()) Log(LogLevel.Warning, "The tray icon could not be shown. Ctrl+Shift+Q still closes Desktop Rider.", null);
            trayMenu.AnnounceIfStartedHidden();
        }

        void OnOverlayChanged(object sender, PropertyChangedEventArgs e) => MainThread.Post(ApplySettings);

        void OnSettingsChanged(object sender, EventArgs e) => MainThread.Post(ApplySettings);

        /// <summary>Makes the rider and its window match the settings. Safe to call any number of times.</summary>
        void ApplySettings()
        {
            if (settings == null || this == null) return;
            var appearance = settings.Current.Appearance;
            bool visible = appearance.CharacterVisible;
            OverlayWindow.SetVisible(visible);
            OverlayWindow.SetAlwaysOnTop(appearance.AlwaysOnTop);
            if (viewCamera != null) viewCamera.enabled = visible;
            if (rider != null)
            {
                rider.enabled = visible;
                rider.Roaming = settings.Current.Behaviour.FreeRiding;
                rider.SetSizeScale((float)appearance.Scale);
            }
            if (overlayWindow != null) OverlayWindow.Resize(overlayWindow.SizeFor((float)appearance.Scale));
            // The rider sets its own frame rate while shown; hidden, nothing is drawn, so tick over slowly.
            if (!visible) Application.targetFrameRate = hiddenFrameRate;
        }

        void RestorePosition()
        {
            if (rider == null) return;
            var saved = settings.Current.Appearance.Position;
            if (saved == null) { MoveToMiddle(); return; }
            var offset = OverlayWindow.WindowsOffset;
            rider.MoveTo(new Vector2((float)saved.X + offset.X, (float)saved.Y + offset.Y));
        }

        void MoveToMiddle()
        {
            if (rider == null) return;
            var centre = OverlayWindow.Area.MainCentre;
            rider.MoveTo(new Vector2(centre.X, centre.Y));
        }

        void SavePositionSoon() => _ = SavePositionAsync();

        /// <summary>Remembers where the rider is, as a Windows position, so it is found there next time.</summary>
        Task SavePositionAsync()
        {
            if (rider == null || settings == null) return Task.CompletedTask;
            var at = rider.DesktopPosition;
            var offset = OverlayWindow.WindowsOffset;
            var position = new SavedPosition { X = at.x - offset.X, Y = at.y - offset.Y };
            return SaveAsync(s => s.Appearance.Position = position);
        }

        async Task SaveAsync(Action<AppSettings> change)
        {
            try { await settings.UpdateAsync(change).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log(LogLevel.Warning, "The rider's position could not be saved.", ex);
            }
        }

        void OnApplicationQuit()
        {
            if (ready)
            {
                // Waiting is safe: the save continues on a pool thread, not this one. A slow disk is given at most a second.
                try { SavePositionAsync().Wait(TimeSpan.FromSeconds(1)); }
                catch (AggregateException ex) { Log(LogLevel.Warning, "The rider's position could not be saved on exit.", ex); }
            }
            trayIcon?.Dispose();
            trayIcon = null;
        }

        void OnDestroy()
        {
            trayIcon?.Dispose();
            trayIcon = null;
            if (overlay != null) { overlay.PropertyChanged -= OnOverlayChanged; overlay.Dispose(); }
            trayMenu?.Dispose();
            if (settings != null) settings.Changed -= OnSettingsChanged;
            if (rider != null) rider.Dropped -= SavePositionSoon;
            services?.Dispose();
        }

        ILogger<T> Logger<T>() => services != null ? services.Loggers.CreateLogger<T>() : NullLogger<T>.Instance;

        void Log(LogLevel level, string message, Exception exception)
        {
            if (log != null && log.IsEnabled(level)) log.Log(level, message, exception);
            if (level >= LogLevel.Warning) Debug.LogWarning(message + (exception != null ? " " + exception.Message : string.Empty));
        }
    }
}
