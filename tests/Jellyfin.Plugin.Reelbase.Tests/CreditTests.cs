using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Reelbase;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// Credit roles come from imported catalogues and are free form, so the mapping has to be
/// forgiving and still keep the role text.
/// </summary>
public class CreditTests
{
    [Theory]
    [InlineData("Director", PersonKind.Director)]
    [InlineData("director", PersonKind.Director)]
    [InlineData("Executive Producer", PersonKind.Producer)]
    [InlineData("Edit", PersonKind.Editor)]
    [InlineData("Director of Photography", PersonKind.Unknown)]
    [InlineData("Choreographer", PersonKind.Unknown)]
    [InlineData(null, PersonKind.Unknown)]
    public void MapRole_MapsTheRolesItKnows(string? role, PersonKind expected)
        => Assert.Equal(expected, ReelbaseCreditMapper.MapRole(role));

    [Fact]
    public void IsDirector_RecognisesDirectorRoles()
    {
        Assert.True(ReelbaseCreditMapper.IsDirector("Director"));
        Assert.False(ReelbaseCreditMapper.IsDirector("Producer"));
    }
}
