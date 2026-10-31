using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// A response carrying a single record.
/// </summary>
/// <typeparam name="T">The type of the record.</typeparam>
public class ReelbaseItemResponse<T>
{
    /// <summary>
    /// Gets or sets the record.
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }
}
