using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// The paging information of a list response.
/// </summary>
public class ReelbasePageInfo
{
    /// <summary>
    /// Gets or sets the page these records are from, counting from one.
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets how many records were asked for per page.
    /// </summary>
    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    /// <summary>
    /// Gets or sets the total number of matching records.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the number of the last page.
    /// </summary>
    [JsonPropertyName("last_page")]
    public int LastPage { get; set; }
}
