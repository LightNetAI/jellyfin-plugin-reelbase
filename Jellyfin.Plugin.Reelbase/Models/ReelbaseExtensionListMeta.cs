using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// The version and size of an approved extension list.
/// </summary>
public class ReelbaseExtensionListMeta
{
    /// <summary>
    /// Gets or sets the version of the list.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets how many extensions the list holds.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
