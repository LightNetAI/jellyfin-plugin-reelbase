using System;
using System.Collections.Generic;
using System.Text;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Builds the search term a library item should be looked up with.
/// </summary>
/// <remarks>
/// <para>
/// Reelbase matches the whole <c>q</c> value as one substring of the title, the artist name or
/// the description - it does not split it into words. The name is therefore sent as the file has
/// it, artist and title together, and the server works out whether the catalogue holds it: the
/// <c>Artist - Title</c> form is the one the API forgives punctuation for, while the bare title is
/// matched literally. Splitting the name up here only spends more requests and finds less.
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
    /// Builds the query to try.
    /// </summary>
    /// <param name="name">The name of the library item.</param>
    /// <returns>The query to send, or an empty list when the name is blank.</returns>
    public static IReadOnlyList<string> ForMusicVideo(string? name)
    {
        var normalised = NormalizeName(name);

        return normalised.Length == 0 ? Array.Empty<string>() : new[] { normalised };
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

    private static bool Contains(string? value, string? other)
        => !string.IsNullOrWhiteSpace(value)
            && !string.IsNullOrWhiteSpace(other)
            && value.Contains(other.Trim(), StringComparison.OrdinalIgnoreCase);
}
