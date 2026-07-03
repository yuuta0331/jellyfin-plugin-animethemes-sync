namespace AnimeThemesSync.Shared.Configuration;

/// <summary>
/// Controls which member artwork is used to build the generated collection thumb and backdrop.
/// </summary>
public enum SeasonCollectionLandscapeSourceMode
{
    /// <summary>Prefer each member's backdrop/thumb; fall back to its poster.</summary>
    LandscapeFirst = 0,

    /// <summary>Prefer each member's poster; fall back to its backdrop/thumb.</summary>
    PosterFirst = 1,

    /// <summary>Use only backdrop/thumb images; members without one are skipped.</summary>
    LandscapeOnly = 2,

    /// <summary>Use only posters; members without one are skipped.</summary>
    PosterOnly = 3,
}
