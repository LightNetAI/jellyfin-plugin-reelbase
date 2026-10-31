using Jellyfin.Plugin.Reelbase.Models;
using Jellyfin.Plugin.Reelbase.Providers;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// The identification dialog draws a result as its name, the provider, AlbumArtist and the year.
/// It never reads Artists, so both have to carry the artist.
/// </summary>
public class SearchResultTests
{
    [Fact]
    public void ToSearchResult_CarriesTheArtistWhereTheDialogLooks()
    {
        var video = new ReelbaseVideo
        {
            Title = "Miss Misunderstood",
            Slug = "miss-misunderstood",
            Year = 2026,
            ArtworkUrl = "https://api.reelbase.me/uploads/artwork/a.jpg",
            Artist = new ReelbaseEntityRef { Name = "Zolita", Slug = "zolita" }
        };

        var result = ReelbaseMusicVideoProvider.ToSearchResult(video);

        Assert.Equal("Miss Misunderstood", result.Name);
        Assert.Equal("Reelbase", result.SearchProviderName);
        Assert.Equal(2026, result.ProductionYear);
        Assert.Equal("https://api.reelbase.me/uploads/artwork/a.jpg", result.ImageUrl);
        Assert.Equal("miss-misunderstood", result.ProviderIds["Reelbase"]);

        // The dialog only reads AlbumArtist; Artists is kept because it is the truthful field.
        Assert.Equal("Zolita", result.AlbumArtist?.Name);
        Assert.Equal("Zolita", Assert.Single(result.Artists!).Name);
    }

    [Fact]
    public void ToSearchResult_LeavesTheArtistEmptyWhenTheRecordHasNone()
    {
        var result = ReelbaseMusicVideoProvider.ToSearchResult(new ReelbaseVideo { Title = "Untitled", Slug = "untitled" });

        Assert.Null(result.AlbumArtist);
        Assert.Empty(result.Artists!);
    }
}
