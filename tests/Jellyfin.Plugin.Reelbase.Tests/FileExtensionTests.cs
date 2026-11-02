using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// The API is told what kind of item was asked about: a file carries its extension, a folder and
/// an extensionless name carry <c>null</c>, so a miss on a file can be recorded as missing content
/// and a miss on a folder cannot.
/// </summary>
public class FileExtensionTests
{
    [Theory]
    [InlineData("/media/Music Videos/George Michael - Freedom `90.mkv", "mkv")]
    [InlineData("/media/Björk - Jóga.MKV", "mkv")]
    [InlineData("/media/Dein ist mein ganzes Herz.webm", "webm")]
    [InlineData("/media/Domingo, Netrebko & Villazón.m4v", "m4v")]
    [InlineData(@"C:\Music Videos\AC_DC - Thunderstruck.mp4", "mp4")]
    public void ReadsTheExtensionOfTheFile(string path, string expected)
        => Assert.Equal(expected, ReelbaseFileExtension.QueryValue(path, null));

    [Theory]
    [InlineData("/media/Linkin Park")] // a folder
    [InlineData("/media/Mr. Brightside")] // a dot that is part of the title
    [InlineData("/media/Song Vol. 2")]
    [InlineData("/media/trailing.")]
    [InlineData("")]
    public void SendsNullWhenThereIsNoExtension(string path)
        => Assert.Equal(ReelbaseFileExtension.None, ReelbaseFileExtension.QueryValue(path, null));

    [Fact]
    public void UsesTheNameWhenTheItemHasNoPath()
    {
        // The identify dialog builds its lookup from the typed name, which may carry an extension.
        Assert.Equal("mp4", ReelbaseFileExtension.QueryValue(null, "George Michael - Freedom.mp4"));
        Assert.Equal(ReelbaseFileExtension.None, ReelbaseFileExtension.QueryValue(null, "George Michael - Freedom"));
    }

    [Fact]
    public void PrefersThePathOverTheName()
        => Assert.Equal("mkv", ReelbaseFileExtension.QueryValue("/media/x.mkv", "x.avi"));
}
