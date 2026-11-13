#nullable enable

using System;
using System.Collections.Generic;

namespace DesktopRider
{
    /// <summary>Ways of searching a list that newer versions of .NET build in but .NET Standard 2.1 does not.</summary>
    internal static class Sequences
    {
        /// <summary>The first item with the largest key, or the default value when there are no items.</summary>
        public static T? MaxBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> key)
            where T : class
            where TKey : IComparable<TKey> => Best(items, key, wantLargest: true);

        /// <summary>The first item with the smallest key, or the default value when there are no items.</summary>
        public static T? MinBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> key)
            where T : class
            where TKey : IComparable<TKey> => Best(items, key, wantLargest: false);

        private static T? Best<T, TKey>(IEnumerable<T> items, Func<T, TKey> key, bool wantLargest)
            where T : class
            where TKey : IComparable<TKey>
        {
            Guard.NotNull(items, nameof(items));
            Guard.NotNull(key, nameof(key));
            T? best = null;
            var bestKey = default(TKey);
            foreach (var item in items)
            {
                var itemKey = key(item);
                if (best is null)
                {
                    best = item;
                    bestKey = itemKey;
                    continue;
                }

                var comparison = itemKey.CompareTo(bestKey!);
                if (wantLargest ? comparison > 0 : comparison < 0)
                {
                    best = item;
                    bestKey = itemKey;
                }
            }

            return best;
        }
    }
}
