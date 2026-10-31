using Jellyfin.Plugin.Reelbase.Models;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// Checks the models against responses captured from the live API.
/// </summary>
public class ApiContractTests
{
    [Fact]
    public void VideoSearchResponse_IsRead()
    {
        var response = Fixture.Load<ReelbaseListResponse<ReelbaseVideo>>("videos-search.json");

        var video = Assert.Single(response.Data);
        Assert.Equal("Miss Misunderstood", video.Title);
        Assert.Equal("miss-misunderstood", video.Slug);
        Assert.Equal("Zolita", video.Artist?.Name);
        Assert.Equal(2026, video.Year);
        Assert.Equal("2026-09-11", video.ReleaseDate);
        Assert.Equal("/uploads/artwork/2026/10/94c5b5f6892b6d71d6cd6a9976e31cfc.jpg", video.ArtworkUrl);
        Assert.Equal("/video/miss-misunderstood", video.Url);
        Assert.NotNull(response.Meta);
        Assert.True(response.Meta!.Total >= 1);
    }

    [Fact]
    public void VideoDetailResponse_IsReadWithCreditsAndGenres()
    {
        var response = Fixture.Load<ReelbaseItemResponse<ReelbaseVideo>>("video-detail.json");

        Assert.NotNull(response.Data);
        var video = response.Data!;
        Assert.Equal("miss-misunderstood", video.Slug);
        Assert.Contains(video.Genres!, genre => genre.Name == "Pop");
        Assert.Contains(video.Credits!, credit => credit.Role == "Director" && credit.Person!.Slug == "zolita");
        Assert.All(video.Credits!, credit => Assert.False(string.IsNullOrEmpty(credit.Person?.Name)));
    }

    [Fact]
    public void ArtistResponses_AreRead()
    {
        var search = Fixture.Load<ReelbaseListResponse<ReelbaseArtist>>("artists-search.json");
        var artist = Assert.Single(search.Data);
        Assert.Equal("Zolita", artist.Name);
        Assert.Equal("zolita", artist.Slug);
        Assert.True(artist.VideoCount > 0);

        var detail = Fixture.Load<ReelbaseItemResponse<ReelbaseArtist>>("artist-detail.json");
        Assert.NotNull(detail.Data);
        Assert.Equal("zolita", detail.Data!.Slug);
        Assert.NotEmpty(detail.Data.Videos!);
    }

    [Fact]
    public void PersonResponses_AreRead()
    {
        var search = Fixture.Load<ReelbaseListResponse<ReelbasePerson>>("people-search.json");
        Assert.NotEmpty(search.Data);

        var detail = Fixture.Load<ReelbaseItemResponse<ReelbasePerson>>("person-detail.json");
        Assert.NotNull(detail.Data);
        Assert.Equal("Annette Milburn", detail.Data!.Name);
        Assert.Equal("annette-milburn", detail.Data.Slug);
        Assert.NotNull(detail.Data.Credits);
    }

    [Fact]
    public void ErrorResponse_IsRead()
    {
        var error = Fixture.Load<ReelbaseErrorResponse>("error.json");

        Assert.Equal(401, error.Error?.Status);
        Assert.False(string.IsNullOrEmpty(error.Error?.Message));
    }
}
