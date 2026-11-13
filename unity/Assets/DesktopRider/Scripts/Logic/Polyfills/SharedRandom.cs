#nullable enable

using System;

namespace DesktopRider
{
    /// <summary>
    /// One random number source that is safe to use from any thread.
    /// It stands in for <c>Random.Shared</c>, which .NET Standard 2.1 does not have.
    /// </summary>
    internal static class SharedRandom
    {
        public static Random Instance { get; } = new LockedRandom();

        private sealed class LockedRandom : Random
        {
            private readonly object _gate = new();

            public override int Next()
            {
                lock (_gate)
                {
                    return base.Next();
                }
            }

            public override int Next(int maxValue)
            {
                lock (_gate)
                {
                    return base.Next(maxValue);
                }
            }

            public override int Next(int minValue, int maxValue)
            {
                lock (_gate)
                {
                    return base.Next(minValue, maxValue);
                }
            }

            public override double NextDouble()
            {
                lock (_gate)
                {
                    return base.NextDouble();
                }
            }

            public override void NextBytes(byte[] buffer)
            {
                lock (_gate)
                {
                    base.NextBytes(buffer);
                }
            }
        }
    }
}
