
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AnimeThemesSync.Shared.Models;

/// <summary>
/// Response wrapper for AnimeThemes resource endpoint.
/// </summary>
public sealed class AnimeThemesResourceResponse
{
    /// <summary>
    /// Gets or sets the list of resources.
    /// </summary>
    [JsonPropertyName("resources")]
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
    public List<AnimeThemesResource>? Resources { get; set; }

    [JsonPropertyName("animethemes")]
    public List<AnimeThemesTheme>? AnimeThemes { get; set; }
}

public sealed class AnimeThemesResource
{
    [JsonPropertyName("site")]
    public string? Site { get; set; }

    [JsonPropertyName("external_id")]
    public int? ExternalId { get; set; }

    [JsonPropertyName("anime")]
    public List<AnimeThemesAnime>? Anime { get; set; }
}

public sealed class AnimeThemesTheme
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("sequence")]
    public int? Sequence { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("song")]
    public AnimeThemesSong? Song { get; set; }

    [JsonPropertyName("group")]
    public AnimeThemesGroup? Group { get; set; }

    [JsonPropertyName("animethemeentries")]
    public List<AnimeThemesEntry>? Entries { get; set; }
}

public sealed class AnimeThemesEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("episodes")]
    public string? Episodes { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("spoiler")]
    public bool? Spoiler { get; set; }

    [JsonPropertyName("nsfw")]
    public bool? Nsfw { get; set; }

    [JsonPropertyName("videos")]
    public List<AnimeThemesVideo>? Videos { get; set; }
}

public sealed class AnimeThemesVideo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("basename")]
    public string? Basename { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("resolution")]
    public int? Resolution { get; set; }

    [JsonPropertyName("nc")]
    public bool? Nc { get; set; }

    [JsonPropertyName("subbed")]
    public bool? Subbed { get; set; }

    [JsonPropertyName("lyrics")]
    public bool? Lyrics { get; set; }

    [JsonPropertyName("uncen")]
    public bool? Uncen { get; set; }

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
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("basename")]
    public string? Basename { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }
}

public sealed class AnimeThemesSong
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("artists")]
    public List<AnimeThemesArtist>? Artists { get; set; }

    [JsonPropertyName("performances")]
    public List<AnimeThemesPerformance>? Performances { get; set; }
}

public sealed class AnimeThemesArtist
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
}

public sealed class AnimeThemesPerformance
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("as")]
    public string? As { get; set; }

    [JsonPropertyName("member_alias")]
    public string? MemberAlias { get; set; }

    [JsonPropertyName("member_as")]
    public string? MemberAs { get; set; }

    [JsonPropertyName("relevance")]
    public int? Relevance { get; set; }

    [JsonPropertyName("artist")]
    public AnimeThemesArtist? Artist { get; set; }
}

public sealed class AnimeThemesGroup
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
}
