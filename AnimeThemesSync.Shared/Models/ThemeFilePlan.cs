using System;
using System.Collections.Generic;

namespace AnimeThemesSync.Shared.Models;

/// <summary>
/// Describes a local theme media file that should exist after synchronization.
/// </summary>
/// <param name="Path">The destination path.</param>
/// <param name="Url">The source URL.</param>
/// <param name="IsVideo">Whether the file is a video theme.</param>
/// <param name="Order">The display order within this media type.</param>
/// <param name="ThemeKey">The compact OP/ED key, e.g. OP1 or ED1v2.</param>
public sealed record ThemeFilePlan(
    string Path,
    string Url,
    bool IsVideo,
    int Order,
    string ThemeKey)
{
    /// <summary>
    /// Gets the logical owner and physical output root for this file.
    /// </summary>
    public ThemeOutputTarget? OutputTarget { get; init; }

    /// <summary>
    /// Gets a value indicating whether the downloaded source must be transcoded or remuxed.
    /// </summary>
    public bool RequiresTranscoding { get; init; }

    /// <summary>
    /// Gets the stable Browser row identifier for the source theme candidate.
    /// </summary>
    public string SourceRowId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable theme title used by download jobs.
    /// </summary>
    public string DisplayTitle { get; init; } = string.Empty;
}

/// <summary>
/// Describes an extras file that should point at a downloaded theme video.
/// </summary>
/// <param name="SourcePath">The already downloaded video path, or null for a direct-download extra.</param>
/// <param name="TargetPath">The extras path.</param>
public sealed record ThemeExtraPlan(string? SourcePath, string TargetPath)
{
    /// <summary>
    /// Gets the logical owner and physical output root for this file.
    /// </summary>
    public ThemeOutputTarget? OutputTarget { get; init; }

    /// <summary>
    /// Gets the remote URL used when the extra is downloaded directly instead of linked from a theme video.
    /// </summary>
    public string? DownloadUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether a directly downloaded extra must be remuxed to its fallback extension.
    /// </summary>
    public bool RequiresTranscoding { get; init; }

    /// <summary>
    /// Gets a stable key for this extras item across display-name format changes.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Gets known pre-manifest target paths that can be renamed to <see cref="TargetPath"/>.
    /// </summary>
    public IReadOnlyList<string> LegacyTargetPaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the human-readable theme title used by download jobs.
    /// </summary>
    public string DisplayTitle { get; init; } = string.Empty;
}

/// <summary>
/// Describes one local directory cleanup pass for plugin-owned theme files.
/// </summary>
/// <param name="Directory">The local directory to clean.</param>
/// <param name="DesiredFiles">The files that should remain in the directory.</param>
/// <param name="Themes">The AnimeThemes themes used to identify plugin-owned legacy files.</param>
public sealed record ThemeCleanupPlan(
    string Directory,
    HashSet<string> DesiredFiles,
    List<AnimeThemesTheme> Themes);

/// <summary>
/// Describes all file-system outputs for one library item.
/// </summary>
/// <param name="MediaFiles">Theme media files for backdrops/theme-music.</param>
/// <param name="ExtraFiles">Browseable extras files.</param>
/// <param name="Themes">The source AnimeThemes themes.</param>
public sealed record ThemeOutputPlan(
    List<ThemeFilePlan> MediaFiles,
    List<ThemeExtraPlan> ExtraFiles,
    List<AnimeThemesTheme> Themes)
{
    /// <summary>
    /// Gets cleanup passes that correspond to the planned output directories.
    /// </summary>
    public List<ThemeCleanupPlan> CleanupPlans { get; init; } = [];
}
