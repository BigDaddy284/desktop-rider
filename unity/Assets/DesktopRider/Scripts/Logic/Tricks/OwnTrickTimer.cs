#nullable enable

using System;
using DesktopRider.Core.Configuration;

namespace DesktopRider.Tricks
{
    /// <summary>
    /// Decides when the rider does a trick by himself, from the "Tricks by himself" setting: never, sometimes (one every
    /// 5 to 10 minutes) or often (one every 2 to 4 minutes). The gap is picked at random each time so it never feels like
    /// a clock. When a trick is due but cannot start (the rider is hidden, being dragged, already doing one, or there is
    /// no room), it is tried again a little later instead of being lost. Pure timing: nothing here runs by itself; the
    /// caller asks <see cref="Due"/> now and then with the current time in seconds.
    /// </summary>
    public sealed class OwnTrickTimer
    {
        /// <summary>How long to wait before trying again when a due trick could not start.</summary>
        public const double RetrySeconds = 20.0;

        private readonly Random _random;
        private TrickFrequency? _frequency;
        private double _next = double.PositiveInfinity;

        public OwnTrickTimer(Random? random = null) => _random = random ?? new Random();

        /// <summary>When the next trick is due, in the caller's seconds; infinity when it is set to never.</summary>
        public double NextAt => _next;

        /// <summary>The shortest and longest wait between tricks done by himself, in seconds; null for never.</summary>
        public static (double Min, double Max)? GapSeconds(TrickFrequency frequency) => frequency switch
        {
            TrickFrequency.Sometimes => (300.0, 600.0),
            TrickFrequency.Often => (120.0, 240.0),
            _ => null,
        };

        /// <summary>
        /// True when a trick should start now. <paramref name="canStart"/> says whether one could start at this moment;
        /// if it is due but cannot, the timer tries again after <see cref="RetrySeconds"/>. Changing the frequency
        /// starts a fresh wait from <paramref name="now"/>.
        /// </summary>
        public bool Due(double now, TrickFrequency frequency, bool canStart)
        {
            if (_frequency != frequency)
            {
                _frequency = frequency;
                Schedule(now);
            }
            if (now < _next) return false;
            if (!canStart)
            {
                _next = now + RetrySeconds;
                return false;
            }
            Schedule(now);
            return true;
        }

        /// <summary>A trick that was due did not start after all (no room): try again after <see cref="RetrySeconds"/>.</summary>
        public void Retry(double now)
        {
            if (!double.IsPositiveInfinity(_next)) _next = now + RetrySeconds;
        }

        /// <summary>Starts a fresh wait from <paramref name="now"/>, for example after a trick the user asked for.</summary>
        public void Restart(double now)
        {
            if (_frequency != null) Schedule(now);
        }

        private void Schedule(double now)
        {
            var gap = GapSeconds(_frequency ?? TrickFrequency.Never);
            _next = gap is { } g ? now + g.Min + ((g.Max - g.Min) * _random.NextDouble()) : double.PositiveInfinity;
        }
    }
}
