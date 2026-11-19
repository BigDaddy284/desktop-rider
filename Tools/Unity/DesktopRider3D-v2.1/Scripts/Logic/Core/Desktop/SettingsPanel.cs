#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Logging;
using DesktopRider.Core.ViewModels;

namespace DesktopRider.Core.Desktop
{
    /// <summary>What kind of control a line of the settings window has.</summary>
    public enum SettingKind
    {
        /// <summary>A switch: on or off (value 1 or 0).</summary>
        Toggle,
        /// <summary>A row of buttons, one of which is picked (value: the index of the picked one).</summary>
        Choice,
        /// <summary>A slider between two ends (value between <see cref="SettingRow.Min"/> and <see cref="SettingRow.Max"/>).</summary>
        Slider,
    }

    /// <summary>One line of the settings window: its words, its control and where its value lives.</summary>
    public sealed class SettingRow
    {
        internal SettingRow(string id, string section, string title, string description, SettingKind kind,
                            Func<double> get, Func<double, Task> set)
        {
            Id = id; Section = section; Title = title; Description = description; Kind = kind; _get = get; _set = set;
        }

        private readonly Func<double> _get;
        private readonly Func<double, Task> _set;

        public string Id { get; }
        public string Section { get; }
        public string Title { get; }
        /// <summary>One short line under the title saying what it does, in plain words.</summary>
        public string Description { get; }
        public SettingKind Kind { get; }
        /// <summary>The buttons of a <see cref="SettingKind.Choice"/>.</summary>
        public IReadOnlyList<string> Choices { get; internal set; } = Array.Empty<string>();
        /// <summary>The words at the two ends of a <see cref="SettingKind.Slider"/>.</summary>
        public string LowLabel { get; internal set; } = string.Empty;
        public string HighLabel { get; internal set; } = string.Empty;
        public double Min { get; internal set; }
        public double Max { get; internal set; } = 1;

        public double Value => _get();

        public bool IsOn => Kind == SettingKind.Toggle && Value >= 0.5;

        /// <summary>
        /// A <see cref="SettingKind.Choice"/> where several buttons can be on at once ("Who rides"). Its value is one bit
        /// per button; setting it to a button's index turns that button on or off.
        /// </summary>
        public bool Several { get; internal set; }

        /// <summary>Whether the <paramref name="index"/>th button of a <see cref="SettingKind.Choice"/> is lit.</summary>
        public bool IsPicked(int index)
        {
            if (Kind != SettingKind.Choice || index < 0 || index >= Choices.Count) return false;
            int value = (int)Math.Round(Value);
            return Several ? ((value >> index) & 1) == 1 : value == index;
        }

        /// <summary>Sets the value (kept inside the row's range) and saves it.</summary>
        public Task SetAsync(double value)
        {
            value = Kind switch
            {
                SettingKind.Toggle => value >= 0.5 ? 1 : 0,
                SettingKind.Choice => Math.Max(0, Math.Min(Choices.Count - 1, Math.Round(value))),
                _ => Math.Max(Min, Math.Min(Max, value)),
            };
            return _set(value);
        }
    }

    /// <summary>
    /// The settings window's content, apart from how it is drawn: the lines it shows, grouped under a few headings, each
    /// reading and changing the saved settings directly, so a change applies at once and there is no Save button. Version 2
    /// has no chat or AI at all (Boss, 14 November 2026: they carry on in version 3). Also adds
    /// "Settings..." to the tray menu, which raises <see cref="Requested"/>.
    /// </summary>
    public sealed class SettingsPanel : ITrayMenuContributor
    {
        public const string MenuText = "Settings...";
        public const string Title = "Desktop Rider settings";
        public const string RiderSection = "The riders";
        public const string BehaviourSection = "What they do";
        public const string WindowsSection = "Windows";

        /// <summary>The names on the "Who rides" buttons, in the order of <see cref="RiderCharacter"/>.</summary>
        public static readonly IReadOnlyList<string> CharacterNames = new[] { "Harvey", "Roy/Dad", "Louie" };

        private readonly ISettingsService _settings;
        private readonly StartWithWindows? _startup;
        private readonly ILogger<SettingsPanel> _logger;

        /// <param name="startup">The "Start with Windows" switch's own handling (it also changes Windows' sign-in list); null leaves that line out.</param>
        public SettingsPanel(ISettingsService settings, StartWithWindows? startup = null, ILogger<SettingsPanel>? logger = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _startup = startup;
            _logger = logger ?? NullLogger<SettingsPanel>.Instance;
            Rows = BuildRows();
        }

        /// <summary>Raised when "Settings..." is chosen from the tray menu.</summary>
        public event EventHandler? Requested;

        public IReadOnlyList<SettingRow> Rows { get; }

        /// <summary>The headings, in the order they appear.</summary>
        public IReadOnlyList<string> Sections => Rows.Select(r => r.Section).Distinct().ToList();

        public SettingRow this[string id] => Rows.First(r => r.Id == id);

        public IReadOnlyList<TrayMenuEntry> BuildEntries() =>
            new[] { new TrayMenuEntry(MenuText, new RelayCommand(() => Requested?.Invoke(this, EventArgs.Empty))) };

        private IReadOnlyList<SettingRow> BuildRows()
        {
            var rows = new List<SettingRow>
            {
                new("character", RiderSection, "Who rides", "Click a name to bring that rider out or send them home. All three can ride at once.", SettingKind.Choice,
                    RidersOut, v => ToggleRider((RiderCharacter)(int)v))
                { Choices = CharacterNames, Several = true },
                new("size", RiderSection, "Size", "How big the riders are on your screen. A smaller screen has a lower top size.", SettingKind.Slider,
                    () => _settings.Current.Appearance.Scale, v => Save(s => s.Appearance.Scale = v))
                { Min = AppearanceSettings.MinScale, Max = AppearanceSettings.MaxScale, LowLabel = "Smaller", HighLabel = "Bigger" },
                new("speed", RiderSection, "Animation speed", "How quickly they move when they ride, rest and do tricks.", SettingKind.Slider,
                    () => _settings.Current.Appearance.AnimationSpeed, v => Save(s => s.Appearance.AnimationSpeed = v))
                { Min = AppearanceSettings.MinAnimationSpeed, Max = AppearanceSettings.MaxAnimationSpeed, LowLabel = "Slower", HighLabel = "Faster" },
                new("roam", BehaviourSection, "Ride around freely", "They ride about your screens by themselves. Off: they stay where you put them.", SettingKind.Toggle,
                    () => _settings.Current.Behaviour.FreeRiding ? 1 : 0, v => Save(s => s.Behaviour.FreeRiding = v >= 0.5)),
                new("ridingspeed", BehaviourSection, "Riding speed", "How fast they ride about your screens.", SettingKind.Slider,
                    () => _settings.Current.Behaviour.RidingSpeed, v => Save(s => s.Behaviour.RidingSpeed = v))
                { Min = BehaviourSettings.MinRidingSpeed, Max = BehaviourSettings.MaxRidingSpeed, LowLabel = "Slower", HighLabel = "Faster" },
                new("tricks", BehaviourSection, "Tricks by themselves", "How often they do a trick without being asked.", SettingKind.Choice,
                    () => (int)_settings.Current.Behaviour.TricksByHimself, v => Save(s => s.Behaviour.TricksByHimself = (TrickFrequency)(int)v))
                { Choices = new[] { "Never", "Sometimes", "Often" } },
                new("idle", BehaviourSection, "Little movements at rest", "They look around and fidget now and then while they wait.", SettingKind.Toggle,
                    () => _settings.Current.Behaviour.IdleAnimations ? 1 : 0, v => Save(s => s.Behaviour.IdleAnimations = v >= 0.5)),
                new("ontop", WindowsSection, "Always on top", "Keep them in front of your other windows.", SettingKind.Toggle,
                    () => _settings.Current.Appearance.AlwaysOnTop ? 1 : 0, v => Save(s => s.Appearance.AlwaysOnTop = v >= 0.5)),
            };
            if (_startup != null)
            {
                var startup = _startup;
                rows.Add(new SettingRow("startup", WindowsSection, "Start with Windows", "Desktop Rider opens by itself when you sign in.", SettingKind.Toggle,
                    () => startup.Enabled ? 1 : 0, v => startup.SetAsync(v >= 0.5)));
            }
            return rows;
        }

        /// <summary>One bit for each rider out, in button order.</summary>
        private double RidersOut() =>
            RiderLineUp.Out(_settings.Current.Appearance).Aggregate(0, (bits, who) => bits | (1 << (int)who));

        /// <summary>Brings a rider out or sends them home; the last one out stays, so nothing is saved for that click.</summary>
        private Task ToggleRider(RiderCharacter who)
        {
            var copy = new AppearanceSettings { Character = _settings.Current.Appearance.Character, Riders = new List<RiderCharacter>(_settings.Current.Appearance.Riders ?? new List<RiderCharacter>()) };
            if (!RiderLineUp.Toggle(copy, who)) return Task.CompletedTask;
            return Save(s => { s.Appearance.Character = copy.Character; s.Appearance.Riders = copy.Riders; });
        }

        private async Task Save(Action<AppSettings> change)
        {
            try
            {
                await _settings.UpdateAsync(change).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The change still applies for this session (the settings object already holds it); it just is not remembered.
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.Log(LogLevel.Warning, "A settings change could not be saved. It applies until Desktop Rider is closed.", ex);
                }
            }
        }
    }
}
