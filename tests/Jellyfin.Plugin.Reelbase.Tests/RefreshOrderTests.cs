using System;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// The nightly run has to resume where the last one stopped, which is entirely down to this
/// ordering: everything refreshed tonight sorts to the back tomorrow.
/// </summary>
public class RefreshOrderTests
{
    [Fact]
    public void OldestFirst_PutsNeverRefreshedItemsFirst()
    {
        var fresh = (Name: "fresh", Refreshed: DateTime.UtcNow);
        var never = (Name: "never", Refreshed: DateTime.MinValue);
        var stale = (Name: "stale", Refreshed: DateTime.UtcNow.AddDays(-3));

        var ordered = ReelbaseRefreshOrder.OldestFirst([fresh, never, stale], item => item.Refreshed);

        Assert.Equal(["never", "stale", "fresh"], ordered.Select(item => item.Name));
    }

    [Fact]
    public void OldestFirst_KeepsTheOrderItWasGivenForEqualTimes()
    {
        var at = DateTime.UtcNow;
        var first = (Name: "first", Refreshed: at);
        var second = (Name: "second", Refreshed: at);

        var ordered = ReelbaseRefreshOrder.OldestFirst([first, second], item => item.Refreshed);

        Assert.Equal(["first", "second"], ordered.Select(item => item.Name));
    }
}
