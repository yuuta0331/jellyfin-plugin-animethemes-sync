namespace AnimeThemesSync.Shared.Configuration;

/// <summary>
/// Which landscape image type a member contributes when building generated collection artwork.
/// </summary>
public enum SeasonCollectionLandscapeArtType
{
    /// <summary>Prefer the member's thumb image; fall back to its backdrop.</summary>
    Thumb = 0,

    /// <summary>Prefer the member's backdrop image; fall back to its thumb.</summary>
    Backdrop = 1,
}
