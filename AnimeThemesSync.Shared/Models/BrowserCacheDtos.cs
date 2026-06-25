using System;
using System.Collections.Generic;

namespace AnimeThemesSync.Shared.Models;

/// <summary>
/// Paged Library Browser result.
/// </summary>
public sealed record ThemeBrowserItemsPage(
    IReadOnlyList<ThemeBrowserLibraryItem> Items,
    int TotalRecordCount,
    int StartIndex,
    int Limit,
    string CacheVersion,
    bool CacheReady);

/// <summary>
/// Plugin storage and cache status.
/// </summary>
public sealed record AnimeThemesStorageStatus(
    string DatabasePath,
    bool DatabaseExists,
    long DatabaseBytes,
    int BrowserItemCount,
    string? CacheVersion,
    bool RebuildRunning,
    bool CacheReady,
    string? LastFullScanUtc,
    string? LastError);

/// <summary>
/// Result of a cache maintenance operation.
/// </summary>
public sealed record AnimeThemesMaintenanceResult(
    bool Started,
    string Message);

/// <summary>
/// Result of legacy extras manifest import.
/// </summary>
public sealed record LegacyExtrasImportResult(
    int ManifestsImported,
    int FilesImported);

/// <summary>
/// Lightweight BrowserItems row stored in the plugin cache.
/// </summary>
public sealed class BrowserItemRecord
{
    public string ItemId { get; set; } = string.Empty;

    public string? LibraryId { get; set; }

    public string ItemType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? SortName { get; set; }

    public string? SeriesName { get; set; }

    public string? SeasonName { get; set; }

    public int? SeasonIndex { get; set; }

    public int? ProductionYear { get; set; }

    public string? AnimeThemesSlug { get; set; }

    public string? AniListId { get; set; }

    public string? MyAnimeListId { get; set; }

    public string LinkStatus { get; set; } = "Unlinked";

    public string? PrimaryImageTag { get; set; }

    public string? LogoImageTag { get; set; }

    public string? BackdropImageTag { get; set; }

    public string? ThumbImageTag { get; set; }

    public string? PrimaryImageUrl { get; set; }

    public string? LogoImageUrl { get; set; }

    public string? BackdropImageUrl { get; set; }

    public string? ThumbImageUrl { get; set; }

    public int ThemeVideoCount { get; set; }

    public int ThemeSongCount { get; set; }

    public int ThemeExtraCount { get; set; }

    public long ThemeBytes { get; set; }

    public bool HasLocalThemes { get; set; }

    public DateTimeOffset DateCreatedUtc { get; set; }

    public DateTimeOffset? LatestEpisodeDateUtc { get; set; }

    public DateTimeOffset LastRefreshedUtc { get; set; }
}
