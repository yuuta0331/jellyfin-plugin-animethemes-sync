using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Plugin-owned persistent cache for AnimeThemes Sync.
/// </summary>
public sealed class AnimeThemesDataStore
{
    private const int CurrentSchemaVersion = 1;
    private const int DefaultLimit = 80;
    private const int MaxLimit = 100;
    private readonly IAnimeThemesDataPathProvider _pathProvider;
    private readonly IAnimeThemesServerIdentityProvider _serverIdentity;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
    private readonly object _syncRoot = new();
    private CacheDocument? _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeThemesDataStore"/> class.
    /// </summary>
    public AnimeThemesDataStore(IAnimeThemesDataPathProvider pathProvider, IAnimeThemesServerIdentityProvider serverIdentity)
    {
        _pathProvider = pathProvider;
        _serverIdentity = serverIdentity;
    }

    /// <summary>
    /// Gets the absolute cache file path.
    /// </summary>
    public string DatabasePath => Path.Combine(_pathProvider.GetPluginDataDirectory(), "animethemes-sync-cache.json");

    /// <summary>
    /// Gets the current server kind.
    /// </summary>
    public string ServerKind => _serverIdentity.ServerKind;

    /// <summary>
    /// Ensures that the cache file exists and can be read.
    /// </summary>
    public void EnsureInitialized()
    {
        lock (_syncRoot)
        {
            _ = LoadDocument();
        }
    }

    /// <summary>
    /// Clears BrowserItems, ThemeFiles, and LibrarySyncState rows for the current server.
    /// </summary>
    public void ClearBrowserCache()
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            document.BrowserItems.RemoveAll(i => IsCurrentServer(i.ServerKind));
            document.ThemeFiles.RemoveAll(i => IsCurrentServer(i.ServerKind));
            document.LibrarySyncState.RemoveAll(i => IsCurrentServer(i.ServerKind));
            var state = GetOrCreateServerCacheState(document);
            state.BrowserCacheReady = false;
            state.BrowserCacheVersion = string.Empty;
            state.LastFullScanUtc = null;
            state.LastError = null;
            state.UpdatedAtUtc = FormatDate(DateTimeOffset.UtcNow);
            SaveDocument(document);
        }
    }

    /// <summary>
    /// Replaces the BrowserItems cache for a set of libraries.
    /// </summary>
    public void ReplaceBrowserItems(IEnumerable<BrowserItemRecord> records, IEnumerable<(string LibraryId, string? LibraryName, int ItemCount)> libraries)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            document.BrowserItems.RemoveAll(i => IsCurrentServer(i.ServerKind));
            document.LibrarySyncState.RemoveAll(i => IsCurrentServer(i.ServerKind));
            var now = FormatDate(DateTimeOffset.UtcNow);

            foreach (var record in records)
            {
                document.BrowserItems.Add(ToStoredBrowserItem(record));
            }

            foreach (var library in libraries)
            {
                document.LibrarySyncState.Add(new StoredLibrarySyncState
                {
                    ServerKind = ServerKind,
                    LibraryId = library.LibraryId,
                    LibraryName = library.LibraryName,
                    LastFullScanUtc = now,
                    LastQuickRefreshUtc = now,
                    ItemCount = library.ItemCount,
                    CacheVersion = now
                });
            }

            var state = GetOrCreateServerCacheState(document);
            state.BrowserCacheReady = true;
            state.BrowserCacheVersion = now;
            state.LastFullScanUtc = now;
            state.LastError = null;
            state.UpdatedAtUtc = now;
            SaveDocument(document);
        }
    }

    /// <summary>
    /// Upserts one BrowserItems row.
    /// </summary>
    public void UpsertBrowserItem(BrowserItemRecord record)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            document.BrowserItems.RemoveAll(i => IsCurrentServer(i.ServerKind) && string.Equals(i.ItemId, record.ItemId, StringComparison.OrdinalIgnoreCase));
            document.BrowserItems.Add(ToStoredBrowserItem(record));
            SaveDocument(document);
        }
    }

    /// <summary>
    /// Gets a page of BrowserItems.
    /// </summary>
    public ThemeBrowserItemsPage QueryBrowserItems(
        string? libraryId,
        int? startIndex,
        int? limit,
        string? sortBy,
        string? sortOrder,
        string? searchTerm,
        string? itemType,
        string? linkFilter,
        string? savedFilter)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            var normalizedStart = Math.Max(0, startIndex ?? 0);
            var normalizedLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
            var filtered = document.BrowserItems
                .Where(i => IsCurrentServer(i.ServerKind))
                .Where(i => MatchesBrowserQuery(i, libraryId, searchTerm, itemType, linkFilter, savedFilter));

            filtered = SortBrowserItems(filtered, sortBy, sortOrder);
            var materialized = filtered.ToList();
            var items = materialized
                .Skip(normalizedStart)
                .Take(normalizedLimit)
                .Select(ToBrowserItem)
                .ToList();

            return new ThemeBrowserItemsPage(
                items,
                materialized.Count,
                normalizedStart,
                normalizedLimit,
                GetCacheVersion(document),
                IsBrowserCacheReady(document));
        }
    }

    /// <summary>
    /// Gets summary values from BrowserItems.
    /// </summary>
    public ThemeBrowserSummary GetBrowserSummary()
    {
        lock (_syncRoot)
        {
            var items = LoadDocument().BrowserItems.Where(i => IsCurrentServer(i.ServerKind)).ToList();
            return new ThemeBrowserSummary(
                items.Count,
                items.Sum(i => i.ThemeVideoCount),
                items.Sum(i => i.ThemeSongCount),
                items.Sum(i => i.ThemeExtraCount),
                items.Sum(i => i.ThemeBytes),
                items.Count(i => string.Equals(i.ItemType, "Series", StringComparison.OrdinalIgnoreCase)),
                items.Count(i => string.Equals(i.ItemType, "Movie", StringComparison.OrdinalIgnoreCase)),
                0,
                items.Count(i => i.HasLocalThemes));
        }
    }

    /// <summary>
    /// Gets storage status.
    /// </summary>
    public AnimeThemesStorageStatus GetStorageStatus(bool rebuildRunning)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            var file = new FileInfo(DatabasePath);
            return new AnimeThemesStorageStatus(
                DatabasePath,
                file.Exists,
                file.Exists ? file.Length : 0,
                document.BrowserItems.Count(i => IsCurrentServer(i.ServerKind)),
                GetCacheVersion(document),
                rebuildRunning,
                IsBrowserCacheReady(document),
                GetServerCacheState(document)?.LastFullScanUtc,
                GetServerCacheState(document)?.LastError);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the browser cache has completed at least one rebuild.
    /// </summary>
    public bool IsBrowserCacheReady()
    {
        lock (_syncRoot)
        {
            return IsBrowserCacheReady(LoadDocument());
        }
    }

    /// <summary>
    /// Stores the last browser cache rebuild error for diagnostics.
    /// </summary>
    public void SetBrowserCacheRebuildError(string? error)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            var state = GetOrCreateServerCacheState(document);
            state.LastError = error;
            state.UpdatedAtUtc = FormatDate(DateTimeOffset.UtcNow);
            SaveDocument(document);
        }
    }

    /// <summary>
    /// Finds a previously tracked extras path.
    /// </summary>
    public string? FindPreviousExtraPath(ThemeExtraPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.Key))
        {
            return null;
        }

        lock (_syncRoot)
        {
            return LoadDocument().ExtraFiles
                .FirstOrDefault(i => IsCurrentServer(i.ServerKind) && string.Equals(i.Key, plan.Key, StringComparison.OrdinalIgnoreCase))
                ?.TargetPath;
        }
    }

    /// <summary>
    /// Upserts one extras file row.
    /// </summary>
    public void UpdateExtraFile(ThemeExtraPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.Key) || string.IsNullOrWhiteSpace(plan.TargetPath))
        {
            return;
        }

        var fileName = Path.GetFileName(plan.TargetPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var info = new FileInfo(plan.TargetPath);
        lock (_syncRoot)
        {
            var document = LoadDocument();
            document.ExtraFiles.RemoveAll(i => IsCurrentServer(i.ServerKind) && string.Equals(i.Key, plan.Key, StringComparison.OrdinalIgnoreCase));
            document.ExtraFiles.Add(new StoredExtraFile
            {
                ServerKind = ServerKind,
                Key = plan.Key,
                TargetPath = plan.TargetPath,
                FileName = fileName,
                FileSize = info.Exists ? info.Length : null,
                LastWriteTimeUtc = info.Exists ? FormatDate(info.LastWriteTimeUtc) : null,
                UpdatedAtUtc = FormatDate(DateTimeOffset.UtcNow)
            });
            SaveDocument(document);
        }
    }

    /// <summary>
    /// Imports a legacy extras manifest from one directory without modifying the manifest file.
    /// </summary>
    public LegacyExtrasImportResult ImportLegacyExtrasManifest(string directory)
    {
        var path = Path.Combine(directory, ThemeExtrasManifestService.ManifestFileName);
        if (!File.Exists(path))
        {
            return new LegacyExtrasImportResult(0, 0);
        }

        LegacyExtrasManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<LegacyExtrasManifest>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return new LegacyExtrasImportResult(0, 0);
        }
        catch (IOException)
        {
            return new LegacyExtrasImportResult(0, 0);
        }

        if (manifest?.Files == null || manifest.Files.Count == 0)
        {
            return new LegacyExtrasImportResult(1, 0);
        }

        var imported = 0;
        foreach (var pair in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            var targetPath = Path.Combine(directory, pair.Value);
            UpdateExtraFile(new ThemeExtraPlan(string.Empty, targetPath) { Key = pair.Key });
            imported++;
        }

        return new LegacyExtrasImportResult(1, imported);
    }

    /// <summary>
    /// Upserts one ThemeFiles row.
    /// </summary>
    public void UpsertThemeFile(string itemId, string themeKey, string fileKind, string path)
    {
        var info = new FileInfo(path);
        lock (_syncRoot)
        {
            var document = LoadDocument();
            document.ThemeFiles.RemoveAll(i =>
                IsCurrentServer(i.ServerKind) &&
                string.Equals(i.ItemId, itemId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.ThemeKey, themeKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.FileKind, fileKind, StringComparison.OrdinalIgnoreCase));
            document.ThemeFiles.Add(new StoredThemeFile
            {
                ServerKind = ServerKind,
                ItemId = itemId,
                ThemeKey = themeKey,
                FileKind = fileKind,
                Path = path,
                ExistsFlag = info.Exists,
                FileSize = info.Exists ? info.Length : null,
                LastWriteTimeUtc = info.Exists ? FormatDate(info.LastWriteTimeUtc) : null,
                UpdatedAtUtc = FormatDate(DateTimeOffset.UtcNow)
            });
            SaveDocument(document);
        }
    }

    private CacheDocument LoadDocument()
    {
        if (_cache != null)
        {
            return _cache;
        }

        Directory.CreateDirectory(_pathProvider.GetPluginDataDirectory());
        if (!File.Exists(DatabasePath))
        {
            _cache = new CacheDocument();
            SaveDocument(_cache);
            return _cache;
        }

        try
        {
            _cache = JsonSerializer.Deserialize<CacheDocument>(File.ReadAllText(DatabasePath), _jsonOptions) ?? new CacheDocument();
            _cache.SchemaVersion = Math.Max(_cache.SchemaVersion, CurrentSchemaVersion);
            _cache.ExtraFiles ??= [];
            _cache.BrowserItems ??= [];
            _cache.ThemeFiles ??= [];
            _cache.LibrarySyncState ??= [];
            _cache.ServerCacheState ??= [];
            return _cache;
        }
        catch (JsonException)
        {
            QuarantineCacheFile();
        }
        catch (IOException)
        {
            QuarantineCacheFile();
        }

        _cache = new CacheDocument();
        SaveDocument(_cache);
        return _cache;
    }

    private void SaveDocument(CacheDocument document)
    {
        Directory.CreateDirectory(_pathProvider.GetPluginDataDirectory());
        document.SchemaVersion = CurrentSchemaVersion;
        document.UpdatedAtUtc = FormatDate(DateTimeOffset.UtcNow);
        var tempPath = DatabasePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(document, _jsonOptions));
        if (File.Exists(DatabasePath))
        {
            File.Delete(DatabasePath);
        }

        File.Move(tempPath, DatabasePath);
        _cache = document;
    }

    private bool IsCurrentServer(string? serverKind)
    {
        return string.Equals(serverKind, ServerKind, StringComparison.OrdinalIgnoreCase);
    }

    private StoredBrowserItem ToStoredBrowserItem(BrowserItemRecord record)
    {
        return new StoredBrowserItem
        {
            ServerKind = ServerKind,
            ItemId = record.ItemId,
            LibraryId = record.LibraryId,
            ItemType = record.ItemType,
            Name = record.Name,
            SortName = record.SortName,
            SeriesName = record.SeriesName,
            SeasonName = record.SeasonName,
            SeasonIndex = record.SeasonIndex,
            ProductionYear = record.ProductionYear,
            AnimeThemesSlug = record.AnimeThemesSlug,
            AniListId = record.AniListId,
            MyAnimeListId = record.MyAnimeListId,
            LinkStatus = record.LinkStatus,
            PrimaryImageTag = record.PrimaryImageTag,
            LogoImageTag = record.LogoImageTag,
            BackdropImageTag = record.BackdropImageTag,
            ThumbImageTag = record.ThumbImageTag,
            PrimaryImageUrl = record.PrimaryImageUrl,
            LogoImageUrl = record.LogoImageUrl,
            BackdropImageUrl = record.BackdropImageUrl,
            ThumbImageUrl = record.ThumbImageUrl,
            ThemeVideoCount = record.ThemeVideoCount,
            ThemeSongCount = record.ThemeSongCount,
            ThemeExtraCount = record.ThemeExtraCount,
            ThemeBytes = record.ThemeBytes,
            HasLocalThemes = record.HasLocalThemes,
            LatestEpisodeDateUtc = record.LatestEpisodeDateUtc.HasValue ? FormatDate(record.LatestEpisodeDateUtc.Value) : null,
            DateCreatedUtc = FormatDate(record.DateCreatedUtc),
            LastRefreshedUtc = FormatDate(record.LastRefreshedUtc == default ? DateTimeOffset.UtcNow : record.LastRefreshedUtc)
        };
    }

    private static ThemeBrowserLibraryItem ToBrowserItem(StoredBrowserItem item)
    {
        var itemId = Guid.Parse(item.ItemId);
        return new ThemeBrowserLibraryItem(
            itemId,
            item.Name ?? "Unknown",
            item.ItemType ?? "Unknown",
            item.AnimeThemesSlug,
            item.AniListId,
            item.MyAnimeListId,
            item.PrimaryImageUrl,
            item.LogoImageUrl,
            item.BackdropImageUrl,
            item.ThumbImageUrl,
            item.ThemeVideoCount,
            item.ThemeSongCount,
            item.ThemeExtraCount,
            item.ThemeBytes,
            item.HasLocalThemes,
            ParseDate(item.DateCreatedUtc) ?? DateTimeOffset.MinValue,
            ParseDate(item.LatestEpisodeDateUtc),
            item.LinkStatus ?? "Unlinked",
            !string.IsNullOrWhiteSpace(item.AnimeThemesSlug),
            string.Equals(item.LinkStatus, "Manual", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesBrowserQuery(StoredBrowserItem item, string? libraryId, string? searchTerm, string? itemType, string? linkFilter, string? savedFilter)
    {
        if (!string.IsNullOrWhiteSpace(libraryId) && !string.Equals(item.LibraryId, libraryId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            if (!Contains(item.Name, term) &&
                !Contains(item.AnimeThemesSlug, term) &&
                !Contains(item.AniListId, term) &&
                !Contains(item.MyAnimeListId, term))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(itemType) &&
            !string.Equals(itemType, "all", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.ItemType, itemType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(linkFilter, "linked", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.LinkStatus, "Unlinked", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(linkFilter, "unlinked", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.LinkStatus, "Unlinked", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(linkFilter, "external", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(item.AnimeThemesSlug) &&
            string.IsNullOrWhiteSpace(item.AniListId) &&
            string.IsNullOrWhiteSpace(item.MyAnimeListId))
        {
            return false;
        }

        return (savedFilter ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "saved" => item.HasLocalThemes,
            "missing" => !item.HasLocalThemes,
            "video" => item.ThemeVideoCount > 0,
            "audio" => item.ThemeSongCount > 0,
            "extras" => item.ThemeExtraCount > 0,
            _ => true
        };
    }

    private static IEnumerable<StoredBrowserItem> SortBrowserItems(IEnumerable<StoredBrowserItem> items, string? sortBy, string? sortOrder)
    {
        var descending = string.Equals(sortOrder, "Descending", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        Func<StoredBrowserItem, object?> key = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "type" or "itemtype" => item => item.ItemType,
            "saved" => item => item.ThemeVideoCount + item.ThemeSongCount + item.ThemeExtraCount,
            "size" or "themebytes" => item => item.ThemeBytes,
            "link" or "linkstatus" => item => item.LinkStatus,
            "itemadded" or "datecreatedutc" => item => item.DateCreatedUtc,
            "latestepisodeadded" or "latestepisodedateutc" => item => item.LatestEpisodeDateUtc,
            "themevideocount" => item => item.ThemeVideoCount,
            "themesongcount" => item => item.ThemeSongCount,
            "themeextracount" => item => item.ThemeExtraCount,
            _ => item => item.SortName ?? item.Name
        };

        return descending
            ? items.OrderByDescending(key).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            : items.OrderBy(key).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
    }

    private string GetCacheVersion(CacheDocument document)
    {
        var stateVersion = GetServerCacheState(document)?.BrowserCacheVersion;
        if (!string.IsNullOrWhiteSpace(stateVersion))
        {
            return stateVersion;
        }

        return document.LibrarySyncState
            .Where(i => IsCurrentServer(i.ServerKind))
            .Select(i => i.CacheVersion)
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .DefaultIfEmpty(string.Empty)
            .OrderBy(i => i, StringComparer.Ordinal)
            .LastOrDefault() ?? string.Empty;
    }

    private bool IsBrowserCacheReady(CacheDocument document)
    {
        var state = GetServerCacheState(document);
        if (state != null)
        {
            return state.BrowserCacheReady;
        }

        return document.LibrarySyncState.Any(i => IsCurrentServer(i.ServerKind));
    }

    private StoredServerCacheState? GetServerCacheState(CacheDocument document)
    {
        return document.ServerCacheState.FirstOrDefault(i => IsCurrentServer(i.ServerKind));
    }

    private StoredServerCacheState GetOrCreateServerCacheState(CacheDocument document)
    {
        var state = GetServerCacheState(document);
        if (state != null)
        {
            return state;
        }

        state = new StoredServerCacheState { ServerKind = ServerKind };
        document.ServerCacheState.Add(state);
        return state;
    }

    private void QuarantineCacheFile()
    {
        if (!File.Exists(DatabasePath))
        {
            return;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        File.Move(DatabasePath, DatabasePath + ".corrupt-" + timestamp);
    }

    private static bool Contains(string? value, string term)
    {
        return value?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static string FormatDate(DateTimeOffset date)
    {
        return date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateTime date)
    {
        return DateTime.SpecifyKind(date, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
    }

    private sealed class CacheDocument
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string UpdatedAtUtc { get; set; } = FormatDate(DateTimeOffset.UtcNow);

        public List<StoredExtraFile> ExtraFiles { get; set; } = [];

        public List<StoredBrowserItem> BrowserItems { get; set; } = [];

        public List<StoredThemeFile> ThemeFiles { get; set; } = [];

        public List<StoredLibrarySyncState> LibrarySyncState { get; set; } = [];

        public List<StoredServerCacheState> ServerCacheState { get; set; } = [];
    }

    private sealed class StoredExtraFile
    {
        public string ServerKind { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public string TargetPath { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public long? FileSize { get; set; }

        public string? LastWriteTimeUtc { get; set; }

        public string UpdatedAtUtc { get; set; } = string.Empty;
    }

    private sealed class StoredBrowserItem
    {
        public string ServerKind { get; set; } = string.Empty;

        public string ItemId { get; set; } = string.Empty;

        public string? LibraryId { get; set; }

        public string? ItemType { get; set; }

        public string? Name { get; set; }

        public string? SortName { get; set; }

        public string? SeriesName { get; set; }

        public string? SeasonName { get; set; }

        public int? SeasonIndex { get; set; }

        public int? ProductionYear { get; set; }

        public string? AnimeThemesSlug { get; set; }

        public string? AniListId { get; set; }

        public string? MyAnimeListId { get; set; }

        public string? LinkStatus { get; set; }

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

        public string? LatestEpisodeDateUtc { get; set; }

        public string? DateCreatedUtc { get; set; }

        public string? LastRefreshedUtc { get; set; }
    }

    private sealed class StoredThemeFile
    {
        public string ServerKind { get; set; } = string.Empty;

        public string ItemId { get; set; } = string.Empty;

        public string ThemeKey { get; set; } = string.Empty;

        public string FileKind { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public bool ExistsFlag { get; set; }

        public long? FileSize { get; set; }

        public string? LastWriteTimeUtc { get; set; }

        public string UpdatedAtUtc { get; set; } = string.Empty;
    }

    private sealed class StoredLibrarySyncState
    {
        public string ServerKind { get; set; } = string.Empty;

        public string LibraryId { get; set; } = string.Empty;

        public string? LibraryName { get; set; }

        public string? LastFullScanUtc { get; set; }

        public string? LastQuickRefreshUtc { get; set; }

        public int ItemCount { get; set; }

        public string CacheVersion { get; set; } = string.Empty;
    }

    private sealed class StoredServerCacheState
    {
        public string ServerKind { get; set; } = string.Empty;

        public bool BrowserCacheReady { get; set; }

        public string BrowserCacheVersion { get; set; } = string.Empty;

        public string? LastFullScanUtc { get; set; }

        public string? LastError { get; set; }

        public string UpdatedAtUtc { get; set; } = string.Empty;
    }

    private sealed class LegacyExtrasManifest
    {
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
