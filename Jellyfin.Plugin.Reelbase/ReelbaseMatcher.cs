using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Jellyfin.Plugin.Reelbase.Models;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Picks the Reelbase record that belongs to a library item.
/// </summary>
/// <remarks>
/// The API searches by substring and returns its results newest first, so the record that
/// matches has to be chosen here rather than taken from the top of the list.
/// </remarks>
public static class ReelbaseMatcher
{
    /// <summary>
    /// The score a candidate has to reach for its title to be considered a match at all.
    /// </summary>
    private const int MinimumTitleScore = 50;

    /// <summary>
    /// Picks the best matching video, or <c>null</c> when none of them is close enough.
    /// </summary>
    /// <param name="candidates">The records the search returned.</param>
    /// <param name="name">The name of the library item.</param>
    /// <param name="artists">The artists Jellyfin knows the item belongs to.</param>
    /// <param name="year">The year Jellyfin knows, when it does.</param>
    /// <returns>The best matching record, or <c>null</c>.</returns>
    public static ReelbaseVideo? FindBestMatch(
        IEnumerable<ReelbaseVideo>? candidates,
        string? name,
        IReadOnlyList<string>? artists,
        int? year)
    {
        if (candidates is null)
        {
            return null;
        }

        ReelbaseVideo? best = null;
        var bestScore = 0;

        foreach (var candidate in candidates)
        {
            var score = Score(candidate, name, artists, year);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return bestScore >= MinimumTitleScore ? best : null;
    }

    /// <summary>
    /// Scores how well a record matches the library item. The title dominates the score, so a
    /// record with the right title and the wrong artist still wins over one that only shares an
    /// artist.
    /// </summary>
    /// <param name="candidate">The record to score.</param>
    /// <param name="name">The name of the library item.</param>
    /// <param name="artists">The artists Jellyfin knows the item belongs to.</param>
    /// <param name="year">The year Jellyfin knows, when it does.</param>
    /// <returns>The score; higher is a better match.</returns>
    public static int Score(ReelbaseVideo candidate, string? name, IReadOnlyList<string>? artists, int? year)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var primaryArtist = artists is { Count: > 0 } ? artists[0] : null;
        var title = ReelbaseSearchQueries.StripArtistPrefix(name, primaryArtist);

        // Jellyfin does not read an artist out of "artist_-_title", but the name still says who
        // it is: with nothing on the item, the half before the separator is the best witness
        // there is, and it is what tells Aaliyah's "Try Again" from the other one.
        var credited = artists is { Count: > 0 } ? artists : ReelbaseSearchQueries.GuessArtists(name);

        var titleScore = Math.Max(
            ScoreText(candidate.Title, name),
            ScoreText(candidate.Title, title));

        if (titleScore <= 0)
        {
            return 0;
        }

        return titleScore + ScoreArtist(candidate, credited) + ScoreYear(candidate.Year, year);
    }

    /// <summary>
    /// Normalizes a value for comparison: lower case, no accents, and only letters and digits.
    /// </summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>The normalized value, which is empty when there was nothing to compare.</returns>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSeparator = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString();
    }

    private static int ScoreText(string? candidateTitle, string? wanted)
    {
        var candidate = Normalize(candidateTitle);
        var query = Normalize(wanted);

        if (candidate.Length == 0 || query.Length == 0)
        {
            return 0;
        }

        if (string.Equals(candidate, query, StringComparison.Ordinal))
        {
            return 100;
        }

        if (candidate.StartsWith(query, StringComparison.Ordinal) || query.StartsWith(candidate, StringComparison.Ordinal))
        {
            return 70;
        }

        if (candidate.Contains(query, StringComparison.Ordinal) || query.Contains(candidate, StringComparison.Ordinal))
        {
            return 60;
        }

        return (int)Math.Round(50 * TokenOverlap(candidate, query), MidpointRounding.AwayFromZero);
    }

    private static double TokenOverlap(string candidate, string query)
    {
        var candidateTokens = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var queryTokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (candidateTokens.Length == 0 || queryTokens.Length == 0)
        {
            return 0;
        }

        var matches = 0;

        foreach (var token in queryTokens)
        {
            foreach (var candidateToken in candidateTokens)
            {
                if (string.Equals(token, candidateToken, StringComparison.Ordinal))
                {
                    matches++;
                    break;
                }
            }
        }

        return (double)matches / Math.Max(candidateTokens.Length, queryTokens.Length);
    }

    private static int ScoreArtist(ReelbaseVideo candidate, IReadOnlyList<string>? artists)
    {
        if (artists is null || artists.Count == 0)
        {
            return 0;
        }

        var candidateArtist = Normalize(candidate.Artist?.Name);
        if (candidateArtist.Length == 0)
        {
            return 0;
        }

        var best = 0;

        foreach (var artist in artists)
        {
            var wanted = Normalize(artist);
            if (wanted.Length == 0)
            {
                continue;
            }

            if (string.Equals(wanted, candidateArtist, StringComparison.Ordinal))
            {
                return 40;
            }

            if (candidateArtist.Contains(wanted, StringComparison.Ordinal) || wanted.Contains(candidateArtist, StringComparison.Ordinal))
            {
                best = Math.Max(best, 20);
            }
        }

        return best;
    }

    private static int ScoreYear(int? candidateYear, int? wanted)
    {
        if (candidateYear is null || wanted is null)
        {
            return 0;
        }

        if (candidateYear == wanted)
        {
            return 5;
        }

        return Math.Abs(candidateYear.Value - wanted.Value) == 1 ? 2 : 0;
    }
}
