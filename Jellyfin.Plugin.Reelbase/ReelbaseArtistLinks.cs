using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities.Audio;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Links the artists a record names to artist items, so Jellyfin can show them.
/// </summary>
/// <remarks>
/// A music video's artist is displayed from <c>ArtistItems</c>, and that list only holds names that
/// match a <see cref="MusicArtist"/> item - the names alone are not enough. Jellyfin creates those
/// items in its post-scan task, which a metadata refresh never runs, so an item that was given its
/// artist by a refresh stays unlinked until the library is scanned again. Asking for the artist
/// here links it as the metadata lands. The lookup is by name, so an artist that already exists is
/// returned instead of duplicated.
/// </remarks>
internal static class ReelbaseArtistLinks
{
    /// <summary>
    /// Asks for an artist item for every name given.
    /// </summary>
    /// <param name="artists">The artists the record names.</param>
    /// <param name="resolve">Gets, or creates, the item for a name.</param>
    /// <param name="onFailure">Called with the name and the error when one cannot be linked.</param>
    internal static void Ensure(
        IReadOnlyList<string>? artists,
        Func<string, MusicArtist?> resolve,
        Action<string, Exception>? onFailure = null)
    {
        foreach (var name in Distinct(artists))
        {
            try
            {
                resolve(name);
            }
#pragma warning disable CA1031 // An artist that cannot be linked must not cost the metadata.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                onFailure?.Invoke(name, ex);
            }
        }
    }

    /// <summary>
    /// The names worth linking: no blanks, no duplicates, and trimmed.
    /// </summary>
    /// <param name="artists">The artists the record names.</param>
    /// <returns>The distinct names.</returns>
    internal static IEnumerable<string> Distinct(IReadOnlyList<string>? artists)
        => (artists ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
}
