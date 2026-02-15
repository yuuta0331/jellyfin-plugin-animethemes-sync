#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.AnimeThemesSync.Services;

/// <summary>
/// Response wrapper for AnimeThemes resource endpoint.
/// </summary>
public sealed class AnimeThemesResourceResponse
{
    /// <summary>
    /// Gets or sets the list of resources.
    /// </summary>
    [JsonPropertyName("resources")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "DTO")]
    public List<AnimeThemesResource>? Resources { get; set; }
}

/// <summary>
/// Response wrapper for AnimeThemes anime endpoint.
/// </summary>
public sealed class AnimeThemesResponse
{
    /// <summary>
    /// Gets or sets the list of anime.
    /// </summary>
    [JsonPropertyName("anime")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "DTO")]
    public List<AnimeThemesAnime>? Anime { get; set; }
}

public sealed class AnimeThemesAnime
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("season")]
    public string? Season { get; set; }

    [JsonPropertyName("resources")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "DTO")]
    public List<AnimeThemesResource>? Resources { get; set; }

    [JsonPropertyName("animethemes")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "DTO")]
    public List<AnimeThemesTheme>? AnimeThemes { get; set; }
}

public sealed class AnimeThemesResource
{
    [JsonPropertyName("site")]
    public string? Site { get; set; }

    [JsonPropertyName("external_id")]
    public int? ExternalId { get; set; }

    [JsonPropertyName("anime")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "DTO")]
    public List<AnimeThemesAnime>? Anime { get; set; }
}

public sealed class AnimeThemesTheme
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("animethemeentries")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "DTO")]
    public List<AnimeThemesEntry>? Entries { get; set; }
}

public sealed class AnimeThemesEntry
{
    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("spoiler")]
    public bool? Spoiler { get; set; }

    [JsonPropertyName("nsfw")]
    public bool? Nsfw { get; set; }

    [JsonPropertyName("videos")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "DTO")]
    public List<AnimeThemesVideo>? Videos { get; set; }
}

public sealed class AnimeThemesVideo
{
    [JsonPropertyName("basename")]
    public string? Basename { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("resolution")]
    public int? Resolution { get; set; }

    [JsonPropertyName("nc")]
    public bool? Nc { get; set; }

    [JsonPropertyName("overlap")]
    public string? Overlap { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    [JsonPropertyName("audio")]
    public AnimeThemesAudio? Audio { get; set; }
}

public sealed class AnimeThemesAudio
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }
}
