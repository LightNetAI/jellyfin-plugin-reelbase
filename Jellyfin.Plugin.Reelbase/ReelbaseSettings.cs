using Jellyfin.Plugin.Reelbase.Configuration;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// The configuration values a request needs, read once per call so that saving the configuration
/// page takes effect without a restart.
/// </summary>
public sealed class ReelbaseSettings
{
    /// <summary>
    /// Gets the Reelbase API key.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the address of the Reelbase instance, without a trailing slash.
    /// </summary>
    public string ServerUrl { get; init; } = ReelbasePlugin.DefaultServerUrl;

    /// <summary>
    /// Gets how many hours a response may be reused for.
    /// </summary>
    public int CacheDurationHours { get; init; } = ReelbaseClient.DefaultCacheDurationHours;

    /// <summary>
    /// Builds the settings from a stored configuration, falling back to the defaults for values
    /// that are missing or out of range.
    /// </summary>
    /// <param name="configuration">The stored configuration, if the plugin is loaded.</param>
    /// <returns>The settings to use.</returns>
    public static ReelbaseSettings FromConfiguration(PluginConfiguration? configuration)
    {
        if (configuration is null)
        {
            return new ReelbaseSettings();
        }

        var cacheHours = configuration.CacheDurationHours;

        return new ReelbaseSettings
        {
            ApiKey = string.IsNullOrWhiteSpace(configuration.ApiKey) ? null : configuration.ApiKey.Trim(),
            ServerUrl = ReelbaseUrls.NormalizeServerUrl(configuration.ServerUrl),
            CacheDurationHours = cacheHours > 0 ? cacheHours : ReelbaseClient.DefaultCacheDurationHours
        };
    }
}
