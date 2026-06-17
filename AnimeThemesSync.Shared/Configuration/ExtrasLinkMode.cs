namespace AnimeThemesSync.Shared.Configuration;

/// <summary>
/// Defines how browseable extras are materialized from downloaded theme videos.
/// </summary>
public enum ExtrasLinkMode
{
    /// <summary>
    /// Create hard links first, then copy when hard links are not supported.
    /// </summary>
    HardLinkWithCopyFallback = 0,

    /// <summary>
    /// Create hard links only.
    /// </summary>
    HardLinkOnly = 1,

    /// <summary>
    /// Copy files instead of creating hard links.
    /// </summary>
    CopyOnly = 2
}
