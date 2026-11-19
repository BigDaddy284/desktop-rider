#nullable enable

using System;

namespace DesktopRider.Core.Tests
{
    public sealed class FakeTimeProvider : TimeProvider
    {
        public FakeTimeProvider(DateTimeOffset now) => Now = now;

        public DateTimeOffset Now { get; set; }

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
