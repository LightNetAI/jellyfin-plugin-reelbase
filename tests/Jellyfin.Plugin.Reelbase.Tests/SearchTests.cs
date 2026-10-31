using Jellyfin.Plugin.Reelbase.Models;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// The API matches q as one substring, so the terms and the ranking are what decide whether an
/// item finds its record.
/// </summary>
public class SearchTests
{
    [Theory]
    [InlineData("Zolita - Miss Misunderstood", "Zolita", "Miss Misunderstood")]
    [InlineData("Miss Misunderstood", "Zolita", "Miss Misunderstood")]
    [InlineData("Zolita - Miss Misunderstood.mp4", "Zolita", "Miss Misunderstood")]
    [InlineData("AC/DC - Thunderstruck", "AC/DC", "Thunderstruck")]
    [InlineData("Love - Hate", null, "Love - Hate")]
    public void StripArtistPrefix_ReducesANameToItsTitle(string name, string? artist, string expected)
        => Assert.Equal(expected, ReelbaseSearchQueries.StripArtistPrefix(name, artist));

    [Fact]
    public void ForMusicVideo_SearchesTheTitleBeforeTheArtist()
    {
        var queries = ReelbaseSearchQueries.ForMusicVideo("Zolita - Miss Misunderstood", new[] { "Zolita" });

        Assert.Equal(new[] { "Miss Misunderstood", "Zolita" }, queries);
    }

    [Fact]
    public void FindBestMatch_PicksTheTitleOverTheNewestResult()
    {
        var candidates = new[]
        {
            new ReelbaseVideo { Title = "Holy", Slug = "holy-4", Artist = new ReelbaseEntityRef { Name = "Zolita" }, Year = 2016 },
            new ReelbaseVideo { Title = "Miss Misunderstood", Slug = "miss-misunderstood", Artist = new ReelbaseEntityRef { Name = "Zolita" }, Year = 2026 }
        };

        var match = ReelbaseMatcher.FindBestMatch(candidates, "Miss Misunderstood", new[] { "Zolita" }, null);

        Assert.Equal("miss-misunderstood", match?.Slug);
    }

    [Fact]
    public void FindBestMatch_PrefersTheRightArtistWhenTitlesTie()
    {
        var candidates = new[]
        {
            new ReelbaseVideo { Title = "Holy", Slug = "holy-other", Artist = new ReelbaseEntityRef { Name = "Somebody Else" } },
            new ReelbaseVideo { Title = "Holy", Slug = "holy-zolita", Artist = new ReelbaseEntityRef { Name = "Zolita" } }
        };

        var match = ReelbaseMatcher.FindBestMatch(candidates, "Holy", new[] { "Zolita" }, null);

        Assert.Equal("holy-zolita", match?.Slug);
    }

    [Fact]
    public void FindBestMatch_RejectsAnUnrelatedResult()
    {
        var candidates = new[]
        {
            new ReelbaseVideo { Title = "Completely Different", Slug = "different", Artist = new ReelbaseEntityRef { Name = "Zolita" } }
        };

        Assert.Null(ReelbaseMatcher.FindBestMatch(candidates, "Miss Misunderstood", new[] { "Zolita" }, null));
    }

    [Fact]
    public void FindBestMatch_UsesTheYearAsATieBreaker()
    {
        var candidates = new[]
        {
            new ReelbaseVideo { Title = "Holy", Slug = "holy-2016", Artist = new ReelbaseEntityRef { Name = "Zolita" }, Year = 2016 },
            new ReelbaseVideo { Title = "Holy", Slug = "holy-2021", Artist = new ReelbaseEntityRef { Name = "Zolita" }, Year = 2021 }
        };

        var match = ReelbaseMatcher.FindBestMatch(candidates, "Holy", new[] { "Zolita" }, 2021);

        Assert.Equal("holy-2021", match?.Slug);
    }

    [Fact]
    public void Normalize_IgnoresCaseAccentsAndPunctuation()
        => Assert.Equal("raphael edwards", ReelbaseMatcher.Normalize("  Raphael EDWARDS! "));
}
