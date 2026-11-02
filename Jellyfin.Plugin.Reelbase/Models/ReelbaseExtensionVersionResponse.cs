using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// The response of the extension version endpoint.
/// </summary>
public class ReelbaseExtensionVersionResponse
{
    /// <summary>
    /// Gets or sets the payload of the response.
    /// </summary>
    [JsonPropertyName("data")]
    public ReelbaseExtensionVersion? Data { get; set; }
}
