using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// The response of the approved extension list endpoint.
/// </summary>
public class ReelbaseExtensionListResponse
{
    /// <summary>
    /// Gets or sets the extensions, in alphabetical order.
    /// </summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<string> Data { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the version information.
    /// </summary>
    [JsonPropertyName("meta")]
    public ReelbaseExtensionListMeta? Meta { get; set; }
}
