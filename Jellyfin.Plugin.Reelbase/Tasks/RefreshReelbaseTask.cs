using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Tasks;
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
    private readonly ILogger<RefreshReelbaseTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshReelbaseTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
    /// <param name="rateLimiter">Instance of the <see cref="IReelbaseRateLimiter"/> interface.</param>
    /// <param name="settingsProvider">Instance of the <see cref="IReelbaseSettingsProvider"/> interface.</param>
    /// <param name="directoryService">Instance of the <see cref="IDirectoryService"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{RefreshReelbaseTask}"/> interface.</param>
    public RefreshReelbaseTask(
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        IReelbaseRateLimiter rateLimiter,
        IReelbaseSettingsProvider settingsProvider,
        IDirectoryService directoryService,
        ILogger<RefreshReelbaseTask> logger)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _rateLimiter = rateLimiter;
        _settingsProvider = settingsProvider;
        _directoryService = directoryService;
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

        var options = new MetadataRefreshOptions(_directoryService)
        {
            MetadataRefreshMode = MetadataRefreshMode.Default,
            ImageRefreshMode = MetadataRefreshMode.Default,
            IsAutomated = true,
            ReplaceAllMetadata = false
        };

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
