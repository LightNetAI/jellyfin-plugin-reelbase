using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// A reference to a video, as carried by a person's credits.
/// </summary>
public class ReelbaseVideoRef
{
    /// <summary>
    /// Gets or sets the song title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the video slug.
    /// </summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    /// <summary>
    /// Gets or sets the year the video was released.
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the artist the video belongs to.
    /// </summary>
    [JsonPropertyName("artist")]
    public ReelbaseEntityRef? Artist { get; set; }

    /// <summary>
    /// Gets or sets the public page of the video.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
