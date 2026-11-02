using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Reelbase.Tasks;

/// <summary>
/// Refreshes the music video library within whatever allowance the API reports.
/// </summary>
/// <remarks>
/// A library larger than the daily allowance cannot be refreshed in one go, so this task works
/// through the items least recently refreshed first and stops when the allowance is spent.
/// Because a refresh stamps <see cref="BaseItem.DateLastRefreshed"/>, the next run picks up
/// where this one stopped, and a library of any size is covered over consecutive nights.
/// </remarks>
public class RefreshReelbaseTask : IScheduledTask
{
    /// <summary>
    /// How often the task runs by default.
    /// </summary>
    private static readonly TimeSpan _interval = TimeSpan.FromHours(24);

    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IReelbaseRateLimiter _rateLimiter;
    private readonly IReelbaseSettingsProvider _settingsProvider;
    private readonly IDirectoryService _directoryService;
    private readonly IReelbaseExtensionCatalog _extensions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshReelbaseTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshReelbaseTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
    /// <param name="rateLimiter">Instance of the <see cref="IReelbaseRateLimiter"/> interface.</param>
    /// <param name="settingsProvider">Instance of the <see cref="IReelbaseSettingsProvider"/> interface.</param>
    /// <param name="directoryService">Instance of the <see cref="IDirectoryService"/> interface.</param>
    /// <param name="extensions">Instance of the <see cref="IReelbaseExtensionCatalog"/> interface.</param>
    /// <param name="scopeFactory">Instance of the <see cref="IServiceScopeFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{RefreshReelbaseTask}"/> interface.</param>
    public RefreshReelbaseTask(
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        IReelbaseRateLimiter rateLimiter,
        IReelbaseSettingsProvider settingsProvider,
        IDirectoryService directoryService,
        IReelbaseExtensionCatalog extensions,
        IServiceScopeFactory scopeFactory,
        ILogger<RefreshReelbaseTask> logger)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _rateLimiter = rateLimiter;
        _settingsProvider = settingsProvider;
        _directoryService = directoryService;
        _extensions = extensions;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Refresh music video metadata from Reelbase";

    /// <inheritdoc />
    public string Key => "ReelbaseRefreshMusicVideos";

    /// <inheritdoc />
    public string Description =>
        "Fills in the metadata and artwork of the music video library from Reelbase, " +
        "stopping when the account's daily allowance is spent and continuing on the next run.";

    /// <inheritdoc />
    public string Category => "Reelbase";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = _interval.Ticks
            }
        ];
    }

    /// <summary>
    /// Asks for the extension list version, and downloads the list when it has moved on.
    /// </summary>
    /// <remarks>
    /// A list that has changed decides which files are looked up for the rest of the run, so this
    /// happens before the first item is refreshed. It costs one request, and none at all when the
    /// version is the one already saved.
    /// </remarks>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the list is as fresh as it can be.</returns>
    private async Task RefreshExtensionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IReelbaseClient>();

            await _extensions.RefreshAsync(client, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The refresh itself is worth attempting even without the list.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Could not check the approved extension list before the refresh.");
        }
    }

    /// <summary>
    /// Builds the options a library-wide refresh needs.
    /// </summary>
    /// <remarks>
    /// <see cref="MetadataRefreshMode.FullRefresh"/> is not an optimisation here, it is the only
    /// mode that reaches a provider: with <c>Default</c> the server runs a provider only when the
    /// item is new, its file changed, or the library's automatic refresh interval has passed, and
    /// otherwise asks nobody - which is how a whole library gets "refreshed" in a second without
    /// a single lookup. Artwork is still only downloaded for items that have none, because an
    /// existing image is left alone unless <c>ReplaceAllImages</c> is set.
    /// </remarks>
    /// <param name="directoryService">The directory service the refresh options need.</param>
    /// <returns>The options to refresh with.</returns>
    internal static MetadataRefreshOptions CreateRefreshOptions(IDirectoryService directoryService)
        => new(directoryService)
        {
            MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
            ImageRefreshMode = MetadataRefreshMode.FullRefresh,
            IsAutomated = true,

            // The provider's answer wins, the way it does during a library scan. Only the fields
            // the provider filled are replaced: the merge carries the item's own values into
            // whatever the provider left empty, so nothing that has no replacement is lost. With
            // this false the item keeps the name it was scanned with - the file name.
            ReplaceAllMetadata = true
        };

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var settings = _settingsProvider.Current;
        if (string.IsNullOrEmpty(settings.ApiKey))
        {
            _logger.LogInformation("No Reelbase API key is configured, so the library refresh is skipped.");
            return;
        }

        // One fresh report for the run; the limiter counts the requests itself from there.
        _rateLimiter.BeginRun();

        await RefreshExtensionsAsync(cancellationToken).ConfigureAwait(false);

        // A pause can outlive the process that was told about it, so the run starts by honouring it.
        if (_rateLimiter.PausedUntil is { } pausedUntil && pausedUntil > DateTimeOffset.UtcNow)
        {
            _logger.LogInformation(
                "Reelbase asked for a pause until {PausedUntil:u}, so the refresh waits for the next run.",
                pausedUntil);
            return;
        }

        var items = ReelbaseRefreshOrder.OldestFirst(
            _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.MusicVideo],
                Recursive = true
            }),
            item => item.DateLastRefreshed);

        if (items.Count == 0)
        {
            _logger.LogInformation("There are no music videos to refresh.");
            return;
        }

        var options = CreateRefreshOptions(_directoryService);

        var refreshed = 0;
        progress?.Report(0);

        for (var index = 0; index < items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_rateLimiter.HasAllowance
                || (_rateLimiter.PausedUntil is { } pauseUntil && pauseUntil > DateTimeOffset.UtcNow))
            {
                break;
            }

            try
            {
                await _providerManager.RefreshSingleItem(items[index], options, cancellationToken).ConfigureAwait(false);
                refreshed++;
            }
#pragma warning disable CA1031 // One unreadable item must not cost the rest of the run.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "Refreshing {Name} failed.", items[index].Name);
            }

            progress?.Report(100.0 * (index + 1) / items.Count);

            if (_rateLimiter.PausedUntil is { } pauseAfterItem && pauseAfterItem > DateTimeOffset.UtcNow)
            {
                _logger.LogWarning(
                    "Reelbase asked for a pause until {PausedUntil:u}, so the refresh stopped after {Refreshed} of {Total} music videos.",
                    pauseAfterItem,
                    refreshed,
                    items.Count);
                return;
            }

            if (!ReelbaseAvailability.IsAvailable)
            {
                _logger.LogWarning(
                    "Reelbase stopped answering, so the refresh gave up after {Refreshed} of {Total} music videos; the rest is left for the next run.",
                    refreshed,
                    items.Count);
                return;
            }
        }

        if (refreshed < items.Count)
        {
            _logger.LogInformation(
                "Refreshed {Refreshed} of {Total} music videos before the Reelbase allowance ran out ({Allowance}, with {Reserved} held back for the rest of the account). The remainder continues on the next run, after {Reset:u}.",
                refreshed,
                items.Count,
                DescribeAllowance(),
                _rateLimiter.Reserved,
                _rateLimiter.ResetsAt);
        }
        else
        {
            _logger.LogInformation("Refreshed {Total} music videos from Reelbase.", items.Count);
        }
    }

    private string DescribeAllowance()
    {
        return _rateLimiter.Remaining is { } remaining
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{remaining} requests left")
            : "the API reported none left";
    }
}
