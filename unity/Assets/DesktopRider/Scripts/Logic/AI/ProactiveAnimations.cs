#nullable enable

using System;
using System.Linq;
using System.Threading;
using DesktopRider.Core.Configuration;

namespace DesktopRider.AI
{
    /// <summary>
    /// Makes the rider do something by itself now and then (a trick, a rev of the engine, a look round) while the user
    /// has let it start conversations. It never contacts the AI. While that choice is off, or the user has switched
    /// the rider's small movements off, this class sets no timer at all. While it is on, one timer waits a random
    /// time; nothing is checked in between. When the time comes the rider stays still if it is muted, in quiet hours,
    /// hidden or busy answering.
    /// </summary>
    public sealed class ProactiveAnimations : IDisposable
    {
        private readonly ProactiveComments _comments;
        private readonly ISettingsService _settings;
        private readonly TimeProvider _time;
        private readonly Random _random;
        private readonly Action<Action> _onInterfaceThread;
        private readonly object _gate = new();
        private ITimer? _timer;
        private DateTimeOffset? _due;
        private RiderAction? _last;
        private bool _started;
        private bool _appliedActive;
        private AnimationFrequency _appliedFrequency;
        private bool _disposed;

        /// <param name="comments">Knows whether the rider is muted, in quiet hours, hidden or busy.</param>
        /// <param name="settings">The user's choices.</param>
        /// <param name="timeProvider">The clock and timers.</param>
        /// <param name="random">The source of chance.</param>
        /// <param name="onInterfaceThread">Runs work on the interface thread. Left out, the work runs where the timer went off.</param>
        public ProactiveAnimations(
            ProactiveComments comments,
            ISettingsService settings,
            TimeProvider? timeProvider = null,
            Random? random = null,
            Action<Action>? onInterfaceThread = null)
        {
            _comments = comments ?? throw new ArgumentNullException(nameof(comments));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _time = timeProvider ?? TimeProvider.System;
            _random = random ?? SharedRandom.Instance;
            _onInterfaceThread = onInterfaceThread ?? (work => work());
        }

        /// <summary>Raised, on the interface thread, when the rider should do something of its own accord.</summary>
        public event EventHandler<RiderActionEventArgs>? ActionRequested;

        /// <summary>True while the rider may start conversations and its small movements are switched on.</summary>
        public bool Active => _settings.Current.Behaviour is { ProactiveComments: true, IdleAnimations: true };

        /// <summary>When the rider will next consider doing something. Null while no timer is set.</summary>
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

        /// <summary>The shortest and longest wait between the rider's own animations for each choice of how often.</summary>
        public static (TimeSpan Shortest, TimeSpan Longest) WaitFor(AnimationFrequency frequency) => frequency switch
        {
            AnimationFrequency.Rare => (TimeSpan.FromMinutes(8), TimeSpan.FromMinutes(16)),
            AnimationFrequency.Frequent => (TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3)),
            _ => (TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(8)),
        };

        /// <summary>Call once at start-up, after the settings are ready. Does nothing unless the user has switched this on.</summary>
        public void Start()
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _settings.Changed += OnSettingsChanged;
            Apply(force: true);
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
            }

            lock (_gate)
            {
                _timer?.Dispose();
                _timer = null;
                _due = null;
            }
        }

        private void OnSettingsChanged(object? sender, EventArgs e) => Apply(force: false);

        // Settings are saved for many reasons, so the wait only starts again when a choice that matters here has changed.
        private void Apply(bool force)
        {
            var active = Active;
            var frequency = _settings.Current.Behaviour.AnimationFrequency;
            if (!force && active == _appliedActive && frequency == _appliedFrequency)
            {
                return;
            }

            _appliedActive = active;
            _appliedFrequency = frequency;
            Schedule();
        }

        // Sets the one timer, or takes it away when the rider should not move by itself.
        private void Schedule()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (!Active)
                {
                    _timer?.Dispose();
                    _timer = null;
                    _due = null;
                    return;
                }

                var (shortest, longest) = WaitFor(_settings.Current.Behaviour.AnimationFrequency);
                var wait = shortest + ((longest - shortest) * _random.NextDouble());
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

        private void OnTimer() => _onInterfaceThread(() =>
        {
            if (_disposed || !Active)
            {
                return;
            }

            if (_comments.MayActNow())
            {
                ActionRequested?.Invoke(this, new RiderActionEventArgs(Pick()));
            }

            Schedule();
        });

        // Anything the rider can do, tricks and gestures alike, but never the same thing twice running.
        private RiderAction Pick()
        {
            var choices = RiderActions.All.Where(action => action != _last).ToList();
            _last = choices[_random.Next(choices.Count)];
            return _last;
        }
    }
}
