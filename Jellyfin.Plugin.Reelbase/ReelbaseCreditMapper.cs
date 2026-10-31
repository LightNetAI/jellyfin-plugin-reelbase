using System;
using Jellyfin.Data.Enums;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Maps the free form credit roles Reelbase stores onto the people types Jellyfin knows.
/// </summary>
/// <remarks>
/// Reelbase roles come from imported catalogues and are not a closed set, so an unrecognised
/// role is not a failure: the person is still added, as an unknown type, with the role text
/// Jellyfin shows next to their name.
/// </remarks>
public static class ReelbaseCreditMapper
{
    /// <summary>
    /// Maps a role to the closest <see cref="PersonKind"/>.
    /// </summary>
    /// <param name="role">The role, such as <c>Director</c>.</param>
    /// <returns>The type to credit the person as.</returns>
    public static PersonKind MapRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return PersonKind.Unknown;
        }

        var normalized = ReelbaseMatcher.Normalize(role);

        return normalized switch
        {
            "director" or "film director" or "video director" => PersonKind.Director,
            "producer" or "executive producer" or "co producer" or "line producer" or "associate producer" => PersonKind.Producer,
            "writer" or "screenwriter" or "screenplay" or "script" or "script writer" => PersonKind.Writer,
            "editor" or "edit" or "editing" or "film editor" => PersonKind.Editor,
            "composer" or "music composer" or "original music" or "score" => PersonKind.Composer,
            "lyricist" or "lyrics" => PersonKind.Lyricist,
            "artist" or "performer" or "featuring" or "featured artist" or "singer" or "vocals" or "vocalist" => PersonKind.Artist,
            "actor" or "actress" or "cast" or "lead actor" or "lead actress" or "dancer" => PersonKind.Actor,
            "narrator" => PersonKind.Narrator,
            "author" => PersonKind.Author,
            "illustrator" => PersonKind.Illustrator,
            "translator" => PersonKind.Translator,
            "conductor" => PersonKind.Conductor,
            "arranger" => PersonKind.Arranger,
            "engineer" or "recording engineer" or "sound engineer" or "mix engineer" => PersonKind.Engineer,
            "mixer" or "mixing" or "mix" => PersonKind.Mixer,
            "remixer" or "remix" => PersonKind.Remixer,
            "creator" => PersonKind.Creator,
            _ => PersonKind.Unknown
        };
    }

    /// <summary>
    /// Determines whether a role is a director credit, so that the video's own director field
    /// does not add the same person twice.
    /// </summary>
    /// <param name="role">The role to test.</param>
    /// <returns><c>true</c> when the role is a director credit.</returns>
    public static bool IsDirector(string? role)
        => MapRole(role) == PersonKind.Director;
}
