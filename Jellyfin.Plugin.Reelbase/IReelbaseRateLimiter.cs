using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Tracks the Reelbase request allowance.
/// </summary>
/// <remarks>
/// The allowance is not configured here and there is no built-in default: it is what the API
/// reported when the current run started. The plugin keeps its own count from that point on and
/// holds back a slice of the allowance, so the account still has room for everything else.
/// </remarks>
public interface IReelbaseRateLimiter
{
    /// <summary>
    /// Gets a value indicating whether the allowance left is worth sending another request for.
    /// </summary>
    bool HasAllowance { get; }

    /// <summary>
    /// Gets what is left of the allowance, counting the requests the plugin has sent since the
    /// last report, or <c>null</c> when the API has not said.
    /// </summary>
    int? Remaining { get; }

    /// <summary>
    /// Gets how much of the allowance is held back for everything else using the account.
    /// </summary>
    int Reserved { get; }

    /// <summary>
    /// Gets when the API says the allowance is replenished, if it says.
    /// </summary>
    DateTimeOffset? ResetsAt { get; }

    /// <summary>
    /// Marks the start of a run. The next response re-reads the allowance; every response after
    /// that is counted locally rather than re-read.
    /// </summary>
    void BeginRun();

    /// <summary>
    /// Takes one request from the allowance, waiting out the spacing between requests.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when a request may be sent.</returns>
    Task<bool> TryAcquireAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Offers a response to the limiter. It is only read when the plugin is due a fresh report.
    /// </summary>
    /// <param name="response">The response.</param>
    void Observe(HttpResponseMessage response);

    /// <summary>
    /// Handles a rate limited response, which always forces a fresh report.
    /// </summary>
    /// <param name="response">The 429 response.</param>
    /// <returns>How long the API asks callers to wait.</returns>
    TimeSpan ObserveRateLimited(HttpResponseMessage response);
}
