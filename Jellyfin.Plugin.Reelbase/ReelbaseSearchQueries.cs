using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Builds the search terms a library item should be looked up with.
/// </summary>
/// <remarks>
/// Reelbase matches the whole <c>q</c> value as one substring of the title, the artist name or
/// the description - it does not split it into words. A music video is usually named
/// <c>Artist - Title</c>, so passing that through unchanged matches nothing; the artist part is
/// therefore stripped before the title is searched for.
/// </remarks>
public static class ReelbaseSearchQueries
{
    /// <summary>
    /// The patterns that separate an artist from a title in a file name.
    /// </summary>
    private static readonly string[] _separators = [" - ", " – ", " — ", " —", " -", " | "];

    /// <summary>
    /// Builds the queries to try, most specific first.
    /// </summary>
    /// <param name="name">The name of the library item.</param>
    /// <param name="artists">The artists Jellyfin knows the item belongs to.</param>
    /// <returns>The queries to try in order.</returns>
    public static IReadOnlyList<string> ForMusicVideo(string? name, IReadOnlyList<string>? artists)
    {
        var primaryArtist = FirstArtist(artists);
        var title = StripArtistPrefix(name, primaryArtist);
        var queries = new List<string>(3);

        Add(queries, title);
        Add(queries, primaryArtist);

        return queries;
    }

    /// <summary>
    /// Reduces <c>Artist - Title</c> to <c>Title</c>, and drops a file extension if one is left.
    /// </summary>
    /// <param name="name">The name of the library item.</param>
    /// <param name="artist">The artist the item is credited to, when it is known.</param>
    /// <returns>The title to search for.</returns>
    public static string StripArtistPrefix(string? name, string? artist)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var trimmed = StripFileExtension(name.Trim());
        var separatorIndex = FindSeparator(trimmed);
        if (separatorIndex <= 0)
        {
            return trimmed;
        }

        var prefix = trimmed[..separatorIndex].Trim();
        var remainder = trimmed[(separatorIndex + 3)..].Trim();

        if (remainder.Length == 0)
        {
            return trimmed;
        }

        // Without a known artist the prefix is only taken to be one when it cannot be part of
        // the song title: a "featuring" clause, or a credited artist that the name starts with.
        if (string.IsNullOrWhiteSpace(artist))
        {
            return Contains(prefix, "feat") ? remainder : trimmed;
        }

        return Contains(prefix, artist) || Contains(artist, prefix) ? remainder : trimmed;
    }

    private static int FindSeparator(string value)
    {
        var best = -1;

        foreach (var separator in _separators)
        {
            var index = value.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0 && (best < 0 || index < best))
            {
                best = index;
            }
        }

        return best;
    }

    private static string StripFileExtension(string value)
    {
        // A path is deliberately not handed to Path.GetFileNameWithoutExtension: a title such as
        // "AC/DC - Thunderstruck" would lose everything before the slash on Unix.
        var dot = value.LastIndexOf('.');
        if (dot <= 0 || dot < value.LastIndexOf(' '))
        {
            return value;
        }

        var extension = value[(dot + 1)..];

        return extension.Length is > 0 and <= 4 && IsAsciiAlphanumeric(extension)
            ? value[..dot].Trim()
            : value;
    }

    private static bool IsAsciiAlphanumeric(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static string? FirstArtist(IReadOnlyList<string>? artists)
    {
        if (artists is null)
        {
            return null;
        }

        foreach (var artist in artists)
        {
            if (!string.IsNullOrWhiteSpace(artist))
            {
                return artist.Trim();
            }
        }

        return null;
    }

    private static bool Contains(string? value, string? other)
        => !string.IsNullOrWhiteSpace(value)
            && !string.IsNullOrWhiteSpace(other)
            && value.Contains(other.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void Add(List<string> queries, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var trimmed = query.Trim();

        foreach (var existing in queries)
        {
            if (string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        queries.Add(trimmed);
    }
}
