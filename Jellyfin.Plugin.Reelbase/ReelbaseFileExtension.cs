using System;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// The file extension a local scanner reports to Reelbase alongside the search query.
/// </summary>
/// <remarks>
/// The API takes the extension with the query so it can tell a folder from a file: a miss on a
/// file is recorded as missing content, a miss on a folder is not. A folder, or any name that
/// carries no extension, is sent as <c>null</c> - the value the API documents for it - rather than
/// left out, so the API always knows which of the two it was asked about.
/// </remarks>
public static class ReelbaseFileExtension
{
    /// <summary>
    /// The value sent for a folder, or for a name that has no extension.
    /// </summary>
    public const string None = "null";

    /// <summary>
    /// The longest tail taken for an extension. Every container a music video arrives in is four
    /// characters or fewer, so anything longer is part of the title, as in "Mr. Brightside".
    /// </summary>
    private const int MaximumLength = 4;

    /// <summary>
    /// Gets the extension to send: lower case, without the dot, or <see cref="None"/>.
    /// </summary>
    /// <param name="path">The path of the item, which is a directory for a folder.</param>
    /// <param name="name">The name of the item, which may carry an extension when it was typed.</param>
    /// <returns>The extension, or <see cref="None"/>.</returns>
    public static string QueryValue(string? path, string? name)
        => FromPath(path) ?? FromPath(name) ?? None;

    private static string? FromPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var fileName = value.Trim();
        var slash = Math.Max(fileName.LastIndexOf('/'), fileName.LastIndexOf('\\'));
        if (slash >= 0)
        {
            fileName = fileName[(slash + 1)..];
        }

        var dot = fileName.LastIndexOf('.');
        if (dot <= 0 || dot == fileName.Length - 1)
        {
            return null;
        }

        var extension = fileName[(dot + 1)..];
        if (extension.Length > MaximumLength || !IsAsciiAlphanumeric(extension))
        {
            return null;
        }

        return extension.ToLowerInvariant();
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
}
