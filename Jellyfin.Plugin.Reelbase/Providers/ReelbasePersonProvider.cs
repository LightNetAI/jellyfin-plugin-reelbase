using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Reelbase.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Reelbase.Providers;

/// <summary>
/// Provides person metadata from Reelbase, such as the biography of a credited director.
/// </summary>
public class ReelbasePersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>
{
    private const int MaxSearchResults = 25;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReelbasePersonProvider> _logger;
    private readonly IReelbaseClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbasePersonProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ReelbasePersonProvider}"/> interface.</param>
    /// <param name="client">Instance of the <see cref="IReelbaseClient"/> interface.</param>
    public ReelbasePersonProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<ReelbasePersonProvider> logger,
        IReelbaseClient client)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _client = client;
    }

    /// <inheritdoc />
    public string Name => ReelbasePlugin.ProviderName;

    /// <inheritdoc />
    public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting person metadata for {Name}", info.Name);

        var result = new MetadataResult<Person> { HasMetadata = false };
        var slug = info.GetProviderId(ReelbasePlugin.ProviderName);

        if (string.IsNullOrEmpty(slug))
        {
            var people = await _client.SearchPeopleAsync(info.Name ?? string.Empty, cancellationToken).ConfigureAwait(false);
            slug = FindBestMatch(people, info.Name)?.Slug;
        }

        if (string.IsNullOrEmpty(slug))
        {
            return result;
        }

        var person = await _client.GetPersonAsync(slug, cancellationToken).ConfigureAwait(false);
        if (person is null)
        {
            return result;
        }

        result.HasMetadata = true;
        result.Item = new Person
        {
            Name = string.IsNullOrWhiteSpace(person.Name) ? info.Name : person.Name,
            Overview = string.IsNullOrWhiteSpace(person.Bio) ? null : person.Bio.Trim(),
            HomePageUrl = person.Website
        };

        result.Item.SetProviderId(ReelbasePlugin.ProviderName, slug);

        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting person search results for {Name}", searchInfo.Name);

        var people = await _client.SearchPeopleAsync(searchInfo.Name ?? string.Empty, cancellationToken).ConfigureAwait(false);

        return people
            .Select(person => new RemoteSearchResult
            {
                Name = person.Name,
                ImageUrl = person.ImageUrl,
                SearchProviderName = Name,
                ProviderIds = { [ReelbasePlugin.ProviderName] = person.Slug ?? string.Empty }
            })
            .Take(MaxSearchResults)
            .ToList();
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);

    private static ReelbasePerson? FindBestMatch(IReadOnlyList<ReelbasePerson> people, string? name)
    {
        var wanted = ReelbaseMatcher.Normalize(name);
        if (wanted.Length == 0)
        {
            return null;
        }

        foreach (var person in people)
        {
            if (string.Equals(ReelbaseMatcher.Normalize(person.Name), wanted, StringComparison.Ordinal))
            {
                return person;
            }
        }

        return null;
    }
}
