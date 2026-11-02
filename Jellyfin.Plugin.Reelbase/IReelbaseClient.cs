using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Reelbase.Models;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Reads records from a Reelbase instance.
/// </summary>
public interface IReelbaseClient
{
    /// <summary>
    /// Searches the published videos.
    /// </summary>
    /// <param name="query">The text to search titles, artists and descriptions for.</param>
    /// <param name="extension">
    /// The item's file extension, or <see cref="ReelbaseFileExtension.None"/> for a folder or an
    /// extensionless name.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The videos on the first page of results.</returns>
    Task<IReadOnlyList<ReelbaseVideo>> SearchVideosAsync(string query, string extension, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one video by slug, with its credits and genres.
    /// </summary>
    /// <param name="slug">The video slug.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The video, or <c>null</c> when there is none.</returns>
    Task<ReelbaseVideo?> GetVideoAsync(string slug, CancellationToken cancellationToken);

    /// <summary>
    /// Searches the artists.
    /// </summary>
    /// <param name="query">The text to search artist names for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The artists on the first page of results.</returns>
    Task<IReadOnlyList<ReelbaseArtist>> SearchArtistsAsync(string query, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one artist by slug, with its videos.
    /// </summary>
    /// <param name="slug">The artist slug.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The artist, or <c>null</c> when there is none.</returns>
    Task<ReelbaseArtist?> GetArtistAsync(string slug, CancellationToken cancellationToken);

    /// <summary>
    /// Searches the credited people.
    /// </summary>
    /// <param name="query">The text to search people's names for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The people on the first page of results.</returns>
    Task<IReadOnlyList<ReelbasePerson>> SearchPeopleAsync(string query, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one person by slug, with their credits.
    /// </summary>
    /// <param name="slug">The person slug.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The person, or <c>null</c> when there is none.</returns>
    Task<ReelbasePerson?> GetPersonAsync(string slug, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the version of the approved extension list.
    /// </summary>
    /// <remarks>
    /// The version is raised whenever an administrator adds or removes an extension, so a client
    /// that already holds the list only has to download it again when this number changes.
    /// </remarks>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version, or <c>null</c> when the API did not answer.</returns>
    Task<int?> GetExtensionsVersionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the approved extensions, with the version they belong to.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list, or <c>null</c> when the API did not answer.</returns>
    Task<ReelbaseExtensionListResponse?> GetExtensionsAsync(CancellationToken cancellationToken);
}
