using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Reelbase.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Reelbase.Providers;

/// <summary>
/// Provides music artist metadata from Reelbase.
/// </summary>
public class ReelbaseMusicArtistProvider : IRemoteMetadataProvider<MusicArtist, ArtistInfo>
{
    private const int MaxSearchResults = 25;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReelbaseMusicArtistProvider> _logger;
    private readonly IReelbaseClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbaseMusicArtistProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ReelbaseMusicArtistProvider}"/> interface.</param>
    /// <param name="client">Instance of the <see cref="IReelbaseClient"/> interface.</param>
    public ReelbaseMusicArtistProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<ReelbaseMusicArtistProvider> logger,
        IReelbaseClient client)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _client = client;
    }

    /// <inheritdoc />
    public string Name => ReelbasePlugin.ProviderName;

    /// <inheritdoc />
    public async Task<MetadataResult<MusicArtist>> GetMetadata(ArtistInfo info, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting artist metadata for {Name}", info.Name);

        var result = new MetadataResult<MusicArtist> { HasMetadata = false };
        var slug = info.GetProviderId(ReelbasePlugin.ProviderName);

        if (string.IsNullOrEmpty(slug))
        {
            var artists = await _client.SearchArtistsAsync(info.Name ?? string.Empty, cancellationToken).ConfigureAwait(false);
            slug = FindBestMatch(artists, info.Name)?.Slug;
        }

        if (string.IsNullOrEmpty(slug))
        {
            return result;
        }

        var artist = await _client.GetArtistAsync(slug, cancellationToken).ConfigureAwait(false);
        if (artist is null)
        {
            return result;
        }

        result.HasMetadata = true;
        result.Item = new MusicArtist
        {
            Name = string.IsNullOrWhiteSpace(artist.Name) ? info.Name : artist.Name,
            Overview = string.IsNullOrWhiteSpace(artist.Bio) ? null : artist.Bio.Trim(),
            ProductionYear = artist.FormedYear,
            ProductionLocations = string.IsNullOrWhiteSpace(artist.Country) ? [] : [artist.Country.Trim()],
            HomePageUrl = artist.Url
        };

        result.Item.SetProviderId(ReelbasePlugin.ProviderName, slug);

        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(ArtistInfo searchInfo, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting artist search results for {Name}", searchInfo.Name);

        var artists = await _client.SearchArtistsAsync(searchInfo.Name ?? string.Empty, cancellationToken).ConfigureAwait(false);

        return artists
            .Select(artist => new RemoteSearchResult
            {
                Name = string.IsNullOrWhiteSpace(artist.Name) ? artist.Slug : artist.Name,
                ProductionYear = artist.FormedYear,
                SearchProviderName = Name,
                ImageUrl = null,
                ProviderIds = { [ReelbasePlugin.ProviderName] = artist.Slug ?? string.Empty }
            })
            .Take(MaxSearchResults)
            .ToList();
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);

    private static ReelbaseArtist? FindBestMatch(IReadOnlyList<ReelbaseArtist> artists, string? name)
    {
        var wanted = ReelbaseMatcher.Normalize(name);
        if (wanted.Length == 0)
        {
            return null;
        }

        ReelbaseArtist? best = null;

        foreach (var artist in artists)
        {
            var candidate = ReelbaseMatcher.Normalize(artist.Name);

            if (candidate.Length == 0)
            {
                continue;
            }

            if (string.Equals(candidate, wanted, StringComparison.Ordinal))
            {
                return artist;
            }

            if (best is null && candidate.Contains(wanted, StringComparison.Ordinal))
            {
                best = artist;
            }
        }

        return best;
    }
}
