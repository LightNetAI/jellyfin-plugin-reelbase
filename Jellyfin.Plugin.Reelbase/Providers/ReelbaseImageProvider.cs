using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Reelbase.Providers;

/// <summary>
/// Provides artwork from Reelbase.
/// </summary>
public class ReelbaseImageProvider : IRemoteImageProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IReelbaseClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbaseImageProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="client">Instance of the <see cref="IReelbaseClient"/> interface.</param>
    public ReelbaseImageProvider(
        IHttpClientFactory httpClientFactory,
        IReelbaseClient client)
    {
        _httpClientFactory = httpClientFactory;
        _client = client;
    }

    /// <inheritdoc />
    public string Name => ReelbasePlugin.ProviderName;

    /// <inheritdoc />
    public bool Supports(BaseItem item)
        => item is MusicVideo || item is Person;

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        yield return ImageType.Primary;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.TryGetProviderId(ReelbasePlugin.ProviderName, out var slug) || string.IsNullOrEmpty(slug))
        {
            return Enumerable.Empty<RemoteImageInfo>();
        }

        var url = item switch
        {
            MusicVideo => (await _client.GetVideoAsync(slug, cancellationToken).ConfigureAwait(false))?.ArtworkUrl,
            Person => (await _client.GetPersonAsync(slug, cancellationToken).ConfigureAwait(false))?.ImageUrl,
            _ => null
        };

        if (string.IsNullOrEmpty(url))
        {
            return Enumerable.Empty<RemoteImageInfo>();
        }

        return new[]
        {
            new RemoteImageInfo
            {
                ProviderName = ReelbasePlugin.ProviderName,
                Url = url,
                Type = ImageType.Primary
            }
        };
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
}
