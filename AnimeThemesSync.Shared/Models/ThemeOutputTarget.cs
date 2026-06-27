using System;

namespace AnimeThemesSync.Shared.Models;

/// <summary>
/// Describes the logical owner and physical output root for theme files.
/// </summary>
public sealed record ThemeOutputTarget(
    Guid LogicalItemId,
    Guid OutputRootItemId,
    string OutputRootPath,
    ThemeOutputScope Scope,
    bool IsRedirected);

/// <summary>
/// Identifies the media-library root that receives theme files.
/// </summary>
public enum ThemeOutputScope
{
    SeriesRoot,
    SeasonRoot,
    MovieRoot
}
