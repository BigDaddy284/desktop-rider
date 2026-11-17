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
using DesktopRider.Tricks;
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
        [Tooltip("Rides tricks; adds \"Do a trick\" to the tray menu. Optional.")]
        public TrickRunner tricks;
        [Tooltip("Harvey and the sidecar crew; the \"Who rides\" setting picks one. Optional (Harvey only without it).")]
        public RiderCharacters characters;
        [Tooltip("Frames a second while the rider is hidden; nothing is drawn, this only keeps the menu answering.")]
        public int hiddenFrameRate = 4;

        CoreServices services;
        ISettingsService settings;
        OverlayViewModel overlay;
        TrayViewModel trayMenu;
        TrayIcon trayIcon;
        SettingsPanel settingsPanel;
        SettingsWindow settingsWindow;
        readonly OwnTrickTimer ownTricks = new OwnTrickTimer();
        float nextOwnTrickCheck;
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
            if (characters != null) characters.Use(settings.Current.Appearance.Character);   // before the tray icon, which pictures him
            BuildMenu();
            RestorePosition();
            ApplySettings();
            if (rider != null) rider.Dropped += SavePositionSoon;
            if (rider != null) rider.RightClicked += OpenSettings;
            ready = true;
            // "DesktopRider3D.exe --open-settings" opens the settings window straight away (handy for a shortcut, and for checks).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--open-settings") >= 0) { Debug.Log("Opening the settings window (--open-settings)."); OpenSettings(); }
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
            var trickMenu = new TrickMenu(() => tricks != null && tricks.HasTricks);
            trickMenu.Requested += (s, e) => MainThread.Post(DoATrick);
            settingsPanel = new SettingsPanel(settings, startup, Logger<SettingsPanel>());
            settingsPanel.Requested += (s, e) => MainThread.Post(OpenSettings);
            trayMenu = new TrayViewModel(overlay, settings, new ITrayMenuContributor[] { trickMenu, freeRiding, startup, settingsPanel }, Logger<TrayViewModel>());

            // The tray icon and the sign-in list are only for the real program, not while working in the editor.
            if (Application.isEditor) return;
            startup.Apply();
            trayIcon = new TrayIcon(trayMenu, rider != null ? RiderIcon.Create(rider.leanPivot != null ? rider.leanPivot : rider.transform) : IntPtr.Zero);
            trayIcon.DisplaysChanged += OverlayWindow.RefreshScreens;
            trayIcon.CommandFailed += ex => Log(LogLevel.Error, "A tray menu action failed.", ex);
            if (!trayIcon.Show()) Log(LogLevel.Warning, "The tray icon could not be shown. Ctrl+Shift+Q still closes Desktop Rider.", null);
            trayMenu.AnnounceIfStartedHidden();
        }

        void DoATrick()
        {
            if (tricks == null || tricks.IsRunning) return;
            if (!settings.Current.Appearance.CharacterVisible) return;
            if (!tricks.TryPerform())
                trayIcon?.ShowHint("Desktop Rider", "There isn't enough clear space on the screen for a trick right now.");
            else
                ownTricks.Restart(Time.realtimeSinceStartup);   // one asked for counts: the next one by himself waits a full gap
        }

        /// <summary>
        /// Tricks by himself ("Tricks by himself": never, sometimes or often). Looked at once a second, which costs
        /// nothing; a trick that is due while he is hidden, held, busy or short of room is tried again a little later.
        /// </summary>
        void Update()
        {
            if (!ready || tricks == null || rider == null) return;
            float now = Time.realtimeSinceStartup;
            if (now < nextOwnTrickCheck) return;
            nextOwnTrickCheck = now + 1f;
            bool canStart = settings.Current.Appearance.CharacterVisible && rider.isActiveAndEnabled && tricks.HasTricks
                            && !tricks.IsRunning && !rider.InTrick && !rider.Approaching && !rider.BeingDragged;
            if (ownTricks.Due(now, settings.Current.Behaviour.TricksByHimself, canStart) && !tricks.TryPerform())
                ownTricks.Retry(now);   // no room just now: try again shortly
        }

        void OnOverlayChanged(object sender, PropertyChangedEventArgs e) => MainThread.Post(ApplySettings);

        void OnSettingsChanged(object sender, EventArgs e) => MainThread.Post(() => { ApplySettings(); settingsWindow?.Refresh(); });

        /// <summary>Opens the settings window, or brings it to the front if it is already open.</summary>
        void OpenSettings()
        {
            if (settingsPanel == null) return;
            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow(settingsPanel, trayIcon != null ? trayIcon.Image : IntPtr.Zero);
                settingsWindow.ResetRequested += MoveToMiddle;
                settingsWindow.CommandFailed += ex => Log(LogLevel.Error, "A change in the settings window failed.", ex);
            }
            settingsWindow.Open();
            Debug.Log(settingsWindow.IsOpen ? "The settings window is open." : "The settings window did not open.");
        }

        /// <summary>Makes the rider and its window match the settings. Safe to call any number of times.</summary>
        void ApplySettings()
        {
            if (settings == null || this == null) return;
            var appearance = settings.Current.Appearance;
            bool visible = appearance.CharacterVisible;
            OverlayWindow.SetVisible(visible);
            OverlayWindow.SetAlwaysOnTop(appearance.AlwaysOnTop);
            if (viewCamera != null) viewCamera.enabled = visible;
            if (characters != null) characters.Use(appearance.Character);
            if (rider != null)
            {
                rider.enabled = visible;
                rider.RidingSpeed = (float)settings.Current.Behaviour.RidingSpeed;
                rider.Roaming = settings.Current.Behaviour.FreeRiding;
                rider.SetSizeScale((float)appearance.Scale);
                rider.SetAnimationSpeed((float)appearance.AnimationSpeed);
                rider.Director.IdleAnimations = settings.Current.Behaviour.IdleAnimations;
            }
            // Only when the size setting changed: any other change (or saving his place) left the window as it was. Resizing
            // every time shrank a window grown for a trick, which flickers (see OverlayWindow.Grow).
            int own = overlayWindow != null ? overlayWindow.SizeFor((float)appearance.Scale) : 0;
            if (overlayWindow != null && OverlayWindow.BaseSize != new Vector2Int(own, own)) OverlayWindow.Resize(own);
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
            settingsWindow?.Dispose();
            settingsWindow = null;
            trayIcon?.Dispose();
            trayIcon = null;
        }

        void OnDestroy()
        {
            settingsWindow?.Dispose();
            settingsWindow = null;
            trayIcon?.Dispose();
            trayIcon = null;
            if (overlay != null) { overlay.PropertyChanged -= OnOverlayChanged; overlay.Dispose(); }
            trayMenu?.Dispose();
            if (settings != null) settings.Changed -= OnSettingsChanged;
            if (rider != null) { rider.Dropped -= SavePositionSoon; rider.RightClicked -= OpenSettings; }
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
