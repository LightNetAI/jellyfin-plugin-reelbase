using Jellyfin.Plugin.Reelbase;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// Jellyfin shows a music video's artist from ArtistItems, which only holds names backed by an
/// artist item. These pin the names that get asked for.
/// </summary>
public class ArtistLinkTests
{
    [Fact]
    public void AsksForEveryArtistOnce()
    {
        var asked = new List<string>();

        ReelbaseArtistLinks.Ensure(["Blue Man Group", "Aaliyah", "blue man group"], name =>
        {
            asked.Add(name);
            return null;
        });

        Assert.Equal(["Blue Man Group", "Aaliyah"], asked);
    }

    [Fact]
    public void SkipsNamesThatAreNotNames()
    {
        var asked = new List<string>();

        ReelbaseArtistLinks.Ensure(["", "  ", " Zolita "], name =>
        {
            asked.Add(name);
            return null;
        });

        Assert.Equal(["Zolita"], asked);
    }

    [Fact]
    public void AsksForNothingWhenThereIsNoArtist()
    {
        var asked = new List<string>();

        ReelbaseArtistLinks.Ensure(null, name =>
        {
            asked.Add(name);
            return null;
        });

        Assert.Empty(asked);
    }

    [Fact]
    public void OneArtistThatCannotBeLinkedDoesNotCostTheRest()
    {
        var asked = new List<string>();
        var failed = new List<string>();

        ReelbaseArtistLinks.Ensure(
            ["Rebexie", "Blue Man Group"],
            name =>
            {
                if (name == "Rebexie")
                {
                    throw new IOException("The artist folder could not be created.");
                }

                asked.Add(name);
                return null;
            },
            (name, _) => failed.Add(name));

        Assert.Equal(["Rebexie"], failed);
        Assert.Equal(["Blue Man Group"], asked);
    }
}
