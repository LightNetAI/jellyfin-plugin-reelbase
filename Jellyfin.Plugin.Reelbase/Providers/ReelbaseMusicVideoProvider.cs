using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Reelbase.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Reelbase.Providers;

/// <summary>
/// Provides music video metadata from Reelbase.
/// </summary>
public class ReelbaseMusicVideoProvider : IRemoteMetadataProvider<MusicVideo, MusicVideoInfo>
{
    /// <summary>
    /// How many candidates to offer in the identification dialog.
    /// </summary>
    private const int MaxSearchResults = 25;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReelbaseMusicVideoProvider> _logger;
    private readonly IReelbaseClient _client;
    private readonly ILibraryManager _libraryManager;
    private readonly IReelbaseExtensionCatalog _extensions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbaseMusicVideoProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ReelbaseMusicVideoProvider}"/> interface.</param>
    /// <param name="client">Instance of the <see cref="IReelbaseClient"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="extensions">Instance of the <see cref="IReelbaseExtensionCatalog"/> interface.</param>
    public ReelbaseMusicVideoProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<ReelbaseMusicVideoProvider> logger,
        IReelbaseClient client,
        ILibraryManager libraryManager,
        IReelbaseExtensionCatalog extensions)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _client = client;
        _libraryManager = libraryManager;
        _extensions = extensions;
    }

    /// <inheritdoc />
    public string Name => ReelbasePlugin.ProviderName;

    /// <inheritdoc />
    public async Task<MetadataResult<MusicVideo>> GetMetadata(MusicVideoInfo info, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting metadata for {Name}", info.Name);

        var result = new MetadataResult<MusicVideo> { HasMetadata = false };
        var slug = info.GetProviderId(ReelbasePlugin.ProviderName);

        if (string.IsNullOrEmpty(slug))
        {
            var match = await FindMatchAsync(info, cancellationToken).ConfigureAwait(false);
            slug = match?.Slug;
        }

        if (string.IsNullOrEmpty(slug))
        {
            return result;
        }

        // The detail endpoint is what carries the credits and the genres; the search result that
        // named the slug does not have them.
        var video = await _client.GetVideoAsync(slug, cancellationToken).ConfigureAwait(false);
        if (video is null)
        {
            return result;
        }

        result.HasMetadata = true;
        result.Item = ToMusicVideo(video);
        result.Item.SetProviderId(ReelbasePlugin.ProviderName, slug);

        AddPeople(result, video);

        // The artist only shows on the item once an artist item exists to link to, and a refresh
        // is not a scan, so it would otherwise stay a bare name until the next library scan.
        ReelbaseArtistLinks.Ensure(
            result.Item.Artists,
            name => _libraryManager.GetArtist(name),
            (name, ex) => _logger.LogWarning(ex, "Could not link the artist {Artist}", name));

        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MusicVideoInfo searchInfo, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting search results for {Name}", searchInfo.Name);

        var candidates = await CollectCandidatesAsync(searchInfo, cancellationToken).ConfigureAwait(false);

        return candidates
            .Select(video => (Video: video, Score: ReelbaseMatcher.Score(video, searchInfo.Name, searchInfo.Artists, searchInfo.Year)))
            .OrderByDescending(entry => entry.Score)
            .Select(entry => ToSearchResult(entry.Video))
            .Take(MaxSearchResults)
            .ToList();
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);

    /// <summary>
    /// Builds the Jellyfin item from a Reelbase record.
    /// </summary>
    /// <param name="video">The record.</param>
    /// <returns>The item.</returns>
    internal static MusicVideo ToMusicVideo(ReelbaseVideo video)
    {
        var item = new MusicVideo
        {
            Name = string.IsNullOrWhiteSpace(video.Title) ? "Unknown" : video.Title,
            ProductionYear = video.Year,
            Overview = string.IsNullOrWhiteSpace(video.Description) ? null : video.Description.Trim(),
            PremiereDate = ParseReleaseDate(video.ReleaseDate),
            Album = video.AlbumName,
            RunTimeTicks = video.DurationSeconds is > 0 ? TimeSpan.FromSeconds(video.DurationSeconds.Value).Ticks : null,
            Artists = GetArtists(video),
            Genres = video.Genres is null
                ? []
                : video.Genres
                    .Select(genre => genre.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
        };

        var studio = FirstNonEmpty(video.Label, video.ProductionCompany);
        if (studio is not null)
        {
            item.Studios = [studio];
        }

        return item;
    }

    private static IReadOnlyList<string> GetArtists(ReelbaseVideo video)
        => string.IsNullOrWhiteSpace(video.Artist?.Name) ? [] : [video.Artist.Name];

    private static DateTime? ParseReleaseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var date)
            ? date
            : null;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    /// <summary>
    /// Adds the credits, and the director the record names separately, as people.
    /// </summary>
    private static void AddPeople(MetadataResult<MusicVideo> result, ReelbaseVideo video)
    {
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hadDirector = false;

        foreach (var credit in video.Credits ?? [])
        {
            var person = credit.Person;
            if (string.IsNullOrWhiteSpace(person?.Name))
            {
                continue;
            }

            var role = string.IsNullOrWhiteSpace(credit.Role) ? null : credit.Role.Trim();
            if (!added.Add(person.Name + "|" + role))
            {
                continue;
            }

            hadDirector |= ReelbaseCreditMapper.IsDirector(role);
            result.AddPerson(ToPersonInfo(person.Name, role, person.Slug));
        }

        // A record names its director twice: once as a field, once as a credit. The field is only
        // used when the credit list did not produce one, so the person is not added twice.
        if (!hadDirector && !string.IsNullOrWhiteSpace(video.Director?.Name))
        {
            result.AddPerson(ToPersonInfo(video.Director.Name, "Director", video.Director.Slug));
        }
    }

    private static PersonInfo ToPersonInfo(string name, string? role, string? slug)
    {
        var person = new PersonInfo
        {
            Name = name.Trim(),
            Role = role,
            Type = ReelbaseCreditMapper.MapRole(role)
        };

        if (!string.IsNullOrWhiteSpace(slug))
        {
            person.ProviderIds[ReelbasePlugin.ProviderName] = slug.Trim();
        }

        return person;
    }

    /// <summary>
    /// Builds a search result for the identification dialog.
    /// </summary>
    /// <remarks>
    /// The dialog lists a result as its name, the provider, <c>AlbumArtist</c> and the year; it
    /// never reads <c>Artists</c>. A music video's artist is therefore put in both properties:
    /// <c>Artists</c> because that is the honest place for it, and <c>AlbumArtist</c> because
    /// that is the one the dialog draws. Without the second, the artist is missing from the list
    /// even though the API sent it.
    /// </remarks>
    /// <param name="video">The record.</param>
    /// <returns>The search result.</returns>
    internal static RemoteSearchResult ToSearchResult(ReelbaseVideo video)
    {
        var artist = string.IsNullOrWhiteSpace(video.Artist?.Name) ? null : video.Artist!.Name!;

        var result = new RemoteSearchResult
        {
            Name = string.IsNullOrWhiteSpace(video.Title) ? "Unknown" : video.Title,
            ProductionYear = video.Year,
            Overview = video.Description,
            ImageUrl = video.ArtworkUrl,
            SearchProviderName = ReelbasePlugin.ProviderName,
            Artists = artist is null ? [] : [new RemoteSearchResult { Name = artist }],
            AlbumArtist = artist is null ? null : new RemoteSearchResult { Name = artist }
        };

        if (!string.IsNullOrWhiteSpace(video.Slug))
        {
            result.SetProviderId(ReelbasePlugin.ProviderName, video.Slug);
        }

        return result;
    }

    /// <summary>
    /// Walks the queries the name suggests, stopping at the first that produces a match.
    /// </summary>
    private async Task<ReelbaseVideo?> FindMatchAsync(MusicVideoInfo info, CancellationToken cancellationToken)
    {
        var extension = ReelbaseFileExtension.QueryValue(info.Path, info.Name);
        if (!_extensions.IsAllowed(extension))
        {
            return null;
        }

        foreach (var query in ReelbaseSearchQueries.ForMusicVideo(info.Name))
        {
            var videos = await _client.SearchVideosAsync(query, extension, cancellationToken).ConfigureAwait(false);
            var match = ReelbaseMatcher.FindBestMatch(videos, info.Name, info.Artists, info.Year);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Collects the candidates the identification dialog offers: the title search first, widened
    /// to the artist when it found little, which keeps a library scan to one request per item.
    /// </summary>
    private async Task<IReadOnlyList<ReelbaseVideo>> CollectCandidatesAsync(MusicVideoInfo info, CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<ReelbaseVideo>();
        var queries = ReelbaseSearchQueries.ForMusicVideo(info.Name);
        var extension = ReelbaseFileExtension.QueryValue(info.Path, info.Name);
        if (!_extensions.IsAllowed(extension))
        {
            _logger.LogDebug(
                "{Name} is a .{Extension} file, which the approved extension list does not accept; no request is sent.",
                info.Name,
                extension);

            return Array.Empty<ReelbaseVideo>();
        }

        foreach (var query in queries)
        {
            foreach (var video in await _client.SearchVideosAsync(query, extension, cancellationToken).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(video.Slug) && seen.Add(video.Slug))
                {
                    candidates.Add(video);
                }
            }

            if (candidates.Count >= MaxSearchResults / 2)
            {
                break;
            }
        }

        return candidates;
    }
}
