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
    bool CacheReady,
    IReadOnlyList<BroadcastSeasonValue>? BroadcastSeasons = null);

/// <summary>
/// Stable broadcast-season key and its current display label.
/// </summary>
public sealed record BroadcastSeasonValue(string Key, string Label, int Year, string Season);

/// <summary>
/// Paged Season Finder result.
/// </summary>
public sealed record SeasonFinderItemsPage(
    IReadOnlyList<SeasonThemeMappingRow> Items,
    int TotalRecordCount,
    int StartIndex,
    int Limit,
    string CacheVersion,
    bool CacheReady,
    IReadOnlyList<int>? SeasonNumbers = null);

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
/// Identifies every supported mapping key for one media-server season.
/// </summary>
public sealed record SeasonThemeMappingTarget(
    string? SeriesItemId,
    string? SeriesPath,
    string? SeasonItemId,
    string? SeasonPath,
    string? SeasonParentPath,
    int? SeasonNumber);

/// <summary>
/// Atomic mapping replacement. A null mapping deletes matching entries.
/// </summary>
public sealed record SeasonThemeMappingChange(
    SeasonThemeMappingTarget Target,
    Configuration.SeasonThemeMapping? Mapping,
    string Source);

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
/// Current state of a full season metadata synchronization.
/// </summary>
public sealed record SeasonMetadataSyncStatus(
    string State,
    int Processed,
    int Total,
    string? StartedAtUtc,
    string? CompletedAtUtc,
    string? Error,
    int Succeeded = 0,
    int Failed = 0,
    IReadOnlyList<SeasonMetadataSyncError>? Errors = null);

/// <summary>
/// One recoverable season metadata synchronization error.
/// </summary>
public sealed record SeasonMetadataSyncError(
    string? SeriesItemId,
    string? SeriesName,
    string? RuleKey,
    string Stage,
    string Message);

/// <summary>
/// Optional cleanup choices supplied when starting season metadata synchronization.
/// </summary>
public sealed class SeasonMetadataSyncRequest
{
    public bool RemoveManagedTags { get; set; }

    public bool RemoveManagedCollectionMemberships { get; set; }
}

/// <summary>
/// Cached broadcast metadata for one media-server season.
/// </summary>
public sealed record SeasonSummary(
    string SeasonItemId,
    int? SeasonNumber,
    string SeasonName,
    string? BroadcastSeasonKey,
    string? BroadcastSeasonLabel,
    string Status);

/// <summary>
/// Provider-independent resolved metadata for one media-server season.
/// </summary>
public sealed class SeasonMetadataRow
{
    public string SeasonItemId { get; set; } = string.Empty;

    public string SeasonName { get; set; } = string.Empty;

    public int? SeasonNumber { get; set; }

    public string Status { get; set; } = "Unmatched";

    public string Source { get; set; } = "None";

    public bool SameAsSeries { get; set; }

    public string? AnimeThemesSlug { get; set; }

    public int? AniListId { get; set; }

    public int? MyAnimeListId { get; set; }

    public int? AnimeYear { get; set; }

    public string? AnimeSeason { get; set; }
}

/// <summary>
/// Cached resolved season metadata for one series.
/// </summary>
public sealed class SeasonMetadataSnapshot
{
    public string SeriesItemId { get; set; } = string.Empty;

    public string? SeriesName { get; set; }

    public string InputFingerprint { get; set; } = string.Empty;

    public string ResolvedAtUtc { get; set; } = string.Empty;

    public string ExpiresAtUtc { get; set; } = string.Empty;

    public string? LastError { get; set; }

    public List<SeasonMetadataRow> Seasons { get; set; } = [];
}

/// <summary>
/// One persistent provider response, including expired entries usable as stale fallback.
/// </summary>
public sealed class ApiFetchCacheEntry
{
    public string CacheKey { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public string CreatedAtUtc { get; set; } = string.Empty;

    public string ExpiresAtUtc { get; set; } = string.Empty;
}

/// <summary>
/// One resolved season automation rule stored in SQLite.
/// </summary>
public sealed class SeasonAutomationRuleRecord
{
    public string RuleKey { get; set; } = string.Empty;

    public string SeriesItemId { get; set; } = string.Empty;

    public string SeasonItemId { get; set; } = string.Empty;

    public string SeasonName { get; set; } = string.Empty;

    public int? SeasonNumber { get; set; }

    public string? AnimeThemesSlug { get; set; }

    public int? AniListId { get; set; }

    public int? MyAnimeListId { get; set; }

    public int? AnimeYear { get; set; }

    public string? AnimeSeason { get; set; }

    public string? BroadcastSeasonKey { get; set; }

    public string? BroadcastSeasonLabel { get; set; }

    public string Source { get; set; } = "Unknown";

    public string? ResolvedAtUtc { get; set; }

    public string? LastError { get; set; }

    public string UpdatedAtUtc { get; set; } = string.Empty;
}

/// <summary>
/// One tag assignment owned or observed by the season automation synchronizer.
/// </summary>
public sealed class SeasonAutomationTagRecord
{
    public string RuleKey { get; set; } = string.Empty;

    public string TargetItemId { get; set; } = string.Empty;

    public string TargetItemType { get; set; } = string.Empty;

    public string TagName { get; set; } = string.Empty;

    public string Source { get; set; } = "Manual";

    public bool AddedByPlugin { get; set; }

    public string? LastAppliedAtUtc { get; set; }

    public string? LastError { get; set; }

    public string UpdatedAtUtc { get; set; } = string.Empty;
}

/// <summary>
/// One managed broadcast-season collection.
/// </summary>
public sealed class ManagedSeasonCollectionRecord
{
    public string CollectionKey { get; set; } = string.Empty;

    public string CollectionName { get; set; } = string.Empty;

    public string? CollectionItemId { get; set; }

    public bool IsPluginCreated { get; set; }

    public string? LastError { get; set; }

    public string UpdatedAtUtc { get; set; } = string.Empty;
}

/// <summary>
/// One item membership in a managed broadcast-season collection.
/// </summary>
public sealed class ManagedSeasonCollectionMemberRecord
{
    public string CollectionKey { get; set; } = string.Empty;

    public string RuleKey { get; set; } = string.Empty;

    public string TargetItemId { get; set; } = string.Empty;

    public string TargetItemType { get; set; } = string.Empty;

    public bool AddedByPlugin { get; set; }

    public string? LastAppliedAtUtc { get; set; }

    public string? LastError { get; set; }

    public string UpdatedAtUtc { get; set; } = string.Empty;
}

/// <summary>
/// Plugin-managed lock and generated-artwork state for one managed broadcast-season collection.
/// </summary>
public sealed class ManagedSeasonCollectionAssetState
{
    public string CollectionKey { get; set; } = string.Empty;

    public string? CollectionItemId { get; set; }

    public bool LockAppliedByPlugin { get; set; }

    public string? PrimaryFingerprint { get; set; }

    public string? ThumbFingerprint { get; set; }

    public string? BackdropFingerprint { get; set; }

    public string? PrimaryWrittenFileIdentity { get; set; }

    public string? ThumbWrittenFileIdentity { get; set; }

    public string? BackdropWrittenFileIdentity { get; set; }

    public string? LastGeneratedAtUtc { get; set; }

    public string? LastError { get; set; }

    public string UpdatedAtUtc { get; set; } = string.Empty;
}

/// <summary>
/// Complete normalized automation state for one series.
/// </summary>
public sealed class SeasonAutomationState
{
    public string SeriesItemId { get; set; } = string.Empty;

    public List<SeasonAutomationRuleRecord> Rules { get; set; } = [];

    public List<SeasonAutomationTagRecord> Tags { get; set; } = [];

    public List<ManagedSeasonCollectionRecord> Collections { get; set; } = [];

    public List<ManagedSeasonCollectionMemberRecord> CollectionMembers { get; set; } = [];
}

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

    public List<BroadcastSeasonValue> BroadcastSeasons { get; set; } = [];

    public List<SeasonSummary> SeasonSummaries { get; set; } = [];
}

/// <summary>
/// Metadata assignments managed by this plugin for one series.
/// </summary>
public sealed class SeasonMetadataState
{
    public string ServerKind { get; set; } = string.Empty;

    public string SeriesItemId { get; set; } = string.Empty;

    public Dictionary<string, List<string>> ManagedTags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<SeasonCollectionMembershipState> CollectionMemberships { get; set; } = [];

    public List<BroadcastSeasonValue> BroadcastSeasons { get; set; } = [];
}

/// <summary>
/// One collection membership added or observed by the season metadata synchronizer.
/// </summary>
public sealed class SeasonCollectionMembershipState
{
    public string BroadcastSeasonKey { get; set; } = string.Empty;

    public string CollectionId { get; set; } = string.Empty;

    public string CollectionName { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;

    public bool AddedByPlugin { get; set; }
}

#pragma warning restore SA1117
