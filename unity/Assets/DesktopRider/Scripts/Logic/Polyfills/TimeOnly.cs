#nullable enable

using System;
using System.Globalization;

namespace DesktopRider
{
    /// <summary>
    /// A time of day with no date, such as 22:00. It mirrors the parts of .NET's own <c>TimeOnly</c> that
    /// Desktop Rider uses, because .NET Standard 2.1 (which Unity uses) does not include it.
    /// </summary>
    public readonly struct TimeOnly : IEquatable<TimeOnly>, IComparable<TimeOnly>
    {
        private readonly long _ticks;

        public TimeOnly(int hour, int minute)
            : this(hour, minute, 0)
        {
        }

        public TimeOnly(int hour, int minute, int second)
        {
            if (hour is < 0 or > 23)
            {
                throw new ArgumentOutOfRangeException(nameof(hour));
            }

            if (minute is < 0 or > 59)
            {
                throw new ArgumentOutOfRangeException(nameof(minute));
            }

            if (second is < 0 or > 59)
            {
                throw new ArgumentOutOfRangeException(nameof(second));
            }

            _ticks = new TimeSpan(hour, minute, second).Ticks;
        }

        public TimeOnly(long ticks)
        {
            if (ticks < 0 || ticks >= TimeSpan.TicksPerDay)
            {
                throw new ArgumentOutOfRangeException(nameof(ticks));
            }

            _ticks = ticks;
        }

        public int Hour => (int)(_ticks / TimeSpan.TicksPerHour);

        public int Minute => (int)(_ticks / TimeSpan.TicksPerMinute % 60);

        public int Second => (int)(_ticks / TimeSpan.TicksPerSecond % 60);

        public long Ticks => _ticks;

        public static TimeOnly FromDateTime(DateTime dateTime) => new(dateTime.TimeOfDay.Ticks);

        public static TimeOnly FromTimeSpan(TimeSpan timeSpan) => new(timeSpan.Ticks);

        public TimeSpan ToTimeSpan() => new(_ticks);

        /// <summary>The time this many hours later (or earlier, if negative), wrapping round midnight.</summary>
        public TimeOnly AddHours(double hours) => Add(TimeSpan.FromHours(hours));

        /// <summary>The time this many minutes later (or earlier, if negative), wrapping round midnight.</summary>
        public TimeOnly AddMinutes(double minutes) => Add(TimeSpan.FromMinutes(minutes));

        /// <summary>The time this much later (or earlier, if negative), wrapping round midnight.</summary>
        public TimeOnly Add(TimeSpan amount)
        {
            var ticks = (_ticks + amount.Ticks) % TimeSpan.TicksPerDay;
            return new TimeOnly(ticks < 0 ? ticks + TimeSpan.TicksPerDay : ticks);
        }

        public static TimeOnly Parse(string text, IFormatProvider? provider = null) =>
            FromDateTime(DateTime.Parse(text, provider, DateTimeStyles.NoCurrentDateDefault));

        public static bool TryParse(string? text, IFormatProvider? provider, DateTimeStyles style, out TimeOnly result)
        {
            if (DateTime.TryParse(text, provider, style | DateTimeStyles.NoCurrentDateDefault, out var parsed))
            {
                result = FromDateTime(parsed);
                return true;
            }

            result = default;
            return false;
        }

        public static bool TryParseExact(string? text, string[] formats, IFormatProvider? provider, DateTimeStyles style, out TimeOnly result)
        {
            if (DateTime.TryParseExact(text, formats, provider, style | DateTimeStyles.NoCurrentDateDefault, out var parsed))
            {
                result = FromDateTime(parsed);
                return true;
            }

            result = default;
            return false;
        }

        public string ToString(string format, IFormatProvider? provider = null) =>
            new DateTime(_ticks).ToString(format, provider);

        public override string ToString() => ToString("t", CultureInfo.CurrentCulture);

        public bool Equals(TimeOnly other) => _ticks == other._ticks;

        public override bool Equals(object? obj) => obj is TimeOnly other && Equals(other);

        public override int GetHashCode() => _ticks.GetHashCode();

        public int CompareTo(TimeOnly other) => _ticks.CompareTo(other._ticks);

        public static bool operator ==(TimeOnly left, TimeOnly right) => left._ticks == right._ticks;

        public static bool operator !=(TimeOnly left, TimeOnly right) => left._ticks != right._ticks;

        public static bool operator <(TimeOnly left, TimeOnly right) => left._ticks < right._ticks;

        public static bool operator >(TimeOnly left, TimeOnly right) => left._ticks > right._ticks;

        public static bool operator <=(TimeOnly left, TimeOnly right) => left._ticks <= right._ticks;

        public static bool operator >=(TimeOnly left, TimeOnly right) => left._ticks >= right._ticks;
    }
}
