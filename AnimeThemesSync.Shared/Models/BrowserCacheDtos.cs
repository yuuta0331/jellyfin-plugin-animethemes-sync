using System;
using System.Collections.Generic;

#pragma warning disable SA1117

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
/// Paged Season Finder result.
/// </summary>
public sealed record SeasonFinderItemsPage(
    IReadOnlyList<SeasonThemeMappingRow> Items,
    int TotalRecordCount,
    int StartIndex,
    int Limit,
    string CacheVersion,
    bool CacheReady);

/// <summary>
/// Persistent Season Finder display row.
/// </summary>
public sealed class SeasonFinderRowRecord
{
    public string? LibraryId { get; set; }

    public SeasonThemeMappingRow Row { get; set; } = new(
        Guid.Empty, string.Empty, null, Guid.Empty, string.Empty, null, null,
        "Unmatched", "None", false, null, null, null, null, null, null, null);

    public string? OutputRootItemId { get; set; }

    public string? OutputRootPath { get; set; }

    public string? OutputScope { get; set; }

    public string? UpdatedAtUtc { get; set; }
}

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
    string? LastError,
    SeasonFinderStorageStatus? SeasonFinder = null);

/// <summary>
/// Season Finder SQLite status.
/// </summary>
public sealed record SeasonFinderStorageStatus(
    string DatabasePath,
    long DatabaseBytes,
    int ItemCount,
    string CacheVersion,
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

#pragma warning restore SA1117
