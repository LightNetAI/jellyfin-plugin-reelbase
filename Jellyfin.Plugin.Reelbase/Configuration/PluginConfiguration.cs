using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Reelbase.Configuration;

/// <summary>
/// The plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        ServerUrl = ReelbasePlugin.DefaultServerUrl;
        CacheDurationHours = ReelbaseClient.DefaultCacheDurationHours;
    }

    /// <summary>
    /// Gets or sets the Reelbase API key. Every request carries it as a bearer token, and
    /// without one Reelbase rejects the request, so no lookup is attempted until it is set.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the address of the Reelbase instance to read from, such as
    /// <c>https://api.reelbase.me</c>. Reelbase can be self-hosted, so the address is
    /// configurable rather than fixed.
    /// </summary>
    public string ServerUrl { get; set; }

    /// <summary>
    /// Gets or sets how many hours an API response is reused before it is fetched again. The
    /// daily request allowance is small enough that caching is not optional; zero or less falls
    /// back to <see cref="ReelbaseClient.DefaultCacheDurationHours"/>.
    /// </summary>
    public int CacheDurationHours { get; set; }
}
