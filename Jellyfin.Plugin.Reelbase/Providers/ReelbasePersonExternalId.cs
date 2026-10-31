using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Reelbase.Providers;

/// <summary>
/// External id for a Reelbase person, such as a credited director.
/// </summary>
public class ReelbasePersonExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => ReelbasePlugin.ProviderName;

    /// <inheritdoc />
    public string Key => ReelbasePlugin.ProviderName;

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Person;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Person;
}
