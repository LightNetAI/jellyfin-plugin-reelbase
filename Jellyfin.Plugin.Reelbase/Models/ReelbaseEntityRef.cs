using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// A reference to another Reelbase record, such as the artist or the director of a video.
/// </summary>
public class ReelbaseEntityRef
{
    /// <summary>
    /// Gets or sets the name of the record.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the slug the record is addressed by. The public pages of Reelbase are built
    /// from it as well, so it doubles as the provider id.
    /// </summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
}
