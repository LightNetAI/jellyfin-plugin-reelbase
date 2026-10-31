using Jellyfin.Plugin.Reelbase.Models;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// The API answers with site relative paths, so every url has to be resolved.
/// </summary>
public class UrlTests
{
    [Theory]
    [InlineData("reelbase.example.com", "https://reelbase.example.com")]
    [InlineData("https://reelbase.example.com/", "https://reelbase.example.com")]
    [InlineData("http://localhost:8085///", "http://localhost:8085")]
    [InlineData("", "https://api.reelbase.me")]
    [InlineData(null, "https://api.reelbase.me")]
    public void NormalizeServerUrl_AddsSchemeAndDropsTrailingSlash(string? input, string expected)
        => Assert.Equal(expected, ReelbaseUrls.NormalizeServerUrl(input));

    [Theory]
    [InlineData("/video/miss-misunderstood", "https://api.reelbase.me/video/miss-misunderstood")]
    [InlineData("video/miss-misunderstood", "https://api.reelbase.me/video/miss-misunderstood")]
    [InlineData("/uploads/artwork/2026/10/a.jpg", "https://api.reelbase.me/uploads/artwork/2026/10/a.jpg")]
    public void Resolve_TurnsSiteRelativePathsIntoUrls(string input, string expected)
        => Assert.Equal(expected, ReelbaseUrls.Resolve("https://api.reelbase.me/", input));

    [Fact]
    public void Resolve_LeavesAbsoluteUrlsAlone()
        => Assert.Equal(
            "https://cdn.example.com/art.jpg",
            ReelbaseUrls.Resolve("https://api.reelbase.me", "https://cdn.example.com/art.jpg"));

    [Fact]
    public void Resolve_ReturnsNullForEmptyValues()
        => Assert.Null(ReelbaseUrls.Resolve("https://api.reelbase.me", null));

    [Fact]
    public void Normalizer_ResolvesThePathsOfASearchResult()
    {
        var video = new ReelbaseVideo
        {
            Slug = "suntanning-for-blacks",
            Url = "/video/suntanning-for-blacks",
            ArtworkUrl = "/uploads/artwork/2026/10/5b39c2c913532014f44b4ff8555bbbb5.jpg"
        };

        ReelbaseModelNormalizer.Normalize(video, "https://api.reelbase.me");

        Assert.Equal("https://api.reelbase.me/video/suntanning-for-blacks", video.Url);
        Assert.Equal("https://api.reelbase.me/uploads/artwork/2026/10/5b39c2c913532014f44b4ff8555bbbb5.jpg", video.ArtworkUrl);
    }

    [Fact]
    public void GetPageUrl_BuildsAPageUrlFromASlug()
        => Assert.Equal(
            "https://api.reelbase.me/artist/zolita",
            ReelbaseUrls.GetPageUrl("https://api.reelbase.me", "artist", "zolita"));
}
