using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// An error, as Reelbase reports it.
/// </summary>
public class ReelbaseError
{
    /// <summary>
    /// Gets or sets the HTTP status the error was sent with.
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// Gets or sets the human readable message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
