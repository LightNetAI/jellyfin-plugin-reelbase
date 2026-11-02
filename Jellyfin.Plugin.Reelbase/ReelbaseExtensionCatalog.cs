using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Holds the approved extension list the API publishes, and remembers it between restarts.
/// </summary>
/// <remarks>
/// The list is saved beside the plugin configuration as JSON. A scan only ever reads it, so the
/// decision to skip a file costs no request; the network is used at startup and by the nightly
/// task, and only to ask for the version - the list itself is downloaded when that version has
/// changed.
/// </remarks>
public class ReelbaseExtensionCatalog : IReelbaseExtensionCatalog
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly ILogger<ReelbaseExtensionCatalog> _logger;
    private readonly object _gate = new();

    private HashSet<string> _extensions = new(StringComparer.OrdinalIgnoreCase);
    private int? _version;
    private bool _loaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbaseExtensionCatalog"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ReelbaseExtensionCatalog}"/> interface.</param>
    public ReelbaseExtensionCatalog(IApplicationPaths applicationPaths, ILogger<ReelbaseExtensionCatalog> logger)
        : this(Path.Combine(applicationPaths.PluginConfigurationsPath, "Reelbase", "extensions.json"), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbaseExtensionCatalog"/> class that keeps
    /// its list at a given path.
    /// </summary>
    /// <param name="path">The file to read the list from and save it to.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ReelbaseExtensionCatalog}"/> interface.</param>
    internal ReelbaseExtensionCatalog(string path, ILogger<ReelbaseExtensionCatalog> logger)
    {
        _path = path;
        _logger = logger;
    }

    /// <inheritdoc />
    public int Version
    {
        get
        {
            EnsureLoaded();
            lock (_gate)
            {
                return _version ?? 0;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Extensions
    {
        get
        {
            EnsureLoaded();
            lock (_gate)
            {
                return _extensions.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public bool IsKnown
    {
        get
        {
            EnsureLoaded();
            lock (_gate)
            {
                return _version.HasValue;
            }
        }
    }

    /// <inheritdoc />
    public bool IsAllowed(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)
            || string.Equals(extension.Trim(), ReelbaseFileExtension.None, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        EnsureLoaded();
        lock (_gate)
        {
            return _extensions.Count == 0 || _extensions.Contains(extension.Trim());
        }
    }

    /// <inheritdoc />
    public void Load()
    {
        lock (_gate)
        {
            _loaded = false;
        }

        EnsureLoaded();
    }

    /// <inheritdoc />
    public async Task<bool> RefreshAsync(IReelbaseClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        try
        {
            var version = await client.GetExtensionsVersionAsync(cancellationToken).ConfigureAwait(false);
            if (version is null)
            {
                _logger.LogDebug("Reelbase did not report an extension list version; the saved list is kept.");
                return false;
            }

            if (IsKnown && Version == version.Value)
            {
                _logger.LogDebug("The approved extension list is unchanged at version {Version}.", version.Value);
                return false;
            }

            var list = await client.GetExtensionsAsync(cancellationToken).ConfigureAwait(false);
            if (list is null)
            {
                _logger.LogDebug("Reelbase did not return the approved extension list; the saved list is kept.");
                return false;
            }

            var extensions = list.Data
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Select(extension => extension.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Save(list.Meta?.Version ?? version.Value, extensions);

            return true;
        }
#pragma warning disable CA1031 // A list that cannot be refreshed must not fail a scan.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Could not refresh the approved extension list.");
            return false;
        }
    }

    private void EnsureLoaded()
    {
        lock (_gate)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
        }

        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            var stored = JsonSerializer.Deserialize<StoredExtensions>(File.ReadAllText(_path), _jsonOptions);
            if (stored is null)
            {
                return;
            }

            var extensions = new HashSet<string>(
                stored.Extensions.Where(extension => !string.IsNullOrWhiteSpace(extension)).Select(extension => extension.Trim().ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);

            lock (_gate)
            {
                _version = stored.Version;
                _extensions = extensions;
            }

            _logger.LogDebug(
                "Read {Count} approved extensions (version {Version}) from {Path}.",
                extensions.Count,
                stored.Version,
                _path);
        }
#pragma warning disable CA1031 // A list that cannot be read is rebuilt from the API instead.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Could not read the approved extension list from {Path}.", _path);
        }
    }

    private void Save(int version, IReadOnlyList<string> extensions)
    {
        var stored = new StoredExtensions
        {
            Version = version,
            Extensions = extensions,
            CheckedAt = DateTimeOffset.UtcNow
        };

        var folder = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(stored, _jsonOptions));

        lock (_gate)
        {
            _version = version;
            _extensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
            _loaded = true;
        }

        _logger.LogInformation(
            "Saved {Count} approved extensions (version {Version}) to {Path}.",
            extensions.Count,
            version,
            _path);
    }

    /// <summary>
    /// The shape of the saved list.
    /// </summary>
    private sealed class StoredExtensions
    {
        /// <summary>
        /// Gets or sets the version the list was downloaded at.
        /// </summary>
        [JsonPropertyName("version")]
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the extensions, lower case and without the dot.
        /// </summary>
        [JsonPropertyName("extensions")]
        public IReadOnlyList<string> Extensions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets when the list was last checked.
        /// </summary>
        [JsonPropertyName("checkedAt")]
        public DateTimeOffset CheckedAt { get; set; }
    }
}
