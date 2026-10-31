using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Keeps the plugin inside the allowance the Reelbase API reports.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here hard-codes how many requests a day an account gets. The figure comes from the
/// API's own <c>X-RateLimit-Limit</c>, <c>X-RateLimit-Remaining</c> and <c>X-RateLimit-Reset</c>
/// headers, which are read <b>once per run</b> - not on every response - and the plugin then
/// keeps its own count for the rest of the run. Reading them again on every response would
/// repeatedly overwrite the count with a figure that other clients sharing the key also move,
/// which is what a local count avoids. A 429 is the exception: it proves the count has drifted,
/// so the limiter takes a fresh report from that response.
/// </para>
/// <para>
/// Only 95% of the allowance is spent. A Reelbase account is shared with
/// the people using the site and with any other integration on the same key, so the plugin
/// deliberately leaves the last 5% alone: with a 1,000 request day it stops at 950, which keeps
/// new content and manual lookups working while a library refresh is in progress.
/// </para>
/// </remarks>
public sealed class ReelbaseRateLimiter : IReelbaseRateLimiter
{
    /// <summary>
    /// The shortest gap between two requests.
    /// </summary>
    private static readonly TimeSpan _defaultMinimumRequestInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// How long to hold off for when the API rate limits without saying for how long.
    /// </summary>
    private static readonly TimeSpan _fallbackRetryDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The longest a request will wait for a pause to pass.
    /// </summary>
    private static readonly TimeSpan _maximumWait = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How often the allowance may be written to disk while it is being spent.
    /// </summary>
    private static readonly TimeSpan _saveInterval = TimeSpan.FromSeconds(15);

    private readonly object _stateLock = new();
    private readonly ILogger<ReelbaseRateLimiter> _logger;
    private readonly IReelbaseAllowanceStore _store;
    private readonly TimeSpan _minimumRequestInterval;

    /// <summary>
    /// When the next request may be sent, on the monotonic clock.
    /// </summary>
    private long _nextRequestTimestamp = Stopwatch.GetTimestamp();

    /// <summary>
    /// What the API reported when the run started, or <c>null</c> when it has not said yet.
    /// </summary>
    private int? _reportedRemaining;

    /// <summary>
    /// The size of the window, when the API reports one.
    /// </summary>
    private int? _limit;

    /// <summary>
    /// How many requests the plugin has sent since the report was taken.
    /// </summary>
    private int _spent;

    /// <summary>
    /// When the window is replenished, when the API reports it.
    /// </summary>
    private DateTimeOffset? _resetsAt;

    /// <summary>
    /// When a 429's <c>Retry-After</c> says requests may start again.
    /// </summary>
    private DateTimeOffset? _pausedUntil;

    /// <summary>
    /// Whether the next response should be read, rather than counted locally.
    /// </summary>
    private bool _needsReport = true;

    private bool _exhaustionLogged;
    private DateTimeOffset _lastSaved = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbaseRateLimiter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="allowanceStore">Remembers the allowance across restarts.</param>
    public ReelbaseRateLimiter(ILogger<ReelbaseRateLimiter> logger, IReelbaseAllowanceStore allowanceStore)
        : this(logger, allowanceStore, _defaultMinimumRequestInterval)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbaseRateLimiter"/> class with an
    /// explicit spacing between requests.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="allowanceStore">Remembers the allowance across restarts.</param>
    /// <param name="minimumRequestInterval">The shortest gap between two requests.</param>
    internal ReelbaseRateLimiter(ILogger<ReelbaseRateLimiter> logger, IReelbaseAllowanceStore allowanceStore, TimeSpan minimumRequestInterval)
    {
        _logger = logger;
        _store = allowanceStore;
        _minimumRequestInterval = minimumRequestInterval;

        var stored = allowanceStore.Load();
        if (stored is not null)
        {
            _reportedRemaining = stored.Remaining;
            _limit = stored.Limit;
            _resetsAt = stored.ResetsAt;
            _pausedUntil = stored.PausedUntil;

            // The stored report keeps a restart from believing the allowance is untouched. It is
            // only a fallback: the first response afterwards replaces it with a fresh one.
            _needsReport = true;
        }
    }

    /// <inheritdoc />
    public bool HasAllowance
    {
        get
        {
            lock (_stateLock)
            {
                RollWindowLocked();
                return HasAllowanceLocked;
            }
        }
    }

    /// <inheritdoc />
    public int? Remaining
    {
        get
        {
            lock (_stateLock)
            {
                RollWindowLocked();
                return RemainingLocked;
            }
        }
    }

    /// <inheritdoc />
    public int Reserved
    {
        get
        {
            lock (_stateLock)
            {
                return ReservedLocked;
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? ResetsAt
    {
        get
        {
            lock (_stateLock)
            {
                return _resetsAt;
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? PausedUntil
    {
        get
        {
            lock (_stateLock)
            {
                RollWindowLocked();
                return _pausedUntil;
            }
        }
    }

    /// <inheritdoc />
    public TimeSpan MaximumWait => _maximumWait;

    /// <summary>
    /// Gets the allowance left, counting what the plugin itself has spent.
    /// </summary>
    private int? RemainingLocked => _reportedRemaining is { } reported ? Math.Max(0, reported - _spent) : null;

    /// <summary>
    /// Gets the slice of the allowance that is held back for everything else on the account.
    /// </summary>
    private int ReservedLocked => _limit is { } limit ? ((limit * 5) + 99) / 100 : 0;

    /// <summary>
    /// Gets a value indicating whether another request is worth sending.
    /// </summary>
    private bool HasAllowanceLocked => RemainingLocked is not { } remaining || remaining > ReservedLocked;

    /// <inheritdoc />
    public void BeginRun()
    {
        lock (_stateLock)
        {
            RollWindowLocked();
            _needsReport = true;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken)
    {
        TimeSpan pause;

        lock (_stateLock)
        {
            RollWindowLocked();

            var now = DateTimeOffset.UtcNow;

            if (_pausedUntil is { } pausedUntil && pausedUntil > now)
            {
                // A Retry-After is a promise, not a suggestion: nothing is sent before it passes.
                pause = pausedUntil - now;
                if (pause > _maximumWait)
                {
                    // Waiting this out would park a scan on a thread until the window resets.
                    _logger.LogDebug(
                        "Reelbase asked for a pause until {PausedUntil:u}, which is longer than a request will wait; leaving the rest to the next run.",
                        pausedUntil);
                    return false;
                }

                _spent++;
            }
            else if (HasAllowanceLocked)
            {
                pause = TimeSpan.Zero;
                _spent++;
            }
            else
            {
                LogExhaustion();
                return false;
            }
        }

        var slot = ReserveSlot();
        var untilSlot = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), slot);
        var wait = pause > untilSlot ? pause : untilSlot;

        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <inheritdoc />
    public void Observe(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        // The count is the plugin's own between reports: a later response would overwrite it
        // with a figure that also moves when somebody else uses the account.
        bool adopted;
        int remaining;

        lock (_stateLock)
        {
            if (!_needsReport)
            {
                return;
            }

            remaining = ReadIntHeader(response, "X-RateLimit-Remaining");
            var limit = ReadIntHeader(response, "X-RateLimit-Limit");
            var reset = ReadIntHeader(response, "X-RateLimit-Reset");

            if (remaining < 0)
            {
                // Nothing to count from, so the next response gets another go at reporting.
                return;
            }

            _reportedRemaining = remaining;
            _spent = 0;
            _needsReport = false;
            adopted = true;

            if (limit >= 0)
            {
                _limit = limit;
            }

            if (reset > 0)
            {
                _resetsAt = DateTimeOffset.FromUnixTimeSeconds(reset);
            }
        }

        if (adopted)
        {
            SaveIfDue(force: remaining <= Reserved);
        }

        if (remaining <= Reserved)
        {
            LogExhaustion();
        }
    }

    /// <inheritdoc />
    public TimeSpan ObserveRateLimited(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var reset = ReadIntHeader(response, "X-RateLimit-Reset");
        var resetsAt = reset > 0 ? DateTimeOffset.FromUnixTimeSeconds(reset) : (DateTimeOffset?)null;

        // Retry-After is what the API asked for and wins; the reset is only what is left of the
        // window, which is the same thing when the account is out of requests for the day.
        var retryDelay = ReadRetryAfter(response)
            ?? (resetsAt is { } windowEnd ? windowEnd - DateTimeOffset.UtcNow : (TimeSpan?)null)
            ?? _fallbackRetryDelay;

        if (retryDelay <= TimeSpan.Zero)
        {
            retryDelay = _fallbackRetryDelay;
        }

        var pausedUntil = DateTimeOffset.UtcNow + retryDelay;

        lock (_stateLock)
        {
            // Being refused proves the local count has drifted, so the next response re-reads.
            _pausedUntil = pausedUntil;
            _needsReport = true;

            if (resetsAt is { } resetsAtValue)
            {
                _resetsAt = resetsAtValue;
            }
        }

        // Everything queued behind this caller waits too, otherwise each of them spends a
        // request rediscovering the same refusal.
        DelayNextRequest(retryDelay);
        SaveIfDue(force: true);

        _logger.LogWarning(
            "The Reelbase API rate limited the plugin and asked for {Delay}, until {PausedUntil:u}; lookups wait for it.",
            retryDelay,
            pausedUntil);

        return retryDelay;
    }

    /// <summary>
    /// Reserves the next request slot, so that concurrent callers do not all send at once.
    /// </summary>
    private long ReserveSlot()
    {
        var interval = (long)(Stopwatch.Frequency * _minimumRequestInterval.TotalSeconds);

        while (true)
        {
            var current = Interlocked.Read(ref _nextRequestTimestamp);
            var now = Stopwatch.GetTimestamp();
            var slot = now > current ? now : current;

            if (Interlocked.CompareExchange(ref _nextRequestTimestamp, slot + interval, current) == current)
            {
                return slot;
            }
        }
    }

    /// <summary>
    /// Pushes the next request slot at least <paramref name="delay"/> into the future.
    /// </summary>
    private void DelayNextRequest(TimeSpan delay)
    {
        var target = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * delay.TotalSeconds);

        // Never pull the slot forward: another response may have asked for longer.
        var current = Interlocked.Read(ref _nextRequestTimestamp);
        while (target > current)
        {
            var previous = Interlocked.CompareExchange(ref _nextRequestTimestamp, target, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    /// <summary>
    /// Starts a fresh window once the one the API reported has passed.
    /// </summary>
    private void RollWindowLocked()
    {
        if (_resetsAt is not { } resetsAt || DateTimeOffset.UtcNow < resetsAt)
        {
            return;
        }

        _reportedRemaining = _limit;
        _spent = 0;
        _resetsAt = null;
        _pausedUntil = null;
        _needsReport = true;
        _exhaustionLogged = false;
    }

    private void SaveIfDue(bool force)
    {
        ReelbaseAllowance allowance;

        lock (_stateLock)
        {
            if (_reportedRemaining is null && _resetsAt is null && _pausedUntil is null)
            {
                return;
            }

            if (!force && DateTimeOffset.UtcNow - _lastSaved < _saveInterval)
            {
                return;
            }

            _lastSaved = DateTimeOffset.UtcNow;
            allowance = new ReelbaseAllowance(_reportedRemaining, _limit, _resetsAt, _pausedUntil);
        }

        _store.Save(allowance);
    }

    private void LogExhaustion()
    {
        DateTimeOffset resetsAt;
        int reserved;

        lock (_stateLock)
        {
            if (_exhaustionLogged)
            {
                return;
            }

            _exhaustionLogged = true;
            resetsAt = _resetsAt ?? new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero);
            reserved = ReservedLocked;
        }

        _logger.LogWarning(
            "The Reelbase allowance is down to the {Reserved} requests this plugin leaves for everything else on the account, so metadata lookups pause until {Reset:u}. The allowance is per account and is shared by everything using the key.",
            reserved,
            resetsAt);
    }

    /// <summary>
    /// Reads <c>Retry-After</c>, which RFC 9110 allows to be either a number of seconds or a
    /// date. Anything unreadable is left for the caller to fall back on.
    /// </summary>
    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta;
        }

        return retryAfter.Date is { } date ? date - DateTimeOffset.UtcNow : null;
    }

    private static int ReadIntHeader(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues(name, out var values)
            && !response.Content.Headers.TryGetValues(name, out values))
        {
            return -1;
        }

        var value = values.FirstOrDefault();

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
    }
}
