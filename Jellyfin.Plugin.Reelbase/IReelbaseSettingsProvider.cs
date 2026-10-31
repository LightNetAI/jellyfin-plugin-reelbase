namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Supplies the settings a request should use.
/// </summary>
public interface IReelbaseSettingsProvider
{
    /// <summary>
    /// Gets the settings to use for the next request.
    /// </summary>
    ReelbaseSettings Current { get; }
}
