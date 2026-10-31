using System;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// What the API last said about the account's allowance, and whether it has asked the plugin to
/// stop sending for a while.
/// </summary>
/// <param name="Remaining">How many requests were left when the report was taken.</param>
/// <param name="Limit">The size of the window, when the API reports one.</param>
/// <param name="ResetsAt">When the window is replenished.</param>
/// <param name="PausedUntil">When the API said it would accept requests again, if it said.</param>
public sealed record ReelbaseAllowance(
    int? Remaining,
    int? Limit,
    DateTimeOffset? ResetsAt,
    DateTimeOffset? PausedUntil = null);
