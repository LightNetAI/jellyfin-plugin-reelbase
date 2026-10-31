using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Reelbase.Providers;

/// <summary>
/// Links an item to its page on the Reelbase site.
/// </summary>
public class ReelbaseExternalUrlProvider : IExternalUrlProvider
{
    private readonly IReelbaseSettingsProvider _settingsProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbaseExternalUrlProvider"/> class.
    /// </summary>
    /// <param name="settingsProvider">Instance of the <see cref="IReelbaseSettingsProvider"/> interface.</param>
    public ReelbaseExternalUrlProvider(IReelbaseSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    /// <inheritdoc />
    public string Name => ReelbasePlugin.ProviderName;

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var section = item switch
        {
            MusicVideo => "video",
            MusicArtist => "artist",
            Person => "person",
            _ => null
        };

        if (section is null || !item.TryGetProviderId(ReelbasePlugin.ProviderName, out var slug))
        {
            yield break;
        }

        if (ReelbaseUrls.GetPageUrl(_settingsProvider.Current.ServerUrl, section, slug) is { } pageUrl)
        {
            yield return pageUrl;
        }
    }
}
