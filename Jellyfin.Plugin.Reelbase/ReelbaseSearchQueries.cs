using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Builds the search terms a library item should be looked up with.
/// </summary>
/// <remarks>
/// <para>
/// Reelbase matches the whole <c>q</c> value as one substring of the title, the artist name or
/// the description - it does not split it into words, and it does not forgive punctuation. A
/// music video is usually named <c>Artist - Title</c>, so passing that through unchanged matches
/// nothing; the artist part has to be taken off first.
/// </para>
/// <para>
/// Jellyfin does not tidy the name it hands over. A file called <c>aaliyah_-_try_again.avi</c>
/// arrives as <c>aaliyah_-_try_again</c>, underscores and all, so the separators are normalised
/// to spaces before anything is looked for.
/// </para>
/// </remarks>
public static class ReelbaseSearchQueries
{
    /// <summary>
    /// The patterns that separate an artist from a title in a file name.
    /// </summary>
    private static readonly string[] _separators = [" - ", " – ", " — ", " —", " -", " | "];

    /// <summary>
    /// The punctuation a file name and a record most often disagree about: a straight quote, a
    /// curly one, a backtick, an accent, an exclamation or a question mark.
    /// </summary>
    private static readonly SearchValues<char> _disputedPunctuation = SearchValues.Create("'\u2019\u0060\u00B4!?");

    /// <summary>
    /// Builds the queries to try, most specific first.
    /// </summary>
    /// <param name="name">The name of the library item.</param>
    /// <param name="artists">The artists Jellyfin knows the item belongs to.</param>
    /// <returns>The queries to try in order.</returns>
    public static IReadOnlyList<string> ForMusicVideo(string? name, IReadOnlyList<string>? artists)
    {
        var primaryArtist = FirstArtist(artists);
        var normalised = NormalizeName(name);
        var title = StripArtistPrefix(normalised, primaryArtist);
        var queries = new List<string>(3);

        if (Split(normalised) is { } split)
        {
            // A title carrying a quote is where the file name and the record most often
            // disagree about the character itself: a film of "Freedom! '90" saved as
            // "Freedom `90" is not the same string. The bare title is matched literally and
            // finds nothing, while the "Artist - Title" form has the punctuation forgiven and
            // finds the record, so that form goes first when the title has one of them.
            if (split.Title.AsSpan().IndexOfAny(_disputedPunctuation) >= 0)
            {
                Add(queries, normalised);
            }

            // Without a quote, the title only gets a turn of its own once the artist was known
            // well enough to take it off the name with confidence; otherwise the halves the
            // separator cut out are what is asked for.
            if (!string.Equals(title, normalised, StringComparison.OrdinalIgnoreCase))
            {
                Add(queries, title);
            }

            Add(queries, split.Title);
            Add(queries, primaryArtist ?? split.Artist);
        }
        else
        {
            Add(queries, title);
            Add(queries, primaryArtist);
        }

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
        var trimmed = NormalizeName(name);
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (Split(trimmed) is not { } split)
        {
            return trimmed;
        }

        // Without a known artist the first half is only taken to be one when it cannot be part
        // of the song title: a "featuring" clause, or a credited artist the name starts with.
        if (string.IsNullOrWhiteSpace(artist))
        {
            return Contains(split.Artist, "feat") ? split.Title : trimmed;
        }

        return Contains(split.Artist, artist) || Contains(artist, split.Artist) ? split.Title : trimmed;
    }

    /// <summary>
    /// Reads the artist half out of a file name that still has one, for when Jellyfin did not
    /// work an artist out on its own: <c>aaliyah_-_try_again</c> answers <c>["aaliyah"]</c>.
    /// </summary>
    /// <param name="name">The name of the library item.</param>
    /// <returns>The artist the name suggests, or an empty list when it suggests none.</returns>
    public static IReadOnlyList<string> GuessArtists(string? name)
        => Split(NormalizeName(name)) is { } split ? [split.Artist] : [];

    /// <summary>
    /// Turns a file name into the text the API is searched with: underscores become the spaces
    /// they stand for, whitespace is collapsed, and a file extension is dropped.
    /// </summary>
    /// <param name="name">The name to normalize.</param>
    /// <returns>The normalized name.</returns>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var value = StripFileExtension(name.Trim());
        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;

        foreach (var character in value)
        {
            if (character == '_' || char.IsWhiteSpace(character))
            {
                pendingSeparator = builder.Length > 0;
                continue;
            }

            if (pendingSeparator)
            {
                builder.Append(' ');
                pendingSeparator = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Splits a normalized name into the half before the separator and the half after it.
    /// </summary>
    private static (string Artist, string Title)? Split(string normalised)
    {
        if (!TryFindSeparator(normalised, out var index, out var length) || index <= 0)
        {
            return null;
        }

        var artist = normalised[..index].Trim();
        var title = normalised[(index + length)..].Trim();

        return artist.Length > 0 && title.Length > 0 ? (artist, title) : null;
    }

    private static bool TryFindSeparator(string value, out int index, out int length)
    {
        index = -1;
        length = 0;

        foreach (var separator in _separators)
        {
            var found = value.IndexOf(separator, StringComparison.Ordinal);
            if (found > 0 && (index < 0 || found < index))
            {
                index = found;
                length = separator.Length;
            }
        }

        return index > 0;
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
