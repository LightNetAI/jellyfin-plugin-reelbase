using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// The version number of the approved extension list.
/// </summary>
public class ReelbaseExtensionVersion
{
    /// <summary>
    /// Gets or sets the version, which is raised whenever an extension is added or removed.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; }
}
