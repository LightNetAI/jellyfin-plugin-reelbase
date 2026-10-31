using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Decides the order in which a refresh run visits the library.
/// </summary>
internal static class ReelbaseRefreshOrder
{
    /// <summary>
    /// Puts the items that were refreshed longest ago first.
    /// </summary>
    /// <remarks>
    /// A run stops when the allowance is spent, so the order is what makes the whole thing
    /// resumable: everything refreshed tonight sorts to the back tomorrow, and the items that
    /// were never reached stay at the front.
    /// </remarks>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to order.</param>
    /// <param name="lastRefreshed">Reads an item's last refresh time.</param>
    /// <returns>The items, least recently refreshed first.</returns>
    internal static IReadOnlyList<T> OldestFirst<T>(IEnumerable<T> items, Func<T, DateTime> lastRefreshed)
        => items.OrderBy(lastRefreshed).ToList();
}
