#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// Deliberately in the namespace that encloses every test, so that inside the tests the name "Assert" means
// this class rather than NUnit's. A failure is still reported through NUnit.
namespace DesktopRider
{
    /// <summary>
    /// The checks the tests use. Version 1's tests were written for xUnit; these methods have the same names and
    /// the same meaning, so every assertion was carried over to NUnit word for word. They only rely on the small
    /// part of NUnit that both Unity's copy and the current release share, and the awaitable ones never block
    /// the thread, which matters inside the Unity editor.
    /// </summary>
    internal static class Assert
    {
        public static void True([DoesNotReturnIf(false)] bool condition, string? message = null)
        {
            if (!condition)
            {
                Fail(message ?? "Expected true but was false.");
            }
        }

        public static void False([DoesNotReturnIf(true)] bool condition, string? message = null)
        {
            if (condition)
            {
                Fail(message ?? "Expected false but was true.");
            }
        }

        public static void Equal<T>(T expected, T actual)
        {
            if (!AreEqual(expected, actual))
            {
                Fail($"Expected: {Show(expected)}\nActual:   {Show(actual)}");
            }
        }

        /// <summary>Checks two lists hold equal items in the same order.</summary>
        public static void Equal<T>(IEnumerable<T>? expected, IEnumerable<T>? actual) => Equal<object?>(expected, actual);

        /// <summary>Checks two numbers are equal once both are rounded to <paramref name="precision"/> decimal places.</summary>
        public static void Equal(double expected, double actual, int precision)
        {
            if (Math.Round(expected, precision) != Math.Round(actual, precision))
            {
                Fail($"Expected: {Show(expected)} (to {precision} decimal places)\nActual:   {Show(actual)}");
            }
        }

        public static void NotEqual<T>(T expected, T actual)
        {
            if (AreEqual(expected, actual))
            {
                Fail($"Expected a value other than {Show(expected)}.");
            }
        }

        public static void Null(object? value)
        {
            if (value is not null)
            {
                Fail($"Expected null but was {Show(value)}.");
            }
        }

        public static void NotNull([NotNull] object? value)
        {
            if (value is null)
            {
                Fail("Expected a value but was null.");
            }
        }

        public static void Same(object? expected, object? actual)
        {
            if (!ReferenceEquals(expected, actual))
            {
                Fail($"Expected the very same object.\nExpected: {Show(expected)}\nActual:   {Show(actual)}");
            }
        }

        public static void NotSame(object? expected, object? actual)
        {
            if (ReferenceEquals(expected, actual))
            {
                Fail("Expected two different objects but both were the same one.");
            }
        }

        public static void Empty(IEnumerable collection)
        {
            NotNull(collection);
            var items = collection.Cast<object?>().ToList();
            if (items.Count != 0)
            {
                Fail($"Expected nothing but found {Show(items)}.");
            }
        }

        public static void NotEmpty(IEnumerable collection)
        {
            NotNull(collection);
            if (!collection.Cast<object?>().Any())
            {
                Fail("Expected at least one item but found none.");
            }
        }

        public static T Single<T>(IEnumerable<T> collection)
        {
            NotNull(collection);
            var items = collection.ToList();
            if (items.Count != 1)
            {
                Fail($"Expected exactly one item but found {items.Count}: {Show(items)}");
            }

            return items[0];
        }

        public static T Single<T>(IEnumerable<T> collection, Predicate<T> predicate)
        {
            NotNull(collection);
            var items = collection.Where(item => predicate(item)).ToList();
            if (items.Count != 1)
            {
                Fail($"Expected exactly one matching item but found {items.Count}: {Show(items)}");
            }

            return items[0];
        }

        public static void All<T>(IEnumerable<T> collection, Action<T> check)
        {
            NotNull(collection);
            foreach (var item in collection.ToList())
            {
                check(item);
            }
        }

        public static void Contains(string expectedSubstring, string? actual) =>
            Contains(expectedSubstring, actual, StringComparison.CurrentCulture);

        public static void Contains(string expectedSubstring, string? actual, StringComparison comparison)
        {
            if (actual is null || actual.IndexOf(expectedSubstring, comparison) < 0)
            {
                Fail($"Expected text containing: {Show(expectedSubstring)}\nActual: {Show(actual)}");
            }
        }

        public static void Contains<T>(T expected, IEnumerable<T> collection)
        {
            NotNull(collection);
            var items = collection.ToList();
            if (!items.Any(item => AreEqual(expected, item)))
            {
                Fail($"Expected to find {Show(expected)} in {Show(items)}.");
            }
        }

        public static void Contains<T>(IEnumerable<T> collection, Predicate<T> predicate)
        {
            NotNull(collection);
            var items = collection.ToList();
            if (!items.Any(item => predicate(item)))
            {
                Fail($"Expected a matching item but found none in {Show(items)}.");
            }
        }

        public static void DoesNotContain(string unexpectedSubstring, string? actual) =>
            DoesNotContain(unexpectedSubstring, actual, StringComparison.CurrentCulture);

        public static void DoesNotContain(string unexpectedSubstring, string? actual, StringComparison comparison)
        {
            if (actual is not null && actual.IndexOf(unexpectedSubstring, comparison) >= 0)
            {
                Fail($"Expected text without: {Show(unexpectedSubstring)}\nActual: {Show(actual)}");
            }
        }

        public static void DoesNotContain<T>(T unexpected, IEnumerable<T> collection)
        {
            NotNull(collection);
            var items = collection.ToList();
            if (items.Any(item => AreEqual(unexpected, item)))
            {
                Fail($"Expected not to find {Show(unexpected)} in {Show(items)}.");
            }
        }

        public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> predicate)
        {
            NotNull(collection);
            var items = collection.ToList();
            if (items.Any(item => predicate(item)))
            {
                Fail($"Expected no matching item but found one in {Show(items)}.");
            }
        }

        public static void StartsWith(string expectedStart, string? actual) =>
            StartsWith(expectedStart, actual, StringComparison.CurrentCulture);

        public static void StartsWith(string expectedStart, string? actual, StringComparison comparison)
        {
            if (actual is null || !actual.StartsWith(expectedStart, comparison))
            {
                Fail($"Expected text starting with: {Show(expectedStart)}\nActual: {Show(actual)}");
            }
        }

        public static void EndsWith(string expectedEnd, string? actual) =>
            EndsWith(expectedEnd, actual, StringComparison.CurrentCulture);

        public static void EndsWith(string expectedEnd, string? actual, StringComparison comparison)
        {
            if (actual is null || !actual.EndsWith(expectedEnd, comparison))
            {
                Fail($"Expected text ending with: {Show(expectedEnd)}\nActual: {Show(actual)}");
            }
        }

        public static void Matches(string pattern, string? actual)
        {
            if (actual is null || !Regex.IsMatch(actual, pattern))
            {
                Fail($"Expected text matching the pattern: {pattern}\nActual: {Show(actual)}");
            }
        }

        public static void InRange<T>(T actual, T low, T high)
            where T : IComparable<T>
        {
            if (actual.CompareTo(low) < 0 || actual.CompareTo(high) > 0)
            {
                Fail($"Expected a value from {Show(low)} to {Show(high)} but was {Show(actual)}.");
            }
        }

        /// <summary>Checks the object is exactly a <typeparamref name="T"/>, not something derived from it.</summary>
        public static T IsType<T>([NotNull] object? value)
        {
            if (value is null || value.GetType() != typeof(T))
            {
                Fail($"Expected a {typeof(T).FullName} but was {(value is null ? "null" : value.GetType().FullName)}.");
            }

            return (T)value;
        }

        /// <summary>Checks the code throws exactly a <typeparamref name="T"/>, not something derived from it.</summary>
        public static T Throws<T>(Action code)
            where T : Exception => Thrown<T>(Record.Exception(code), exactType: true);

        /// <summary>Checks the code throws exactly a <typeparamref name="T"/>, not something derived from it.</summary>
        public static T Throws<T>(Func<object?> code)
            where T : Exception => Thrown<T>(Record.Exception(() => _ = code()), exactType: true);

        /// <summary>Checks the work fails with exactly a <typeparamref name="T"/>, not something derived from it.</summary>
        public static async Task<T> ThrowsAsync<T>(Func<Task> work)
            where T : Exception => Thrown<T>(await Record.ExceptionAsync(work), exactType: true);

        /// <summary>Checks the work fails with a <typeparamref name="T"/> or something derived from it.</summary>
        public static async Task<T> ThrowsAnyAsync<T>(Func<Task> work)
            where T : Exception => Thrown<T>(await Record.ExceptionAsync(work), exactType: false);

        [DoesNotReturn]
        private static void Fail(string message)
        {
            NUnit.Framework.Assert.Fail(message);
            throw new InvalidOperationException(message);
        }

        private static T Thrown<T>(Exception? exception, bool exactType)
            where T : Exception
        {
            if (exception is null)
            {
                Fail($"Expected a {typeof(T).FullName} to be thrown, but nothing was.");
            }

            if (exactType ? exception.GetType() != typeof(T) : exception is not T)
            {
                Fail($"Expected a {typeof(T).FullName} to be thrown, but it was a {exception.GetType().FullName}: {exception.Message}");
            }

            return (T)exception;
        }

        // Two lists are equal when they hold equal items in the same order; anything else is compared by its own Equals.
        private static bool AreEqual(object? expected, object? actual)
        {
            if (expected is null || actual is null)
            {
                return expected is null && actual is null;
            }

            if (expected is not string && actual is not string && expected is IEnumerable expectedItems && actual is IEnumerable actualItems)
            {
                var left = expectedItems.Cast<object?>().ToList();
                var right = actualItems.Cast<object?>().ToList();
                return left.Count == right.Count && left.Zip(right, AreEqual).All(same => same);
            }

            return expected.Equals(actual);
        }

        private static string Show(object? value) => value switch
        {
            null => "null",
            string text => "\"" + text + "\"",
            IEnumerable items => "[" + string.Join(", ", items.Cast<object?>().Select(Show)) + "]",
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    /// <summary>Runs code and hands back whatever it threw, or null if it threw nothing.</summary>
    internal static class Record
    {
        public static Exception? Exception(Action code)
        {
            try
            {
                code();
                return null;
            }
            catch (Exception ex) when (ex is not NUnit.Framework.AssertionException)
            {
                return ex;
            }
        }

        public static async Task<Exception?> ExceptionAsync(Func<Task> work)
        {
            try
            {
                await work();
                return null;
            }
            catch (Exception ex) when (ex is not NUnit.Framework.AssertionException)
            {
                return ex;
            }
        }
    }
}
