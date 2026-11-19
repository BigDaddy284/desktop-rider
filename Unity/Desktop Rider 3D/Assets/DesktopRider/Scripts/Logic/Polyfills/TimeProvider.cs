#nullable enable

using System;
using System.Threading;

namespace DesktopRider
{
    /// <summary>
    /// A clock that can be replaced in tests. It mirrors the parts of .NET's own <c>TimeProvider</c> that
    /// Desktop Rider uses, because .NET Standard 2.1 (which Unity uses) does not include it.
    /// </summary>
    public abstract class TimeProvider
    {
        /// <summary>The computer's real clock.</summary>
        public static TimeProvider System { get; } = new SystemTimeProvider();

        public virtual DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

        public virtual TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

        public DateTimeOffset GetLocalNow()
        {
            var utcNow = GetUtcNow();
            var zone = LocalTimeZone ?? throw new InvalidOperationException("The clock has no local time zone.");
            return utcNow.ToOffset(zone.GetUtcOffset(utcNow));
        }

        /// <summary>Starts a timer that calls back once after <paramref name="dueTime"/> and then every <paramref name="period"/>.</summary>
        public virtual ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return new SystemTimer(callback, state, dueTime, period);
        }

        private sealed class SystemTimeProvider : TimeProvider
        {
        }

        private sealed class SystemTimer : ITimer
        {
            private readonly Timer _timer;

            public SystemTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
                _timer = new Timer(callback, state, dueTime, period);

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                try
                {
                    return _timer.Change(dueTime, period);
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }

            public void Dispose() => _timer.Dispose();
        }
    }

    /// <summary>A timer made by a <see cref="TimeProvider"/>.</summary>
    public interface ITimer : IDisposable
    {
        /// <summary>Changes when the timer next calls back. Returns false if the timer has already been stopped for good.</summary>
        bool Change(TimeSpan dueTime, TimeSpan period);
    }
}
