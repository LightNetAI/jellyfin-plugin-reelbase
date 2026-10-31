using System.Text.Json;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.Reelbase.Models;
using Jellyfin.Plugin.Reelbase.Providers;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// The album is not in the API yet. These pin the shapes the plugin accepts, so that the field
/// starts filling the moment the API sends one instead of needing another release.
/// </summary>
public class AlbumTests
{
    [Fact]
    public void ReadsAnAlbumSentAsAName()
    {
        Assert.Equal("One in a Million", Read("""{"title":"Try Again","album":"One in a Million"}""").AlbumName);
    }

    [Fact]
    public void ReadsAnAlbumSentAsAnObject()
    {
        Assert.Equal("One in a Million", Read("""{"title":"Try Again","album":{"name":"One in a Million"}}""").AlbumName);
        Assert.Equal("One in a Million", Read("""{"title":"Try Again","album":{"title":"One in a Million"}}""").AlbumName);
    }

    [Fact]
    public void LeavesTheAlbumEmptyWhenTheRecordHasNone()
    {
        Assert.Null(Read("""{"title":"Try Again"}""").AlbumName);
        Assert.Null(Read("""{"title":"Try Again","album":null}""").AlbumName);
        Assert.Null(Read("""{"title":"Try Again","album":""}""").AlbumName);
        Assert.Null(Read("""{"title":"Try Again","album":{}}""").AlbumName);
    }

    [Fact]
    public void TrimsWhatTheApiSends()
    {
        Assert.Equal("One in a Million", Read("""{"title":"Try Again","album":"  One in a Million  "}""").AlbumName);
    }

    [Fact]
    public void TheAlbumReachesTheItemTheProviderReturns()
    {
        var item = ReelbaseMusicVideoProvider.ToMusicVideo(
            Read("""{"title":"Time To Start","artist":{"name":"Blue Man Group"},"album":"Time To Start"}"""));

        Assert.Equal("Time To Start", item.Album);
        Assert.Equal("Blue Man Group", Assert.Single(item.Artists));
    }

    private static ReelbaseVideo Read(string json)
        => JsonSerializer.Deserialize<ReelbaseVideo>(json, JsonDefaults.Options)
           ?? throw new InvalidOperationException("Could not deserialize.");
}
