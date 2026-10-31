using Jellyfin.Plugin.Reelbase.Models;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Resolves the site relative paths the API returns against the configured instance, so that
/// the rest of the plugin only ever sees absolute urls.
/// </summary>
internal static class ReelbaseModelNormalizer
{
    /// <summary>
    /// Resolves the urls of a video.
    /// </summary>
    /// <param name="video">The video to normalize.</param>
    /// <param name="serverUrl">The configured server address.</param>
    internal static void Normalize(ReelbaseVideo video, string serverUrl)
    {
        video.Url = ReelbaseUrls.Resolve(serverUrl, video.Url);
        video.ArtworkUrl = ReelbaseUrls.Resolve(serverUrl, video.ArtworkUrl);
    }

    /// <summary>
    /// Resolves the urls of an artist and of the videos it carries.
    /// </summary>
    /// <param name="artist">The artist to normalize.</param>
    /// <param name="serverUrl">The configured server address.</param>
    internal static void Normalize(ReelbaseArtist artist, string serverUrl)
    {
        artist.Url = ReelbaseUrls.Resolve(serverUrl, artist.Url);

        if (artist.Videos is { } videos)
        {
            foreach (var video in videos)
            {
                Normalize(video, serverUrl);
            }
        }
    }

    /// <summary>
    /// Resolves the urls of a person and of the videos they are credited on.
    /// </summary>
    /// <param name="person">The person to normalize.</param>
    /// <param name="serverUrl">The configured server address.</param>
    internal static void Normalize(ReelbasePerson person, string serverUrl)
    {
        person.Url = ReelbaseUrls.Resolve(serverUrl, person.Url);
        person.ImageUrl = ReelbaseUrls.Resolve(serverUrl, person.ImageUrl);

        if (person.Credits is { } credits)
        {
            foreach (var credit in credits)
            {
                if (credit.Video is { } video)
                {
                    video.Url = ReelbaseUrls.Resolve(serverUrl, video.Url);
                }
            }
        }
    }
}
