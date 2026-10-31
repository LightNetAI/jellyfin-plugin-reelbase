namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Reads the settings from the plugin configuration.
/// </summary>
internal sealed class PluginSettingsProvider : IReelbaseSettingsProvider
{
    /// <inheritdoc />
    public ReelbaseSettings Current
        => ReelbaseSettings.FromConfiguration(ReelbasePlugin.Instance?.Configuration);
}
