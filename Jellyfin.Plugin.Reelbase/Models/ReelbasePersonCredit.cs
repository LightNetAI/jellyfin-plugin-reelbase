using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// A video a person was credited on.
/// </summary>
public class ReelbasePersonCredit
{
    /// <summary>
    /// Gets or sets the role the person had on the video.
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// Gets or sets the video the credit belongs to.
    /// </summary>
    [JsonPropertyName("video")]
    public ReelbaseVideoRef? Video { get; set; }
}
