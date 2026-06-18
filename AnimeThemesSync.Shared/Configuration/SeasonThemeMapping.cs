namespace AnimeThemesSync.Shared.Configuration;

/// <summary>
/// Maps one media-server season to a specific AnimeThemes anime.
/// </summary>
public sealed class SeasonThemeMapping
{
    /// <summary>
    /// Gets or sets a value indicating whether this mapping is active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the optional media-server series item id.
    /// </summary>
    public string? SeriesItemId { get; set; }

    /// <summary>
    /// Gets or sets the optional media-server series path.
    /// </summary>
    public string? SeriesPath { get; set; }

    /// <summary>
    /// Gets or sets the optional media-server season item id.
    /// </summary>
    public string? SeasonItemId { get; set; }

    /// <summary>
    /// Gets or sets the optional season folder path.
    /// </summary>
    public string? SeasonPath { get; set; }

    /// <summary>
    /// Gets or sets the optional season number.
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets the AnimeThemes anime slug.
    /// </summary>
    public string? AnimeThemesSlug { get; set; }

    /// <summary>
    /// Gets or sets the AniList anime id.
    /// </summary>
    public int? AniListId { get; set; }

    /// <summary>
    /// Gets or sets the MyAnimeList anime id.
    /// </summary>
    public int? MyAnimeListId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether automatic resolvers should avoid replacing this mapping.
    /// </summary>
    public bool Locked { get; set; }
}
