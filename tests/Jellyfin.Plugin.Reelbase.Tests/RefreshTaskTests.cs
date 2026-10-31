using Jellyfin.Plugin.Reelbase.Tasks;
using MediaBrowser.Controller.Providers;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// The refresh has to ask for a full refresh. With the default mode the server runs a provider
/// only when the item is new, its file changed, or the library's automatic refresh interval has
/// passed - so a library that has been scanned once is "refreshed" without a single lookup.
/// </summary>
public class RefreshTaskTests
{
    [Fact]
    public void EveryRefreshAsksForTheProvidersToRun()
    {
        var options = RefreshReelbaseTask.CreateRefreshOptions(null!);

        Assert.Equal(MetadataRefreshMode.FullRefresh, options.MetadataRefreshMode);
        Assert.Equal(MetadataRefreshMode.FullRefresh, options.ImageRefreshMode);
    }

    [Fact]
    public void ARefreshAppliesWhatTheProviderReturnsLikeAScanDoes()
    {
        var options = RefreshReelbaseTask.CreateRefreshOptions(null!);

        // Without this the merge keeps the name the item was scanned with, which is the file name.
        Assert.True(options.ReplaceAllMetadata);
        Assert.True(options.IsAutomated);

        // Artwork that is already there stays: replacing images is a separate switch.
        Assert.False(options.ReplaceAllImages);
    }
}
