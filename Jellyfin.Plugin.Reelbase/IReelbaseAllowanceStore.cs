namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Remembers the allowance the API last reported, so that a restart does not spend it twice.
/// </summary>
public interface IReelbaseAllowanceStore
{
    /// <summary>
    /// Reads the last reported allowance.
    /// </summary>
    /// <returns>The allowance, or <c>null</c> when nothing usable was stored.</returns>
    ReelbaseAllowance? Load();

    /// <summary>
    /// Stores the last reported allowance.
    /// </summary>
    /// <param name="allowance">The allowance to store.</param>
    void Save(ReelbaseAllowance allowance);
}
