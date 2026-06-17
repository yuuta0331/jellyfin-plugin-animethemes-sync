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
    string ThemeKey);

/// <summary>
/// Describes an extras file that should point at a downloaded theme video.
/// </summary>
/// <param name="SourcePath">The already downloaded video path.</param>
/// <param name="TargetPath">The extras path.</param>
public sealed record ThemeExtraPlan(string SourcePath, string TargetPath);

/// <summary>
/// Describes all file-system outputs for one library item.
/// </summary>
/// <param name="MediaFiles">Theme media files for backdrops/theme-music.</param>
/// <param name="ExtraFiles">Browseable extras files.</param>
/// <param name="Themes">The source AnimeThemes themes.</param>
public sealed record ThemeOutputPlan(
    List<ThemeFilePlan> MediaFiles,
    List<ThemeExtraPlan> ExtraFiles,
    List<AnimeThemesTheme> Themes);
