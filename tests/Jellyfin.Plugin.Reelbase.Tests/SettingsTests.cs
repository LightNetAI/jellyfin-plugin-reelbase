using Jellyfin.Plugin.Reelbase.Configuration;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// The configuration is stored as entered, so the settings have to repair what a user types.
/// </summary>
public class SettingsTests
{
    [Fact]
    public void FromConfiguration_FallsBackToTheDefaults()
    {
        var settings = ReelbaseSettings.FromConfiguration(null);

        Assert.Null(settings.ApiKey);
        Assert.Equal("https://api.reelbase.me", settings.ServerUrl);
        Assert.Equal(24, settings.CacheDurationHours);
    }

    [Fact]
    public void FromConfiguration_RepairsOutOfRangeValues()
    {
        var settings = ReelbaseSettings.FromConfiguration(new PluginConfiguration
        {
            ApiKey = "  rb_key  ",
            ServerUrl = "reelbase.example.com/",
            CacheDurationHours = -1
        });

        Assert.Equal("rb_key", settings.ApiKey);
        Assert.Equal("https://reelbase.example.com", settings.ServerUrl);
        Assert.Equal(24, settings.CacheDurationHours);
    }

}
