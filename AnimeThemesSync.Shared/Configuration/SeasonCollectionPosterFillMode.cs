namespace AnimeThemesSync.Shared.Configuration;

/// <summary>
/// Controls how leftover space on the generated collection poster is handled
/// when fewer than four member posters are available.
/// </summary>
public enum SeasonCollectionPosterFillMode
{
    /// <summary>Fill the leftover region with a member's landscape artwork (backdrop/thumb).</summary>
    ArtworkFill = 0,

    /// <summary>Leave the leftover region as the configured canvas color.</summary>
    EmptySpace = 1,
}
