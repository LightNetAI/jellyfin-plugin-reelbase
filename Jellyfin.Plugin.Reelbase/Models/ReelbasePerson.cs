using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// A person record returned by the Reelbase API, such as a credited director.
/// </summary>
/// <remarks>
/// The list endpoint returns the summary fields and the detail endpoint adds
/// <see cref="Credits"/>, so one type covers both.
/// </remarks>
public class ReelbasePerson
{
    /// <summary>
    /// Gets or sets the person's name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the person slug, which is what the plugin stores as the provider id.
    /// </summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    /// <summary>
    /// Gets or sets the biography.
    /// </summary>
    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    /// <summary>
    /// Gets or sets the person's website.
    /// </summary>
    [JsonPropertyName("website")]
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets how many credits the person has. Only the list endpoint reports it.
    /// </summary>
    [JsonPropertyName("credit_count")]
    public int CreditCount { get; set; }

    /// <summary>
    /// Gets or sets the portrait of the person.
    /// </summary>
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the public page of the person.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the videos the person worked on. Only the detail endpoint fills this in.
    /// </summary>
    [JsonPropertyName("credits")]
    public IReadOnlyList<ReelbasePersonCredit>? Credits { get; set; }
}
