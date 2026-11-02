using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.Reelbase.Models;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// The Reelbase client.
/// </summary>
/// <remarks>
/// Every response is cached for the configured number of hours, and every request is paid for
/// out of the daily allowance, because the allowance is small enough that a scan which asks
/// twice for the same record is a scan that runs out.
/// </remarks>
public class ReelbaseClient : IReelbaseClient
{
    /// <summary>
    /// How long a response is reused for when the configuration does not say.
    /// </summary>
    internal const int DefaultCacheDurationHours = 24;

    /// <summary>
    /// The path every endpoint lives under.
    /// </summary>
    internal const string ApiPath = "/api/v1";

    /// <summary>
    /// How many results to ask a search for. The API allows up to 100, but a music video title
    /// is specific enough that the first page holds the match, and a smaller page keeps both the
    /// response and the cached copy small.
    /// </summary>
    private const int SearchPerPage = 25;

    /// <summary>
    /// Prefixes the keys this plugin puts in the shared memory cache.
    /// </summary>
    private const string CacheKeyPrefix = "Jellyfin.Plugin.Reelbase:";

    /// <summary>
    /// A response that failed outright is worth retrying sooner than a good one is worth
    /// refetching, but not immediately: a dead slug would otherwise be asked for on every scan.
    /// </summary>
    private static readonly TimeSpan _notFoundCacheDuration = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions _jsonSerializerOptions = JsonDefaults.Options;

    /// <summary>
    /// Whether the missing API key has already been reported. Client instances are short lived,
    /// so this outlives them.
    /// </summary>
    private static int _missingApiKeyLogged;

    /// <summary>
    /// Whether a rejected API key has already been reported.
    /// </summary>
    private static int _rejectedApiKeyLogged;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ReelbaseClient> _logger;
    private readonly IReelbaseSettingsProvider _settingsProvider;
    private readonly IReelbaseRateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbaseClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ReelbaseClient}"/> interface.</param>
    /// <param name="settingsProvider">Instance of the <see cref="IReelbaseSettingsProvider"/> interface.</param>
    /// <param name="rateLimiter">Instance of the <see cref="IReelbaseRateLimiter"/> interface.</param>
    public ReelbaseClient(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<ReelbaseClient> logger,
        IReelbaseSettingsProvider settingsProvider,
        IReelbaseRateLimiter rateLimiter)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _logger = logger;
        _settingsProvider = settingsProvider;
        _rateLimiter = rateLimiter;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReelbaseVideo>> SearchVideosAsync(string query, string extension, CancellationToken cancellationToken)
    {
        var settings = _settingsProvider.Current;
        var response = await GetResponseAsync<ReelbaseListResponse<ReelbaseVideo>>(
                BuildVideoSearchPath(query, extension),
                settings,
                cancellationToken)
            .ConfigureAwait(false);

        var videos = response?.Data ?? Array.Empty<ReelbaseVideo>();

        foreach (var video in videos)
        {
            ReelbaseModelNormalizer.Normalize(video, settings.ServerUrl);
        }

        return videos;
    }

    /// <inheritdoc />
    public async Task<ReelbaseVideo?> GetVideoAsync(string slug, CancellationToken cancellationToken)
    {
        var settings = _settingsProvider.Current;
        var response = await GetResponseAsync<ReelbaseItemResponse<ReelbaseVideo>>(
                $"{ApiPath}/videos/{Uri.EscapeDataString(slug)}",
                settings,
                cancellationToken)
            .ConfigureAwait(false);

        if (response?.Data is not { } video)
        {
            return null;
        }

        ReelbaseModelNormalizer.Normalize(video, settings.ServerUrl);

        return video;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReelbaseArtist>> SearchArtistsAsync(string query, CancellationToken cancellationToken)
    {
        var settings = _settingsProvider.Current;
        var response = await GetResponseAsync<ReelbaseListResponse<ReelbaseArtist>>(
                BuildSearchPath("/artists", query),
                settings,
                cancellationToken)
            .ConfigureAwait(false);

        var artists = response?.Data ?? Array.Empty<ReelbaseArtist>();

        foreach (var artist in artists)
        {
            ReelbaseModelNormalizer.Normalize(artist, settings.ServerUrl);
        }

        return artists;
    }

    /// <inheritdoc />
    public async Task<ReelbaseArtist?> GetArtistAsync(string slug, CancellationToken cancellationToken)
    {
        var settings = _settingsProvider.Current;
        var response = await GetResponseAsync<ReelbaseItemResponse<ReelbaseArtist>>(
                $"{ApiPath}/artists/{Uri.EscapeDataString(slug)}",
                settings,
                cancellationToken)
            .ConfigureAwait(false);

        if (response?.Data is not { } artist)
        {
            return null;
        }

        ReelbaseModelNormalizer.Normalize(artist, settings.ServerUrl);

        return artist;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReelbasePerson>> SearchPeopleAsync(string query, CancellationToken cancellationToken)
    {
        var settings = _settingsProvider.Current;
        var response = await GetResponseAsync<ReelbaseListResponse<ReelbasePerson>>(
                BuildSearchPath("/people", query),
                settings,
                cancellationToken)
            .ConfigureAwait(false);

        var people = response?.Data ?? Array.Empty<ReelbasePerson>();

        foreach (var person in people)
        {
            ReelbaseModelNormalizer.Normalize(person, settings.ServerUrl);
        }

        return people;
    }

    /// <inheritdoc />
    public async Task<ReelbasePerson?> GetPersonAsync(string slug, CancellationToken cancellationToken)
    {
        var settings = _settingsProvider.Current;
        var response = await GetResponseAsync<ReelbaseItemResponse<ReelbasePerson>>(
                $"{ApiPath}/people/{Uri.EscapeDataString(slug)}",
                settings,
                cancellationToken)
            .ConfigureAwait(false);

        if (response?.Data is not { } person)
        {
            return null;
        }

        ReelbaseModelNormalizer.Normalize(person, settings.ServerUrl);

        return person;
    }

    /// <summary>
    /// Builds the video search path, which carries the extension the item was found with.
    /// </summary>
    /// <remarks>
    /// A folder or an extensionless name is sent as <c>ext=null</c>: the API still searches, but a
    /// miss is not recorded as missing content, because there is no file to add.
    /// </remarks>
    /// <param name="query">The text to search for.</param>
    /// <param name="extension">The extension, or <see cref="ReelbaseFileExtension.None"/>.</param>
    /// <returns>The path to request.</returns>
    private static string BuildVideoSearchPath(string query, string extension)
        => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{ApiPath}/videos?q={Uri.EscapeDataString(query ?? string.Empty)}&per_page={SearchPerPage}&ext={Uri.EscapeDataString(extension ?? ReelbaseFileExtension.None)}");

    private static string BuildSearchPath(string resource, string query)
        => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{ApiPath}{resource}?q={Uri.EscapeDataString(query ?? string.Empty)}&per_page={SearchPerPage}");

    private async Task<T?> GetResponseAsync<T>(string path, ReelbaseSettings settings, CancellationToken cancellationToken)
        where T : class
    {
        // Without a key there is nothing this plugin can do: Reelbase rejects every request that
        // carries no bearer token, so the lookup is skipped rather than sent.
        if (string.IsNullOrEmpty(settings.ApiKey))
        {
            LogMissingApiKeyOnce();
            return default;
        }

        var cacheKey = $"{CacheKeyPrefix}{settings.ServerUrl}{path}";
        if (_memoryCache.TryGetValue(cacheKey, out T? cached))
        {
            _logger.LogDebug("Serving {Path} from the cache.", path);
            return cached;
        }

        var (result, cacheDuration) = await FetchAsync<T>(path, settings, cancellationToken).ConfigureAwait(false);

        if (cacheDuration > TimeSpan.Zero)
        {
            _memoryCache.Set(cacheKey, result, cacheDuration);
        }

        return result;
    }

    /// <summary>
    /// Sends the request, paying for it out of the daily allowance and retrying once if the API
    /// rate limits anyway.
    /// </summary>
    /// <returns>
    /// The deserialized response, and how long it may be cached for - <see cref="TimeSpan.Zero"/>
    /// when it must not be.
    /// </returns>
    private async Task<(T? Result, TimeSpan CacheDuration)> FetchAsync<T>(string path, ReelbaseSettings settings, CancellationToken cancellationToken)
        where T : class
    {
        var rateLimitRetries = 0;

        while (true)
        {
            // An API that is already failing does not need one request per library item.
            if (!ReelbaseAvailability.IsAvailable)
            {
                _logger.LogDebug("Reelbase is not answering, so {Path} is left for the next run.", path);
                return (default, TimeSpan.Zero);
            }

            if (!await _rateLimiter.TryAcquireAsync(cancellationToken).ConfigureAwait(false))
            {
                return (default, TimeSpan.Zero);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, settings.ServerUrl + path);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + settings.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

            HttpResponseMessage response;
            try
            {
                response = await _httpClientFactory.CreateClient(NamedClient.Default)
                    .SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReportFailure(ex);
                return (default, TimeSpan.Zero);
            }

            using (response)
            {
                _rateLimiter.Observe(response);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // The API is answering, it just wants the plugin to wait.
                    ReelbaseAvailability.RecordSuccess();

                    var retryDelay = _rateLimiter.ObserveRateLimited(response);

                    // The limiter now holds every caller for that delay: a short Retry-After is
                    // waited out in TryAcquireAsync, and a long one ends the attempt instead of
                    // parking a scan until the window resets. One retry tells the two apart.
                    if (rateLimitRetries++ >= 1)
                    {
                        return (default, TimeSpan.Zero);
                    }

                    _logger.LogDebug("Reelbase asked for {Delay}; asking for {Path} once more.", retryDelay, path);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    ReelbaseAvailability.RecordSuccess();
                    _logger.LogDebug("Reelbase has nothing at {Path}.", path);

                    // Remember the miss for a while so a rescan does not ask again immediately.
                    return (default, _notFoundCacheDuration);
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    // A refused key fails every request the same way, so the breaker counts it.
                    ReportFailure(null);
                    LogRejectedApiKeyOnce(await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false));
                    return (default, TimeSpan.Zero);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "The Reelbase request for {Path} failed with HTTP {StatusCode}: {Error}",
                        path,
                        (int)response.StatusCode,
                        await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false));

                    ReportFailure(null);
                    return (default, TimeSpan.Zero);
                }

                ReelbaseAvailability.RecordSuccess();

                try
                {
                    var result = await response.Content
                        .ReadFromJsonAsync<T>(_jsonSerializerOptions, cancellationToken)
                        .ConfigureAwait(false);

                    // An empty page is a real answer, but not a lasting one: the catalogue grows,
                    // and a miss remembered for a day is a lookup that never reaches the API
                    // again - which looks exactly like the plugin not sending anything.
                    return (result, HasPayload(result) ? GetCacheDuration(settings) : TimeSpan.Zero);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize the Reelbase response for {Path}.", path);
                    return (default, TimeSpan.Zero);
                }
            }
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            // Errors come back as {"error":{"status":401,"message":"..."}}.
            var error = await response.Content
                .ReadFromJsonAsync<ReelbaseErrorResponse>(_jsonSerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrEmpty(error?.Error?.Message))
            {
                return error.Error.Message;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or HttpRequestException)
        {
            // Not every failure is answered with the documented error object; a proxy in front
            // of the API can serve plain HTML for some of them.
        }

        return response.ReasonPhrase ?? string.Empty;
    }

    /// <summary>
    /// Whether a response carries something worth remembering.
    /// </summary>
    /// <param name="result">The deserialized response.</param>
    /// <returns><c>true</c> when the response holds a record or a page of them.</returns>
    private static bool HasPayload<T>(T? result)
        where T : class
        => result switch
        {
            ReelbaseListResponse<ReelbaseVideo> list => list.Data.Count > 0,
            ReelbaseListResponse<ReelbaseArtist> list => list.Data.Count > 0,
            ReelbaseItemResponse<ReelbaseVideo> item => item.Data is not null,
            ReelbaseItemResponse<ReelbaseArtist> item => item.Data is not null,
            ReelbaseItemResponse<ReelbasePerson> item => item.Data is not null,
            _ => result is not null
        };

    /// <summary>
    /// How long a good response may be reused for.
    /// </summary>
    private static TimeSpan GetCacheDuration(ReelbaseSettings settings)
        => TimeSpan.FromHours(settings.CacheDurationHours > 0 ? settings.CacheDurationHours : DefaultCacheDurationHours);

    private void ReportFailure(Exception? exception)
    {
        if (!ReelbaseAvailability.RecordFailure())
        {
            return;
        }

        if (exception is null)
        {
            _logger.LogWarning(
                "The Reelbase API is not answering usefully, so lookups pause for {CoolDown}. The rest of the library is left for the next run.",
                ReelbaseAvailability.CoolDown);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "The Reelbase API is not answering, so lookups pause for {CoolDown}. The rest of the library is left for the next run.",
                ReelbaseAvailability.CoolDown);
        }
    }

    private void LogMissingApiKeyOnce()
    {
        if (Interlocked.Exchange(ref _missingApiKeyLogged, 1) == 0)
        {
            _logger.LogError(
                "No Reelbase API key is configured, so no metadata is looked up. Create a key under Account -> API keys on your Reelbase instance and enter it on the Reelbase plugin's configuration page.");
        }
    }

    private void LogRejectedApiKeyOnce(string error)
    {
        if (Interlocked.Exchange(ref _rejectedApiKeyLogged, 1) == 0)
        {
            _logger.LogError(
                "The Reelbase API rejected the configured API key ({Error}). Check that the key is still enabled and that the account's email address is confirmed.",
                error);
        }
    }
}
