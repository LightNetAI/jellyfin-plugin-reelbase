using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// A paged list response.
/// </summary>
/// <typeparam name="T">The type of the records in the list.</typeparam>
public class ReelbaseListResponse<T>
{
    /// <summary>
    /// Gets or sets the records on this page.
    /// </summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<T> Data { get; set; } = Array.Empty<T>();

    /// <summary>
    /// Gets or sets the paging information.
    /// </summary>
    [JsonPropertyName("meta")]
    public ReelbasePageInfo? Meta { get; set; }
}
