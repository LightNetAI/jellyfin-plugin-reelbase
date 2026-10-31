using System;
using System.Globalization;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Turns the paths the Reelbase API returns into absolute urls.
/// </summary>
/// <remarks>
/// The API answers with site relative paths - <c>/video/&lt;slug&gt;</c> for a page and
/// <c>/uploads/artwork/...</c> for an image - so every url has to be resolved against the
/// configured instance before Jellyfin can use it.
/// </remarks>
public static class ReelbaseUrls
{
    /// <summary>
    /// Normalizes a configured server address: adds a scheme when one is missing and drops the
    /// trailing slash so that paths can be appended with one.
    /// </summary>
    /// <param name="serverUrl">The configured address.</param>
    /// <returns>The address to build requests from.</returns>
    public static string NormalizeServerUrl(string? serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            return ReelbasePlugin.DefaultServerUrl;
        }

        var value = serverUrl.Trim().TrimEnd('/');

        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = "https://" + value;
        }

        return value;
    }

    /// <summary>
    /// Determines whether a value is already an absolute http or https url.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><c>true</c> when the value can be used as it is.</returns>
    public static bool IsAbsoluteHttpUrl(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Resolves a path the API returned against the configured instance.
    /// </summary>
    /// <param name="serverUrl">The configured server address.</param>
    /// <param name="pathOrUrl">An absolute url or a site relative path.</param>
    /// <returns>The absolute url, or <c>null</c> when there is no usable one.</returns>
    public static string? Resolve(string? serverUrl, string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return null;
        }

        if (IsAbsoluteHttpUrl(pathOrUrl))
        {
            return pathOrUrl;
        }

        var baseUri = new Uri(NormalizeServerUrl(serverUrl) + "/", UriKind.Absolute);

        return Uri.TryCreate(baseUri, pathOrUrl.TrimStart('/'), out var resolved)
            ? resolved.AbsoluteUri
            : null;
    }

    /// <summary>
    /// Builds the url of a page on the Reelbase site.
    /// </summary>
    /// <param name="serverUrl">The configured server address.</param>
    /// <param name="section">The path section, such as <c>video</c>.</param>
    /// <param name="slug">The record's slug.</param>
    /// <returns>The page url, or <c>null</c> when the slug is unusable.</returns>
    public static string? GetPageUrl(string? serverUrl, string section, string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var path = string.Create(CultureInfo.InvariantCulture, $"{section}/{slug.Trim('/')}");

        return Resolve(serverUrl, path);
    }
}
