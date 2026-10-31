using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// A credit on a video, such as <c>Director: Annette Milburn</c>.
/// </summary>
public class ReelbaseCredit
{
    /// <summary>
    /// Gets or sets the role, such as <c>Director</c> or <c>Director of Photography</c>.
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// Gets or sets the person who is credited.
    /// </summary>
    [JsonPropertyName("person")]
    public ReelbaseEntityRef? Person { get; set; }
}
