#nullable enable

using System;

namespace DesktopRider
{
    /// <summary>Argument checks that newer versions of .NET build in but .NET Standard 2.1 does not.</summary>
    internal static class Guard
    {
        public static void NotNull(object? value, string name)
        {
            if (value is null)
            {
                throw new ArgumentNullException(name);
            }
        }

        public static void NotBlank(string? value, string name)
        {
            if (value is null)
            {
                throw new ArgumentNullException(name);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", name);
            }
        }

        public static void Positive(int value, string name)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(name, value, "The value must be greater than zero.");
            }
        }

        public static void NotNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name, value, "The value must not be negative.");
            }
        }

        public static void NotDisposed(bool disposed, object instance)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(instance.GetType().FullName);
            }
        }
    }
}
