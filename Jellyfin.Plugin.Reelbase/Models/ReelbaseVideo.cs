using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Reelbase.Models;

/// <summary>
/// A music video record returned by the Reelbase API.
/// </summary>
/// <remarks>
/// The list and search endpoints return everything but <see cref="Credits"/> and
/// <see cref="Genres"/>; those are added by the detail endpoint.
/// </remarks>
public class ReelbaseVideo
{
    /// <summary>
    /// Gets or sets the song title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the video slug. It is unique and stable, so it is what the plugin stores as
    /// the provider id.
    /// </summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    /// <summary>
    /// Gets or sets the artist the video belongs to.
    /// </summary>
    [JsonPropertyName("artist")]
    public ReelbaseEntityRef? Artist { get; set; }

    /// <summary>
    /// Gets or sets the director, when the record names one.
    /// </summary>
    [JsonPropertyName("director")]
    public ReelbaseEntityRef? Director { get; set; }

    /// <summary>
    /// Gets or sets the year the video was released.
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the release date as <c>yyyy-MM-dd</c>.
    /// </summary>
    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets the album the video belongs to. The field is not part of the API yet; it is
    /// read here so the album starts filling in the day the API carries one.
    /// </summary>
    /// <remarks>
    /// Kept as it arrived because the shape is not settled: a bare name and an object with a
    /// <c>name</c> or <c>title</c> are both plausible. Read it through <see cref="AlbumName"/>.
    /// </remarks>
    [JsonPropertyName("album")]
    public JsonElement? Album { get; set; }

    /// <summary>
    /// Gets the album name, whichever shape the API sent it in.
    /// </summary>
    [JsonIgnore]
    public string? AlbumName => ReadName(Album);

    /// <summary>
    /// Gets or sets the duration in seconds, when the record carries one.
    /// </summary>
    [JsonPropertyName("duration_seconds")]
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the description of the video.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the YouTube id of the video.
    /// </summary>
    [JsonPropertyName("youtube_id")]
    public string? YouTubeId { get; set; }

    /// <summary>
    /// Gets or sets the Vimeo id of the video.
    /// </summary>
    [JsonPropertyName("vimeo_id")]
    public string? VimeoId { get; set; }

    /// <summary>
    /// Gets or sets the country of production.
    /// </summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets the record label.
    /// </summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the production company.
    /// </summary>
    [JsonPropertyName("production_company")]
    public string? ProductionCompany { get; set; }

    /// <summary>
    /// Gets or sets the name of the catalogue the record was imported from.
    /// </summary>
    [JsonPropertyName("source_name")]
    public string? SourceName { get; set; }

    /// <summary>
    /// Gets or sets the url of the catalogue the record was imported from.
    /// </summary>
    [JsonPropertyName("source_url")]
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Gets or sets the number of views the site has recorded.
    /// </summary>
    [JsonPropertyName("views")]
    public long Views { get; set; }

    /// <summary>
    /// Gets or sets the public page of the video. The API sends a site relative path, which
    /// <see cref="ReelbaseClient"/> resolves against the configured instance.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the artwork of the video, resolved the same way as <see cref="Url"/>.
    /// </summary>
    [JsonPropertyName("artwork_url")]
    public string? ArtworkUrl { get; set; }

    /// <summary>
    /// Gets or sets the credits. Only the detail endpoint fills this in.
    /// </summary>
    [JsonPropertyName("credits")]
    public IReadOnlyList<ReelbaseCredit>? Credits { get; set; }

    /// <summary>
    /// Gets or sets the genres. Only the detail endpoint fills this in.
    /// </summary>
    [JsonPropertyName("genres")]
    public IReadOnlyList<ReelbaseEntityRef>? Genres { get; set; }

    private static string? ReadName(JsonElement? value)
    {
        var name = value?.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Object => FirstName(value.Value),
            _ => null
        };

        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    private static string? FirstName(JsonElement value)
    {
        foreach (var property in new[] { "name", "title" })
        {
            if (value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.String)
            {
                return found.GetString();
            }
        }

        return null;
    }
}
