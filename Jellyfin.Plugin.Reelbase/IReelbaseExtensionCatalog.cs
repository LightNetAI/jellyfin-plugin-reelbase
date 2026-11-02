using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// The approved extensions the API accepts, and the version they belong to.
/// </summary>
/// <remarks>
/// The list is downloaded once and kept on disk, so a library scan decides locally whether a file
/// is worth a request. The version endpoint is asked when the plugin starts and when the nightly
/// task runs, and the list is only downloaded again when that version has moved on.
/// </remarks>
public interface IReelbaseExtensionCatalog
{
    /// <summary>
    /// Gets the version of the list that is held, or 0 when none has been downloaded yet.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Gets the extensions that are held, lower case and without the dot.
    /// </summary>
    IReadOnlyCollection<string> Extensions { get; }

    /// <summary>
    /// Gets a value indicating whether a list has been downloaded or read from disk.
    /// </summary>
    bool IsKnown { get; }

    /// <summary>
    /// Gets a value indicating whether a file of this extension may be sent to the API.
    /// </summary>
    /// <remarks>
    /// A folder, or a name with no extension, is always allowed: there is no file type to judge.
    /// Before any list has been downloaded everything is allowed, so a first run is never left
    /// without metadata, and the server refuses what it does not accept anyway.
    /// </remarks>
    /// <param name="extension">The extension, or <see cref="ReelbaseFileExtension.None"/>.</param>
    /// <returns><c>true</c> when the extension may be sent.</returns>
    bool IsAllowed(string? extension);

    /// <summary>
    /// Reads the saved list from disk.
    /// </summary>
    void Load();

    /// <summary>
    /// Asks the API for the list version, and downloads the list when it has changed.
    /// </summary>
    /// <param name="client">The client to ask with.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when a new list was downloaded and saved.</returns>
    Task<bool> RefreshAsync(IReelbaseClient client, CancellationToken cancellationToken);
}
