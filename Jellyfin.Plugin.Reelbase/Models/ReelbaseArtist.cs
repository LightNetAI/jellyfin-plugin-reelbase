using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// An artist record returned by the Reelbase API.
/// </summary>
/// <remarks>
/// The list endpoint returns the summary fields and the detail endpoint adds
/// <see cref="Bio"/> and <see cref="Videos"/>, so one type covers both.
/// </remarks>
public class ReelbaseArtist
{
    /// <summary>
    /// Gets or sets the artist name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the artist slug, which is what the plugin stores as the provider id.
    /// </summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    /// <summary>
    /// Gets or sets the biography, when the detail endpoint returned one.
    /// </summary>
    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    /// <summary>
    /// Gets or sets the country the artist is from.
    /// </summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets the year the artist formed.
    /// </summary>
    [JsonPropertyName("formed_year")]
    public int? FormedYear { get; set; }

    /// <summary>
    /// Gets or sets how many published videos the artist has. Only the list endpoint reports it.
    /// </summary>
    [JsonPropertyName("video_count")]
    public int VideoCount { get; set; }

    /// <summary>
    /// Gets or sets the public page of the artist.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the artist's videos. Only the detail endpoint fills this in.
    /// </summary>
    [JsonPropertyName("videos")]
    public IReadOnlyList<ReelbaseVideo>? Videos { get; set; }

    /// <summary>
    /// Gets or sets the paging information of <see cref="Videos"/>.
    /// </summary>
    [JsonPropertyName("meta")]
    public ReelbasePageInfo? Meta { get; set; }
}
