using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
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

        // Several riders at once (see RiderLineUp). The main program starts a companion window for each extra rider out;
        // a companion has no tray icon or settings of its own and follows the main program's settings file.
        CompanionRiders companions;
        bool isCompanion;
        RiderCharacter companionRider;
        int hostProcessId;
        FileSystemWatcher settingsWatcher;
        bool reloadQueued;
        SingleInstanceGuard instanceGuard;

        // Right-clicking an extra rider opens the settings in the main program, and the main program's "Do a trick" can be
        // passed to an extra rider (see RiderSignal).
        RiderSignal listening;
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int processId);

        async void Start()
        {
            isCompanion = RiderLineUp.TryReadCompanion(Environment.GetCommandLineArgs(), out companionRider, out hostProcessId);
            // One main program, and at most one companion window for each rider. (Replaces Unity's own "one copy only"
            // setting, which would also stop the companions.)
            if (!Application.isEditor)
            {
                instanceGuard = new SingleInstanceGuard(isCompanion ? SingleInstanceGuard.DefaultName + "." + companionRider : SingleInstanceGuard.DefaultName);
                if (!instanceGuard.IsFirstInstance) { Debug.Log("Desktop Rider is already running; this copy closes."); Application.Quit(); return; }
            }
            AppPaths paths = null;
            try
            {
                paths = new AppPaths(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
                    DataFolderName));
                paths.EnsureDirectoriesExist();
                services = new CoreServices(paths);
                log = services.Loggers.CreateLogger<AppHost>();
                settings = isCompanion ? new SettingsService(new ReadOnlySettingsStore(paths.SettingsFile)) : services.Settings;
                // Reading the file happens off the main thread; the rest of Start carries on on the main thread afterwards.
                await Task.Run(() => settings.InitializeAsync());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning("Desktop Rider could not read its settings and is using the defaults: " + ex.Message);
            }

            if (this == null) return;     // closed while the settings were loading
            if (settings == null) return; // could not even make the settings folder; the rider still rides with its defaults
            if (characters != null) characters.Use(RiderHere);   // before the tray icon, which pictures him
            OverlayWindow.ScreensChanged += OnScreensChanged;      // a screen added, removed or resized can change the biggest size
            if (isCompanion) { StartAsCompanion(paths); return; }
            BuildMenu();
            RestorePosition();
            ApplySettings();
            if (rider != null) rider.Dropped += SavePositionSoon;
            if (rider != null) rider.RightClicked += OpenSettings;
            if (!Application.isEditor)
                listening = RiderSignal.Listen(RiderSignal.OpenSettings, () => MainThread.Post(OpenSettings),
                    ex => Log(LogLevel.Warning, "Right-clicking an extra rider will not open the settings.", ex));
            ready = true;
            // "DesktopRider3D.exe --open-settings" opens the settings window straight away (handy for a shortcut, and for checks).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--open-settings") >= 0) { Debug.Log("Opening the settings window (--open-settings)."); OpenSettings(); }
        }

        /// <summary>Who rides in this window: the main window's rider, or this companion's.</summary>
        RiderCharacter RiderHere => isCompanion ? companionRider : settings.Current.Appearance.Character;

        /// <summary>
        /// A companion window: rides its own rider with the main program's settings, read again whenever that file
        /// changes; closes when the main program closes or this rider is sent home. Saves nothing.
        /// </summary>
        void StartAsCompanion(AppPaths paths)
        {
            settings.Changed += OnSettingsChanged;
            try
            {
                var host = System.Diagnostics.Process.GetProcessById(hostProcessId);
                host.EnableRaisingEvents = true;
                host.Exited += (s, e) => MainThread.Post(Application.Quit);
                if (host.HasExited) { Application.Quit(); return; }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Debug.Log("The main Desktop Rider program is not running; this companion window closes.");
                Application.Quit();
                return;
            }
            if (paths != null)
            {
                try
                {
                    settingsWatcher = new FileSystemWatcher(paths.DataDirectory, Path.GetFileName(paths.SettingsFile))
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    };
                    FileSystemEventHandler changed = (s, e) => MainThread.Post(ReloadSoon);
                    settingsWatcher.Changed += changed;
                    settingsWatcher.Created += changed;
                    settingsWatcher.Renamed += (s, e) => MainThread.Post(ReloadSoon);
                    settingsWatcher.EnableRaisingEvents = true;
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or PlatformNotSupportedException)
                {
                    Log(LogLevel.Warning, "This companion window will not follow settings changes until it is opened again.", ex);
                }
            }
            // Starts in the bottom-right corner of the main screen, in a row beside the main window's rider (each in its
            // own place, so the riders do not all appear in one heap).
            if (rider != null)
            {
                var others = RiderLineUp.Companions(settings.Current.Appearance);
                int slot = 1 + Math.Max(0, IndexOf(others, companionRider));
                rider.MoveTo(CornerSpot(slot));
            }
            if (rider != null) rider.RightClicked += AskHostForSettings;
            listening = RiderSignal.Listen(RiderSignal.DoATrick(companionRider), () => MainThread.Post(() => TryTrickHere()),
                ex => Debug.LogWarning("\"Do a trick\" from the menu will not reach " + companionRider + ": " + ex.Message));
            ApplySettings();
            ready = true;
            Debug.Log("Companion window for " + companionRider + " is riding.");
        }

        /// <summary>A companion: asks the main program to open the settings window (and lets it come to the front).</summary>
        void AskHostForSettings()
        {
            AllowSetForegroundWindow(hostProcessId);
            if (!RiderSignal.Send(RiderSignal.OpenSettings)) Debug.LogWarning("Could not ask the main Desktop Rider for its settings.");
        }

        /// <summary>Saving writes the file in steps, so several change notices come at once: read it once, a moment later.</summary>
        void ReloadSoon()
        {
            if (reloadQueued || this == null) return;
            reloadQueued = true;
            _ = ReloadAsync();
        }

        async Task ReloadAsync()
        {
            await Task.Delay(250);
            reloadQueued = false;
            if (settings is SettingsService service) await Task.Run(() => service.ReloadAsync());
        }

        void BuildMenu()
        {
            overlay = new OverlayViewModel(settings, Logger<OverlayViewModel>());
            overlay.PropertyChanged += OnOverlayChanged;
            overlay.ExitRequested += (s, e) => MainThread.Post(Application.Quit);
            overlay.ResetPositionRequested += (s, e) => MainThread.Post(MoveAllToCorner);
            settings.Changed += OnSettingsChanged;

            string exe;
            using (var process = System.Diagnostics.Process.GetCurrentProcess()) exe = process.MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exe)) exe = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, "DesktopRider3D.exe");
            companions = new CompanionRiders(exe, (message, ex) => Log(LogLevel.Warning, message, ex));
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

        /// <summary>
        /// "Do a trick" from the tray menu or the settings window: one of the riders out, picked at random, does a trick. The main window's rider
        /// does it here; an extra rider is asked through its signal.
        /// </summary>
        void DoATrick()
        {
            if (!settings.Current.Appearance.CharacterVisible) return;
            var outNow = RiderLineUp.Out(settings.Current.Appearance);
            var who = outNow[UnityEngine.Random.Range(0, outNow.Count)];
            if (who != RiderHere && RiderSignal.Send(RiderSignal.DoATrick(who))) return;
            if (!TryTrickHere())
                trayIcon?.ShowHint("Desktop Rider", "There isn't enough clear space on the screen for a trick right now.");
        }

        /// <summary>This window's rider does a trick if it can. False when it cannot (busy, hidden or short of room).</summary>
        bool TryTrickHere()
        {
            if (tricks == null || tricks.IsRunning || !settings.Current.Appearance.CharacterVisible) return false;
            if (!tricks.TryPerform()) return false;
            ownTricks.Restart(Time.realtimeSinceStartup);   // one asked for counts: the next one by themselves waits a full gap
            return true;
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
                settingsWindow.ResetRequested += MoveAllToCorner;
                settingsWindow.TrickRequested += DoATrick;
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
            if (isCompanion && !RiderLineUp.Companions(appearance).Contains(companionRider))
            {
                Application.Quit();   // sent home (the main program also closes this window; this is in case it cannot)
                return;
            }
            if (characters != null) characters.Use(RiderHere);
            if (!isCompanion && companions != null && !Application.isEditor) companions.Match(RiderLineUp.Companions(appearance));
            if (rider != null)
            {
                rider.enabled = visible;
                rider.RidingSpeed = (float)settings.Current.Behaviour.RidingSpeed;
                rider.Roaming = settings.Current.Behaviour.FreeRiding;
                rider.SetSizeScale(DrawnSize);
                rider.SetAnimationSpeed((float)appearance.AnimationSpeed);
                rider.Director.IdleAnimations = settings.Current.Behaviour.IdleAnimations;
            }
            // Only when the size setting changed: any other change (or saving his place) left the window as it was. Resizing
            // every time shrank a window grown for a trick, which flickers (see OverlayWindow.Grow).
            int own = overlayWindow != null ? overlayWindow.SizeFor(DrawnSize) : 0;
            if (overlayWindow != null && OverlayWindow.BaseSize != new Vector2Int(own, own)) OverlayWindow.Resize(own);
            // The rider sets its own frame rate while shown; hidden, nothing is drawn, so tick over slowly.
            if (!visible) Application.targetFrameRate = hiddenFrameRate;
        }

        /// <summary>
        /// The size setting, kept within what the smallest screen suits (AppearanceSettings.MaxScaleFor), so the biggest
        /// size on a small screen is smaller than on a full-HD one. Screen widths are counted at 100% Windows scaling.
        /// </summary>
        float DrawnSize
        {
            get
            {
                double narrowest = double.MaxValue;
                foreach (var screen in OverlayWindow.Area.Screens) narrowest = Math.Min(narrowest, screen.Width / (double)OverlayWindow.ScaleFactor);
                return (float)AppearanceSettings.ScaleFor(settings.Current.Appearance.Scale, narrowest);
            }
        }

        void OnScreensChanged() => MainThread.Post(ApplySettings);

        void RestorePosition()
        {
            if (rider == null) return;
            var saved = settings.Current.Appearance.Position;
            if (saved == null) { MoveToMiddle(); return; }
            var offset = OverlayWindow.WindowsOffset;
            rider.MoveTo(new Vector2((float)saved.X + offset.X, (float)saved.Y + offset.Y));
        }

        /// <summary>"Move to corner": this rider to the bottom-right corner of the main screen, the companions in a row beside it.</summary>
        void MoveAllToCorner()
        {
            if (rider != null) rider.MoveTo(CornerSpot(0));
            if (!isCompanion && companions != null && !Application.isEditor) companions.Restart();
        }

        /// <summary>
        /// A place in the bottom-right corner of the main screen's work area: slot 0 is nearest the corner, each next slot
        /// one rider's length further left.
        /// </summary>
        static Vector2 CornerSpot(int slot)
        {
            var main = OverlayWindow.Area.Main;
            float unit = OverlayWindow.ScaleFactor;
            return new Vector2(main.X + main.Width - (170f + 240f * slot) * unit, main.Y + main.Height - 110f * unit);
        }

        static int IndexOf(System.Collections.Generic.IReadOnlyList<RiderCharacter> list, RiderCharacter who)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == who) return i;
            return -1;
        }

        void MoveToMiddle()
        {
            if (rider == null) return;
            var centre = OverlayWindow.Area.MainCentre;
            rider.MoveTo(new Vector2(centre.X, centre.Y));
        }

        void SavePositionSoon() { if (!isCompanion) _ = SavePositionAsync(); }

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
            companions?.Dispose();
            if (ready && !isCompanion)
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
            OverlayWindow.ScreensChanged -= OnScreensChanged;
            companions?.Dispose();
            companions = null;
            settingsWatcher?.Dispose();
            settingsWatcher = null;
            instanceGuard?.Dispose();
            instanceGuard = null;
            listening?.Dispose();
            listening = null;
            settingsWindow?.Dispose();
            settingsWindow = null;
            trayIcon?.Dispose();
            trayIcon = null;
            if (overlay != null) { overlay.PropertyChanged -= OnOverlayChanged; overlay.Dispose(); }
            trayMenu?.Dispose();
            if (settings != null) settings.Changed -= OnSettingsChanged;
            if (rider != null) { rider.Dropped -= SavePositionSoon; rider.RightClicked -= OpenSettings; rider.RightClicked -= AskHostForSettings; }
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
