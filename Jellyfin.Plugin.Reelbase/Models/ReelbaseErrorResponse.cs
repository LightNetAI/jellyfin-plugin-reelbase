using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// The body of a failed request.
/// </summary>
public class ReelbaseErrorResponse
{
    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    [JsonPropertyName("error")]
    public ReelbaseError? Error { get; set; }
}
