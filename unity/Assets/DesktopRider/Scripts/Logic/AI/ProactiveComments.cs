#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Logging;
using DesktopRider.Core.ViewModels;

namespace DesktopRider.AI
{
    /// <summary>The hours of the day when the rider keeps to itself.</summary>
    public static class QuietHours
    {
        /// <summary>
        /// True if <paramref name="time"/> falls in the quiet hours. They may run past midnight (22:00 to 08:00).
        /// The same start and end means there are none.
        /// </summary>
        public static bool Cover(TimeOnly time, TimeOnly start, TimeOnly end) =>
            start < end ? time >= start && time < end
            : start > end && (time >= start || time < end);
    }

    /// <summary>
    /// Lets the rider start a conversation now and then, when and only when the user has switched that on.
    /// While it is off this class sets no timer and makes no request of any kind. While it is on, one timer waits
    /// for the chosen time since anything was last said; nothing is checked in between. The rider then stays
    /// silent if it is muted, in quiet hours, hidden, busy answering or has no AI model, and after a few remarks
    /// in a row that the user did not answer it stops until the user next speaks.
    /// </summary>
    public sealed partial class ProactiveComments : ITrayMenuContributor, IDisposable
    {
        public const string EnableText = "Let the rider start conversations";
        public const string MuteText = "Keep the rider quiet for an hour";
        public const string UnmuteText = "Let the rider speak again";

        /// <summary>How many remarks in a row the rider makes without an answer before it stops until spoken to.</summary>
        public const int MaxUnanswered = 3;

        public static readonly TimeSpan MuteLength = TimeSpan.FromHours(1);

        private readonly ConversationViewModel _conversation;
        private readonly ISettingsService _settings;
        private readonly TimeProvider _time;
        private readonly ILogger<ProactiveComments> _logger;
        private readonly Action<Action> _onInterfaceThread;
        private readonly object _gate = new();
        private ITimer? _timer;
        private DateTimeOffset? _due;
        private DateTimeOffset _mutedUntil = DateTimeOffset.MinValue;
        private int _unanswered;
        private bool _started;
        private bool _appliedEnabled;
        private int _appliedInterval;
        private bool _disposed;

        /// <param name="conversation">The conversation the rider speaks in.</param>
        /// <param name="settings">The user's choices.</param>
        /// <param name="timeProvider">The clock and timers.</param>
        /// <param name="logger">Where problems are recorded.</param>
        /// <param name="onInterfaceThread">
        /// Runs work on the interface thread, where the conversation lives. Timers go off on a background thread.
        /// Left out, the work runs where the timer went off.
        /// </param>
        public ProactiveComments(
            ConversationViewModel conversation,
            ISettingsService settings,
            TimeProvider? timeProvider = null,
            ILogger<ProactiveComments>? logger = null,
            Action<Action>? onInterfaceThread = null)
        {
            _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _time = timeProvider ?? TimeProvider.System;
            _logger = logger ?? NullLogger<ProactiveComments>.Instance;
            _onInterfaceThread = onInterfaceThread ?? (work => work());
        }

        /// <summary>True while the user has switched this on.</summary>
        public bool Enabled => _settings.Current.Behaviour.ProactiveComments;

        /// <summary>True while the user has asked the rider to keep quiet for a while.</summary>
        public bool IsMuted => _time.GetUtcNow() < _mutedUntil;

        /// <summary>When the rider will next consider saying something. Null while no timer is set.</summary>
        public DateTimeOffset? NextDue
        {
            get
            {
                lock (_gate)
                {
                    return _due;
                }
            }
        }

        /// <summary>
        /// True if this is a moment when the rider may do something of its own accord: it is not muted, not in quiet
        /// hours, not hidden and not in the middle of answering. Says nothing about whether the user has switched that on.
        /// </summary>
        public bool MayActNow()
        {
            var settings = _settings.Current;
            var behaviour = settings.Behaviour;
            if (IsMuted || _conversation.IsBusy || !settings.Appearance.CharacterVisible)
            {
                return false;
            }

            return !(behaviour.QuietHoursEnabled
                && QuietHours.Cover(TimeOnly.FromDateTime(_time.GetLocalNow().DateTime), behaviour.QuietHoursStart, behaviour.QuietHoursEnd));
        }

        /// <summary>The words for the "keep quiet" choice on the rider's menu, or null when there is nothing to offer.</summary>
        public string? MuteChoiceText => !Enabled ? null : IsMuted ? UnmuteText : MuteText;

        /// <summary>Call once at start-up, after the settings are ready. Does nothing unless the user has switched this on.</summary>
        public void Start()
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _settings.Changed += OnSettingsChanged;
            _conversation.Lines.CollectionChanged += OnLinesChanged;
            Apply(force: true);
        }

        /// <summary>The easy way to hush the rider: no remarks for an hour, or until un-muted.</summary>
        public void Mute() => _mutedUntil = _time.GetUtcNow() + MuteLength;

        public void Unmute() => _mutedUntil = DateTimeOffset.MinValue;

        public void ToggleMute()
        {
            if (IsMuted)
            {
                Unmute();
            }
            else
            {
                Mute();
            }
        }

        /// <summary>Switches the rider's own remarks on or off and remembers the choice.</summary>
        public async Task SetEnabledAsync(bool value)
        {
            try
            {
                await _settings.UpdateAsync(s => s.Behaviour.ProactiveComments = value).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The choice still applies until Desktop Rider closes; it just could not be remembered.
                LogSaveFailed(_logger, ex);
                Apply(force: false);
            }
        }

        public IReadOnlyList<TrayMenuEntry> BuildEntries()
        {
            var enabled = Enabled;
            var entries = new List<TrayMenuEntry>
            {
                new(EnableText, new RelayCommand(() => _ = SetEnabledAsync(!Enabled)), enabled),
            };
            if (MuteChoiceText is { } mute)
            {
                entries.Add(new TrayMenuEntry(mute, new RelayCommand(ToggleMute)));
            }

            return entries;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_started)
            {
                _settings.Changed -= OnSettingsChanged;
                _conversation.Lines.CollectionChanged -= OnLinesChanged;
            }

            lock (_gate)
            {
                _timer?.Dispose();
                _timer = null;
                _due = null;
            }
        }

        private void OnSettingsChanged(object? sender, EventArgs e) => Apply(force: false);

        // Something the user says is an answer: the count of unanswered remarks starts again, and so does the wait.
        private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null && e.NewItems.OfType<ConversationLine>().Any(line => line.Speaker == Speaker.User))
            {
                _unanswered = 0;
                Schedule();
            }
        }

        // Settings are saved for many reasons (dragging the rider, for one), so the wait only starts again
        // when one of the choices that matter here has actually changed.
        private void Apply(bool force)
        {
            var behaviour = _settings.Current.Behaviour;
            if (!force && behaviour.ProactiveComments == _appliedEnabled && behaviour.ProactiveIntervalMinutes == _appliedInterval)
            {
                return;
            }

            if (behaviour.ProactiveComments && !_appliedEnabled)
            {
                _unanswered = 0;
            }

            _appliedEnabled = behaviour.ProactiveComments;
            _appliedInterval = behaviour.ProactiveIntervalMinutes;
            Schedule();
        }

        // Sets the one timer, or takes it away when the rider should not speak first.
        private void Schedule()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (!Enabled || _unanswered >= MaxUnanswered)
                {
                    _timer?.Dispose();
                    _timer = null;
                    _due = null;
                    return;
                }

                var wait = TimeSpan.FromMinutes(Math.Clamp(
                    _settings.Current.Behaviour.ProactiveIntervalMinutes,
                    BehaviourSettings.MinProactiveIntervalMinutes,
                    BehaviourSettings.MaxProactiveIntervalMinutes));
                _due = _time.GetUtcNow() + wait;
                if (_timer is null)
                {
                    _timer = _time.CreateTimer(_ => OnTimer(), null, wait, Timeout.InfiniteTimeSpan);
                }
                else
                {
                    _timer.Change(wait, Timeout.InfiniteTimeSpan);
                }
            }
        }

        private void OnTimer() => _onInterfaceThread(() => _ = SpeakIfSuitableAsync());

        private async Task SpeakIfSuitableAsync()
        {
            try
            {
                if (_disposed || !Enabled)
                {
                    return;
                }

                if (MaySpeakNow() && await _conversation.CommentAsync())
                {
                    _unanswered++;
                    LogSpoke(_logger, _unanswered);
                }

                // If the user spoke in the meantime the wait has already started again, which this simply repeats.
                Schedule();
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                // Desktop Rider is closing.
            }
        }

        private bool MaySpeakNow() => MayActNow() && !string.IsNullOrWhiteSpace(_settings.Current.Ai.Model);

        // What the rider said is never logged.
        private static void LogSpoke(ILogger logger, int unanswered)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.Log(LogLevel.Information, $"The rider started a conversation ({LogText.Of(unanswered)} in a row without an answer).", null);
            }
        }

        private static void LogSaveFailed(ILogger logger, Exception exception)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.Log(LogLevel.Warning, "The choice of whether the rider starts conversations could not be saved.", exception);
            }
        }
    }
}
