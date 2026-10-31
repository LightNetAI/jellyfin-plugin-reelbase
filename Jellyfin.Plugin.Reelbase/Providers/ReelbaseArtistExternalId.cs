using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Reelbase.Providers;

/// <summary>
/// External id for a Reelbase artist.
/// </summary>
public class ReelbaseArtistExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => ReelbasePlugin.ProviderName;

    /// <inheritdoc />
    public string Key => ReelbasePlugin.ProviderName;

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Artist;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is MusicArtist;
}
