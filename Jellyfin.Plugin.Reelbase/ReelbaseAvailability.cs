using System;
using System.Threading;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Stops the plugin from asking an API that is not answering.
/// </summary>
/// <remarks>
/// A library refresh or a nightly run can visit thousands of items. If Reelbase is down or
/// returning errors, every one of those items would otherwise cost a request and a log line.
/// After <see cref="FailureThreshold"/> failures in a row the plugin stops sending for
/// <see cref="CoolDown"/>, then tries again: the cost of an outage becomes a handful of probes
/// every couple of minutes rather than a request per item.
/// </remarks>
internal static class ReelbaseAvailability
{
    /// <summary>
    /// How many failures in a row are tolerated before the plugin stops asking.
    /// </summary>
    internal const int FailureThreshold = 10;

    private static readonly TimeSpan _defaultCoolDown = TimeSpan.FromMinutes(2);

    private static long _openUntilTicks;
    private static int _failures;
    private static bool _logged;

    /// <summary>
    /// Gets or sets how long the plugin waits before trying a failing API again.
    /// </summary>
    internal static TimeSpan CoolDown { get; set; } = _defaultCoolDown;

    /// <summary>
    /// Gets a value indicating whether the API is worth talking to.
    /// </summary>
    internal static bool IsAvailable => Interlocked.Read(ref _openUntilTicks) <= DateTime.UtcNow.Ticks;

    /// <summary>
    /// Notes that the API answered, which closes the breaker.
    /// </summary>
    internal static void RecordSuccess()
    {
        Interlocked.Exchange(ref _failures, 0);
        Interlocked.Exchange(ref _openUntilTicks, 0);
        _logged = false;
    }

    /// <summary>
    /// Notes a failure, opening the breaker once too many have piled up.
    /// </summary>
    /// <returns><c>true</c> when the breaker has just opened and the caller should say so once.</returns>
    internal static bool RecordFailure()
    {
        if (Interlocked.Increment(ref _failures) < FailureThreshold)
        {
            return false;
        }

        Interlocked.Exchange(ref _failures, 0);
        Interlocked.Exchange(ref _openUntilTicks, DateTime.UtcNow.Add(CoolDown).Ticks);

        if (_logged)
        {
            return false;
        }

        _logged = true;
        return true;
    }

    /// <summary>
    /// Forgets everything, for tests.
    /// </summary>
    internal static void Reset()
    {
        CoolDown = _defaultCoolDown;
        RecordSuccess();
    }
}
