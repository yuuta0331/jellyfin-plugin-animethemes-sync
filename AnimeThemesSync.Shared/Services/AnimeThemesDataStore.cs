using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Plugin-owned persistent cache for AnimeThemes Sync.
/// </summary>
public sealed class AnimeThemesDataStore
{
    private const int CurrentSchemaVersion = 7;
    private const int DefaultLimit = 80;
    private const int MaxLimit = 100;
    private const int QueryResultMemoLimit = 50;
    private const int ManagerIssueLimit = 500;
    private static readonly char[] IssueFilterSeparators = [',', ';'];
    private static readonly StringComparison DownloadPathComparison =
        Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // Emby constructs a store per API request while the scheduled task holds its own
    // instance, so every instance that points at the same cache file must share one
    // lock and one in-memory document. Per-instance state would let concurrent
    // full-document saves overwrite each other and collide on the temp file.
    private static readonly ConcurrentDictionary<string, SharedState> SharedStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAnimeThemesDataPathProvider _pathProvider;
    private readonly IAnimeThemesServerIdentityProvider _serverIdentity;
    private readonly ILogger? _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
    private SharedState? _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeThemesDataStore"/> class.
    /// </summary>
    public AnimeThemesDataStore(
        IAnimeThemesDataPathProvider pathProvider,
        IAnimeThemesServerIdentityProvider serverIdentity,
        ILogger? logger = null)
    {
        _pathProvider = pathProvider;
        _serverIdentity = serverIdentity;
        _logger = logger;
    }

    /// <summary>
    /// Gets the absolute cache file path.
    /// </summary>
    public string DatabasePath => Path.Combine(_pathProvider.GetPluginDataDirectory(), "animethemes-sync-cache.json");

    /// <summary>
    /// Gets the current server kind.
    /// </summary>
    public string ServerKind => _serverIdentity.ServerKind;

    private SharedState State => _state ??= SharedStates.GetOrAdd(Path.GetFullPath(DatabasePath), _ => new SharedState());

    /// <summary>
    /// Detaches this store's shared in-memory state so the next access reloads from
    /// disk, simulating a process restart in tests.
    /// </summary>
    internal void ResetSharedStateForTests()
    {
        SharedStates.TryRemove(Path.GetFullPath(DatabasePath), out _);
        _state = null;
    }

    /// <summary>
    /// Ensures that the cache file exists and can be read.
    /// </summary>
    public void EnsureInitialized()
    {
        lock (State)
        {
            _ = LoadDocument();
        }
    }

    /// <summary>
    /// Clears BrowserItems and LibrarySyncState rows for the current server.
    /// ThemeFiles is an ownership registry and intentionally survives cache clears.
    /// </summary>
    public void ClearBrowserCache()
    {
        lock (State)
        {
            var document = LoadDocument();
            document.BrowserItems.RemoveAll(i => IsCurrentServer(i.ServerKind));
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
        lock (State)
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
        UpsertBrowserItems([record]);
    }

    /// <summary>
    /// Upserts multiple BrowserItems rows with a single document write.
    /// Later records win when the same ItemId appears more than once.
    /// </summary>
    public void UpsertBrowserItems(IReadOnlyCollection<BrowserItemRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        lock (State)
        {
            var document = LoadDocument();
            var itemIds = records
                .Select(record => record.ItemId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            document.BrowserItems.RemoveAll(i => IsCurrentServer(i.ServerKind) && itemIds.Contains(i.ItemId));
            foreach (var record in records
                         .GroupBy(record => record.ItemId, StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.Last()))
            {
                document.BrowserItems.Add(ToStoredBrowserItem(record));
            }

            SaveDocument(document);
        }
    }

    /// <summary>
    /// Gets plugin-managed metadata state for a series.
    /// </summary>
    public SeasonMetadataState? GetSeasonMetadataState(string seriesItemId)
    {
        lock (State)
        {
            return LoadDocument().SeasonMetadataStates
                .FirstOrDefault(i => IsCurrentServer(i.ServerKind) && string.Equals(i.SeriesItemId, seriesItemId, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Replaces plugin-managed metadata state for a series.
    /// </summary>
    public void SaveSeasonMetadataState(SeasonMetadataState state)
    {
        lock (State)
        {
            var document = LoadDocument();
            document.SeasonMetadataStates.RemoveAll(i =>
                IsCurrentServer(i.ServerKind) && string.Equals(i.SeriesItemId, state.SeriesItemId, StringComparison.OrdinalIgnoreCase));
            state.ServerKind = ServerKind;
            document.SeasonMetadataStates.Add(state);
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
        string? savedFilter,
        string? broadcastSeason = null)
    {
        lock (State)
        {
            var document = LoadDocument();
            var normalizedStart = Math.Max(0, startIndex ?? 0);
            var normalizedLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

            // Filtering and sorting walk every stored item; memoize the result per
            // query signature so paging through the same view reuses one pass. Any
            // document write invalidates the memo.
            var queryKey = string.Join(
                "\u0001",
                ServerKind,
                libraryId ?? string.Empty,
                sortBy ?? string.Empty,
                sortOrder ?? string.Empty,
                searchTerm ?? string.Empty,
                itemType ?? string.Empty,
                linkFilter ?? string.Empty,
                savedFilter ?? string.Empty,
                broadcastSeason ?? string.Empty);
            if (!State.QueryResultsByFilter.TryGetValue(queryKey, out var materialized))
            {
                materialized = SortBrowserItems(
                    document.BrowserItems
                        .Where(i => IsCurrentServer(i.ServerKind))
                        .Where(i => Guid.TryParse(i.ItemId, out _))
                        .Where(i => MatchesBrowserQuery(i, libraryId, searchTerm, itemType, linkFilter, savedFilter, broadcastSeason)),
                    sortBy,
                    sortOrder).ToList();
                if (State.QueryResultsByFilter.Count >= QueryResultMemoLimit)
                {
                    State.QueryResultsByFilter.Clear();
                }

                State.QueryResultsByFilter[queryKey] = materialized;
            }

            // The aggregation walks every stored item, so memoize it per filter until
            // the next document write invalidates the memo.
            var broadcastSeasonsKey = string.Join(
                "|",
                ServerKind,
                libraryId?.Trim() ?? string.Empty,
                (itemType ?? string.Empty).Trim().ToLowerInvariant());
            if (!State.BroadcastSeasonsByFilter.TryGetValue(broadcastSeasonsKey, out var broadcastSeasons))
            {
                broadcastSeasons = document.BrowserItems
                    .Where(i => IsCurrentServer(i.ServerKind))
                    .Where(i => string.IsNullOrWhiteSpace(libraryId) || string.Equals(i.LibraryId, libraryId, StringComparison.OrdinalIgnoreCase))
                    .Where(i => string.IsNullOrWhiteSpace(itemType) || string.Equals(itemType, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(i.ItemType, itemType, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(i => i.BroadcastSeasons ?? [])
                    .GroupBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(i => i.First())
                    .OrderByDescending(i => i.Year)
                    .ThenByDescending(i => GetSeasonOrder(i.Season))
                    .ThenBy(i => i.Label, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                State.BroadcastSeasonsByFilter[broadcastSeasonsKey] = broadcastSeasons;
            }

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
                IsBrowserCacheReady(document),
                broadcastSeasons);
        }
    }

    /// <summary>
    /// Gets summary values from BrowserItems.
    /// </summary>
    public ThemeBrowserSummary GetBrowserSummary()
    {
        lock (State)
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
        lock (State)
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
        lock (State)
        {
            return IsBrowserCacheReady(LoadDocument());
        }
    }

    /// <summary>
    /// Stores the last browser cache rebuild error for diagnostics.
    /// </summary>
    public void SetBrowserCacheRebuildError(string? error)
    {
        lock (State)
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

        lock (State)
        {
            return LoadDocument().ExtraFiles
                .Where(i => IsCurrentServer(i.ServerKind) && string.Equals(i.Key, plan.Key, StringComparison.OrdinalIgnoreCase))
                .Where(i => plan.OutputTarget == null ||
                            string.Equals(i.LogicalItemId, plan.OutputTarget.LogicalItemId.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
                            (string.IsNullOrWhiteSpace(i.LogicalItemId) && SameDirectory(i.TargetPath, plan.TargetPath)))
                .OrderByDescending(i => plan.OutputTarget != null && string.Equals(i.LogicalItemId, plan.OutputTarget.LogicalItemId.ToString("D"), StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault()
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
        lock (State)
        {
            var document = LoadDocument();
            var logicalItemId = plan.OutputTarget?.LogicalItemId.ToString("D");
            document.ExtraFiles.RemoveAll(i =>
                IsCurrentServer(i.ServerKind) &&
                string.Equals(i.Key, plan.Key, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(i.LogicalItemId, logicalItemId, StringComparison.OrdinalIgnoreCase) ||
                 (string.IsNullOrWhiteSpace(i.LogicalItemId) && string.Equals(i.TargetPath, plan.TargetPath, StringComparison.OrdinalIgnoreCase))));
            document.ExtraFiles.Add(new StoredExtraFile
            {
                ServerKind = ServerKind,
                Key = plan.Key,
                LogicalItemId = logicalItemId,
                OutputRootItemId = plan.OutputTarget?.OutputRootItemId.ToString("D"),
                OutputRootPath = plan.OutputTarget?.OutputRootPath,
                OutputScope = plan.OutputTarget?.Scope.ToString(),
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
    public void UpsertThemeFile(ThemeOutputTarget outputTarget, string themeKey, string fileKind, string path, string source = "TrackedUnknown")
    {
        var logicalItemId = outputTarget.LogicalItemId.ToString("D");
        var info = new FileInfo(path);
        lock (State)
        {
            var document = LoadDocument();
            document.ThemeFiles.RemoveAll(i =>
                IsCurrentServer(i.ServerKind) &&
                string.Equals(i.LogicalItemId, logicalItemId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.ThemeKey, themeKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.FileKind, fileKind, StringComparison.OrdinalIgnoreCase));
            document.ThemeFiles.Add(new StoredThemeFile
            {
                ServerKind = ServerKind,
                LogicalItemId = logicalItemId,
                OutputRootItemId = outputTarget.OutputRootItemId.ToString("D"),
                OutputRootPath = outputTarget.OutputRootPath,
                OutputScope = outputTarget.Scope.ToString(),
                ThemeKey = themeKey,
                FileKind = fileKind,
                Path = path,
                Source = string.IsNullOrWhiteSpace(source) ? "TrackedUnknown" : source,
                ExistsFlag = info.Exists,
                FileSize = info.Exists ? info.Length : null,
                LastWriteTimeUtc = info.Exists ? FormatDate(info.LastWriteTimeUtc) : null,
                UpdatedAtUtc = FormatDate(DateTimeOffset.UtcNow)
            });
            SaveDocument(document);
        }
    }

    public IReadOnlyList<ThemeFileRegistryEntry> GetThemeFiles()
    {
        lock (State)
        {
            return LoadDocument().ThemeFiles
                .Where(file => IsCurrentServer(file.ServerKind) && Guid.TryParse(file.LogicalItemId, out _))
                .Select(file => new ThemeFileRegistryEntry(
                    Guid.Parse(file.LogicalItemId),
                    file.ThemeKey,
                    file.FileKind,
                    file.Path,
                    string.IsNullOrWhiteSpace(file.Source) ? "TrackedUnknown" : file.Source))
                .ToList();
        }
    }

    public void RemoveThemeFilesByPaths(IEnumerable<string> paths)
    {
        var normalized = paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (normalized.Count == 0)
        {
            return;
        }

        lock (State)
        {
            var document = LoadDocument();
            document.ThemeFiles.RemoveAll(file => IsCurrentServer(file.ServerKind) && normalized.Contains(file.Path));
            SaveDocument(document);
        }
    }

    public DownloadFailureRecord? GetDownloadFailure(string url, string destinationPath)
    {
        var normalizedPath = NormalizeDownloadPath(destinationPath);
        lock (State)
        {
            var row = LoadDocument().DownloadFailures.FirstOrDefault(item =>
                IsCurrentServer(item.ServerKind) &&
                string.Equals(item.Url, url, StringComparison.Ordinal) &&
                string.Equals(item.DestinationPath, normalizedPath, DownloadPathComparison));
            return row == null
                ? null
                : new DownloadFailureRecord(
                    row.Url,
                    row.DestinationPath,
                    row.Status,
                    row.AttemptCount,
                    row.LastError,
                    row.LastStatusCode,
                    ParseDate(row.NextRetryUtc),
                    ParseDate(row.UpdatedUtc) ?? DateTimeOffset.MinValue);
        }
    }

    public bool ShouldDeferDownload(string url, string destinationPath, DateTimeOffset utcNow, out DownloadFailureRecord? failure)
    {
        failure = GetDownloadFailure(url, destinationPath);
        return failure?.NextRetryUtc is DateTimeOffset nextRetry && nextRetry > utcNow;
    }

    public void RecordDownloadFailure(
        string url,
        string destinationPath,
        string status,
        string lastError,
        int? lastStatusCode,
        DateTimeOffset nextRetryUtc)
    {
        var normalizedPath = NormalizeDownloadPath(destinationPath);
        lock (State)
        {
            var document = LoadDocument();
            var existing = document.DownloadFailures.FirstOrDefault(item =>
                IsCurrentServer(item.ServerKind) &&
                string.Equals(item.Url, url, StringComparison.Ordinal) &&
                string.Equals(item.DestinationPath, normalizedPath, DownloadPathComparison));
            var attemptCount = (existing?.AttemptCount ?? 0) + 1;
            if (existing != null)
            {
                document.DownloadFailures.Remove(existing);
            }

            var now = DateTimeOffset.UtcNow;
            document.DownloadFailures.RemoveAll(item =>
                ParseDate(item.UpdatedUtc) is DateTimeOffset updated && updated < now.AddDays(-180));
            document.DownloadFailures.Add(new StoredDownloadFailure
            {
                ServerKind = ServerKind,
                Url = url,
                DestinationPath = normalizedPath,
                Status = status,
                AttemptCount = attemptCount,
                LastError = lastError,
                LastStatusCode = lastStatusCode,
                NextRetryUtc = FormatDate(nextRetryUtc),
                UpdatedUtc = FormatDate(now)
            });
            UpsertManagerIssueCore(
                document,
                new ManagerIssueUpsert
                {
                    Category = ManagerIssueCategories.Download,
                    Severity = string.Equals(status, DownloadFailureStatuses.PermanentFailed, StringComparison.OrdinalIgnoreCase)
                        ? ManagerIssueSeverities.Error
                        : ManagerIssueSeverities.Warning,
                    State = nextRetryUtc > now ? ManagerIssueStates.Deferred : ManagerIssueStates.Open,
                    Title = string.Equals(status, DownloadFailureStatuses.PermanentFailed, StringComparison.OrdinalIgnoreCase)
                        ? "Download failed permanently"
                        : "Download deferred after a transient failure",
                    Message = lastError,
                    Operation = "Download",
                    Url = url,
                    DestinationPath = normalizedPath,
                    HttpStatusCode = lastStatusCode,
                    Stage = "MediaTransfer",
                    SuggestedAction = "Review the URL or run synchronization again after the retry time.",
                    FingerprintSeed = "download|" + url + "|" + normalizedPath,
                    NextRetryUtc = nextRetryUtc,
                },
                now);
            SaveDocument(document);
        }
    }

    // A genuine failure event always persists; the manager issue is a side effect.
    public void RemoveDownloadFailure(string url, string destinationPath)
    {
        var normalizedPath = NormalizeDownloadPath(destinationPath);
        lock (State)
        {
            var document = LoadDocument();
            var removed = document.DownloadFailures.RemoveAll(item =>
                IsCurrentServer(item.ServerKind) &&
                string.Equals(item.Url, url, StringComparison.Ordinal) &&
                string.Equals(item.DestinationPath, normalizedPath, DownloadPathComparison));
            if (removed > 0)
            {
                ResolveManagerIssueCore(document, BuildManagerIssueFingerprint("download|" + url + "|" + normalizedPath), DateTimeOffset.UtcNow);
                SaveDocument(document);
            }
        }
    }

    public ManagerIssuePage QueryManagerIssues(
        int? startIndex,
        int? limit,
        string? state,
        string? category,
        string? severity,
        string? searchTerm)
    {
        lock (State)
        {
            var document = LoadDocument();
            var changed = EnsureDownloadFailuresHaveIssues(document);
            changed |= PruneManagerIssues(document, DateTimeOffset.UtcNow);

            var normalizedStart = Math.Max(0, startIndex ?? 0);
            var normalizedLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
            var issues = document.ManagerIssues
                .Where(i => IsCurrentServer(i.ServerKind))
                .Where(i => MatchesIssueFilter(i, state, category, severity, searchTerm))
                .OrderBy(i => GetIssueStateOrder(i.State))
                .ThenByDescending(i => ParseDate(i.LastSeenUtc) ?? DateTimeOffset.MinValue)
                .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var allCurrent = document.ManagerIssues.Where(i => IsCurrentServer(i.ServerKind)).ToList();
            if (changed)
            {
                SaveDocument(document);
            }

            return new ManagerIssuePage(
                issues.Skip(normalizedStart).Take(normalizedLimit).Select(ToManagerIssueRecord).ToList(),
                issues.Count,
                normalizedStart,
                normalizedLimit,
                BuildManagerIssueSummary(allCurrent));
        }
    }

    public ManagerIssueSummary GetManagerIssueSummary()
    {
        lock (State)
        {
            var document = LoadDocument();
            var changed = EnsureDownloadFailuresHaveIssues(document);
            changed |= PruneManagerIssues(document, DateTimeOffset.UtcNow);
            if (changed)
            {
                SaveDocument(document);
            }

            return BuildManagerIssueSummary(document.ManagerIssues.Where(i => IsCurrentServer(i.ServerKind)).ToList());
        }
    }

    public ManagerIssueRecord RecordManagerIssue(ManagerIssueUpsert issue)
    {
        lock (State)
        {
            var document = LoadDocument();
            var stored = UpsertManagerIssueCore(document, issue, DateTimeOffset.UtcNow).Issue;
            SaveDocument(document);
            return ToManagerIssueRecord(stored);
        }
    }

    public bool SetManagerIssueState(string issueId, string state)
    {
        if (!IsValidManagerIssueState(state))
        {
            return false;
        }

        lock (State)
        {
            var document = LoadDocument();
            var issue = document.ManagerIssues.FirstOrDefault(i => IsCurrentServer(i.ServerKind) && string.Equals(i.Id, issueId, StringComparison.OrdinalIgnoreCase));
            if (issue == null)
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            issue.State = state;
            issue.ResolvedAtUtc = string.Equals(state, ManagerIssueStates.Resolved, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(state, ManagerIssueStates.Ignored, StringComparison.OrdinalIgnoreCase)
                ? FormatDate(now)
                : null;
            issue.UpdatedAtUtc = FormatDate(now);
            SaveDocument(document);
            return true;
        }
    }

    public bool DeleteManagerIssue(string issueId)
    {
        lock (State)
        {
            var document = LoadDocument();
            var removed = document.ManagerIssues.RemoveAll(i => IsCurrentServer(i.ServerKind) && string.Equals(i.Id, issueId, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return false;
            }

            SaveDocument(document);
            return true;
        }
    }

    private CacheDocument LoadDocument()
    {
        if (State.Cache != null)
        {
            return State.Cache;
        }

        Directory.CreateDirectory(_pathProvider.GetPluginDataDirectory());
        RecoverFromIncompleteSave();
        if (!File.Exists(DatabasePath))
        {
            State.Cache = new CacheDocument();
            SaveDocument(State.Cache);
            return State.Cache;
        }

        try
        {
            State.Cache = JsonSerializer.Deserialize<CacheDocument>(File.ReadAllText(DatabasePath), _jsonOptions) ?? new CacheDocument();
            State.Cache.SchemaVersion = Math.Max(State.Cache.SchemaVersion, CurrentSchemaVersion);
            State.Cache.ExtraFiles ??= [];
            State.Cache.BrowserItems ??= [];
            State.Cache.ThemeFiles ??= [];
            State.Cache.LibrarySyncState ??= [];
            State.Cache.ServerCacheState ??= [];
            State.Cache.SeasonMetadataStates ??= [];
            State.Cache.DownloadFailures ??= [];
            State.Cache.ManagerIssues ??= [];
            foreach (var seasonMetadataState in State.Cache.SeasonMetadataStates)
            {
                seasonMetadataState.ManagedTags ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                seasonMetadataState.CollectionMemberships ??= [];
                seasonMetadataState.BroadcastSeasons ??= [];
            }

            foreach (var themeFile in State.Cache.ThemeFiles)
            {
                if (string.IsNullOrWhiteSpace(themeFile.LogicalItemId))
                {
                    themeFile.LogicalItemId = themeFile.ItemId ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(themeFile.Source))
                {
                    themeFile.Source = "TrackedUnknown";
                }
            }

            var invalidBrowserRows = State.Cache.BrowserItems.Count(i => !Guid.TryParse(i.ItemId, out _));
            if (invalidBrowserRows > 0)
            {
                _logger?.LogWarning(
                    "The AnimeThemes cache contains {Count} browser rows with invalid item ids; they are ignored until the next cache rebuild.",
                    invalidBrowserRows);
            }

            return State.Cache;
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "The AnimeThemes cache file {Path} could not be parsed; quarantining it and starting a new cache.", DatabasePath);
            QuarantineCacheFile();
        }
        catch (IOException ex)
        {
            _logger?.LogWarning(ex, "The AnimeThemes cache file {Path} could not be read; quarantining it and starting a new cache.", DatabasePath);
            QuarantineCacheFile();
        }

        State.Cache = new CacheDocument();
        SaveDocument(State.Cache);
        return State.Cache;
    }

    private bool EnsureDownloadFailuresHaveIssues(CacheDocument document)
    {
        var now = DateTimeOffset.UtcNow;
        var changed = false;
        foreach (var failure in document.DownloadFailures.Where(i => IsCurrentServer(i.ServerKind)))
        {
            changed |= UpsertManagerIssueCore(
                document,
                new ManagerIssueUpsert
                {
                    Category = ManagerIssueCategories.Download,
                    Severity = string.Equals(failure.Status, DownloadFailureStatuses.PermanentFailed, StringComparison.OrdinalIgnoreCase)
                        ? ManagerIssueSeverities.Error
                        : ManagerIssueSeverities.Warning,
                    State = ParseDate(failure.NextRetryUtc) is DateTimeOffset nextRetry && nextRetry > now
                        ? ManagerIssueStates.Deferred
                        : ManagerIssueStates.Open,
                    Title = string.Equals(failure.Status, DownloadFailureStatuses.PermanentFailed, StringComparison.OrdinalIgnoreCase)
                        ? "Download failed permanently"
                        : "Download deferred after a transient failure",
                    Message = failure.LastError,
                    Operation = "Download",
                    Url = failure.Url,
                    DestinationPath = failure.DestinationPath,
                    HttpStatusCode = failure.LastStatusCode,
                    Stage = "MediaTransfer",
                    SuggestedAction = "Review the URL or run synchronization again after the retry time.",
                    FingerprintSeed = "download|" + failure.Url + "|" + failure.DestinationPath,
                    NextRetryUtc = ParseDate(failure.NextRetryUtc),
                },
                ParseDate(failure.UpdatedUtc) ?? now,
                countOccurrence: false,
                preserveManualState: true).Changed;
        }

        return changed;
    }

    private (StoredManagerIssue Issue, bool Changed) UpsertManagerIssueCore(CacheDocument document, ManagerIssueUpsert issue, DateTimeOffset now, bool countOccurrence = true, bool preserveManualState = false)
    {
        var fingerprint = BuildManagerIssueFingerprint(BuildManagerIssueFingerprintSeed(issue));
        var existing = document.ManagerIssues.FirstOrDefault(i =>
            IsCurrentServer(i.ServerKind) &&
            string.Equals(i.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase));
        var isNew = existing == null;
        if (existing == null)
        {
            existing = new StoredManagerIssue
            {
                ServerKind = ServerKind,
                Id = fingerprint,
                Fingerprint = fingerprint,
                FirstSeenUtc = FormatDate(now),
                OccurrenceCount = countOccurrence ? 0 : 1,
            };
            document.ManagerIssues.Add(existing);
        }

        // Snapshot the user-visible projection so callers on read paths can skip the
        // full-document save when re-projecting an unchanged issue (e.g. a Manager
        // tab poll that finds the same still-deferred download failure).
        var before = isNew ? null : ToManagerIssueRecord(existing);

        // Re-projecting a derived issue (e.g. from a still-failing download) must not
        // clobber a manual Ignore/Resolve the user applied; that override sticks until
        // the underlying failure clears or the user reopens it.
        var keepManualState = preserveManualState && !isNew &&
            (string.Equals(existing.State, ManagerIssueStates.Ignored, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(existing.State, ManagerIssueStates.Resolved, StringComparison.OrdinalIgnoreCase));
        var manualState = existing.State;
        var manualResolvedAtUtc = existing.ResolvedAtUtc;

        existing.Category = ClampText(NormalizeIssueValue(issue.Category, ManagerIssueCategories.Task), 80);
        existing.Severity = ClampText(NormalizeIssueValue(issue.Severity, ManagerIssueSeverities.Error), 40);
        existing.State = ClampText(NormalizeIssueValue(issue.State, ManagerIssueStates.Open), 40);
        existing.Title = ClampText(issue.Title, 200);
        existing.Message = ClampText(issue.Message, 2048);
        existing.TargetName = ClampNullableText(issue.TargetName, 300);
        existing.ItemId = ClampNullableText(issue.ItemId, 80);
        existing.SeriesItemId = ClampNullableText(issue.SeriesItemId, 80);
        existing.SeasonItemId = ClampNullableText(issue.SeasonItemId, 80);
        existing.RowId = ClampNullableText(issue.RowId, 160);
        existing.TaskName = ClampNullableText(issue.TaskName, 160);
        existing.Operation = ClampNullableText(issue.Operation, 160);
        existing.Url = ClampNullableText(issue.Url, 2048);
        existing.DestinationPath = ClampNullableText(issue.DestinationPath, 1024);
        existing.HttpStatusCode = issue.HttpStatusCode;
        existing.Stage = ClampNullableText(issue.Stage, 160);
        existing.SuggestedAction = ClampNullableText(issue.SuggestedAction, 500);
        if (countOccurrence)
        {
            existing.OccurrenceCount++;
        }

        existing.LastSeenUtc = FormatDate(now);
        existing.NextRetryUtc = issue.NextRetryUtc.HasValue ? FormatDate(issue.NextRetryUtc.Value) : null;
        if (keepManualState)
        {
            existing.State = manualState;
            existing.ResolvedAtUtc = manualResolvedAtUtc;
        }
        else
        {
            existing.ResolvedAtUtc = null;
        }

        existing.UpdatedAtUtc = FormatDate(now);
        var changed = isNew || !ToManagerIssueRecord(existing).Equals(before);
        return (existing, changed);
    }

    private bool ResolveManagerIssueCore(CacheDocument document, string fingerprint, DateTimeOffset now)
    {
        var issue = document.ManagerIssues.FirstOrDefault(i =>
            IsCurrentServer(i.ServerKind) &&
            string.Equals(i.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase));
        if (issue == null)
        {
            return false;
        }

        issue.State = ManagerIssueStates.Resolved;
        issue.ResolvedAtUtc = FormatDate(now);
        issue.UpdatedAtUtc = FormatDate(now);
        return true;
    }

    private static string BuildManagerIssueFingerprintSeed(ManagerIssueUpsert issue)
    {
        if (!string.IsNullOrWhiteSpace(issue.FingerprintSeed))
        {
            return issue.FingerprintSeed.Trim();
        }

        return string.Join(
            "|",
            issue.Category,
            issue.Operation,
            issue.TaskName,
            issue.ItemId,
            issue.SeriesItemId,
            issue.SeasonItemId,
            issue.RowId,
            issue.Url,
            issue.DestinationPath,
            issue.Stage,
            issue.Message);
    }

    private static string BuildManagerIssueFingerprint(string seed)
    {
#if NETSTANDARD2_1
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
#else
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
#endif
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static bool MatchesIssueFilter(StoredManagerIssue issue, string? state, string? category, string? severity, string? searchTerm)
    {
        if (!MatchesOptionalFilter(issue.State, state))
        {
            return false;
        }

        if (!MatchesOptionalFilter(issue.Category, category))
        {
            return false;
        }

        if (!MatchesOptionalFilter(issue.Severity, severity))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            if (!Contains(issue.Title, term) &&
                !Contains(issue.Message, term) &&
                !Contains(issue.TargetName, term) &&
                !Contains(issue.Url, term) &&
                !Contains(issue.DestinationPath, term) &&
                !Contains(issue.TaskName, term) &&
                !Contains(issue.Operation, term))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesOptionalFilter(string? value, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return filter
            .Split(IssueFilterSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(i => i.Trim())
            .Any(i => string.Equals(value, i, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetIssueStateOrder(string? state)
    {
        return state switch
        {
            ManagerIssueStates.Open => 0,
            ManagerIssueStates.Deferred => 1,
            ManagerIssueStates.Ignored => 2,
            ManagerIssueStates.Resolved => 3,
            _ => 4,
        };
    }

    private static ManagerIssueRecord ToManagerIssueRecord(StoredManagerIssue issue)
    {
        return new ManagerIssueRecord(
            issue.Id,
            issue.Fingerprint,
            issue.Category,
            issue.Severity,
            issue.State,
            issue.Title,
            issue.Message,
            issue.TargetName,
            issue.ItemId,
            issue.SeriesItemId,
            issue.SeasonItemId,
            issue.RowId,
            issue.TaskName,
            issue.Operation,
            issue.Url,
            issue.DestinationPath,
            issue.HttpStatusCode,
            issue.Stage,
            issue.SuggestedAction,
            issue.OccurrenceCount,
            issue.FirstSeenUtc,
            issue.LastSeenUtc,
            issue.NextRetryUtc,
            issue.ResolvedAtUtc);
    }

    private static ManagerIssueSummary BuildManagerIssueSummary(IReadOnlyCollection<StoredManagerIssue> issues)
    {
        return new ManagerIssueSummary(
            issues.Count(i => string.Equals(i.State, ManagerIssueStates.Open, StringComparison.OrdinalIgnoreCase)),
            issues.Count(i => string.Equals(i.State, ManagerIssueStates.Deferred, StringComparison.OrdinalIgnoreCase)),
            issues.Count(i => string.Equals(i.State, ManagerIssueStates.Ignored, StringComparison.OrdinalIgnoreCase)),
            issues.Count(i => string.Equals(i.State, ManagerIssueStates.Resolved, StringComparison.OrdinalIgnoreCase)),
            issues.Count(i => string.Equals(i.Severity, ManagerIssueSeverities.Error, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(i.Severity, ManagerIssueSeverities.Critical, StringComparison.OrdinalIgnoreCase)),
            issues.Count(i => string.Equals(i.Severity, ManagerIssueSeverities.Warning, StringComparison.OrdinalIgnoreCase)),
            issues.Count(i => string.Equals(i.Category, ManagerIssueCategories.Download, StringComparison.OrdinalIgnoreCase)),
            issues.Count(i => string.Equals(i.Category, ManagerIssueCategories.Mapping, StringComparison.OrdinalIgnoreCase)),
            issues.Count(i => string.Equals(i.Category, ManagerIssueCategories.SeasonAutomation, StringComparison.OrdinalIgnoreCase)),
            issues.Count(i => string.Equals(i.Category, ManagerIssueCategories.Task, StringComparison.OrdinalIgnoreCase)),
            issues.Count(i => string.Equals(i.Category, ManagerIssueCategories.Import, StringComparison.OrdinalIgnoreCase)),
            issues.Count(i => string.Equals(i.Category, ManagerIssueCategories.Maintenance, StringComparison.OrdinalIgnoreCase)));
    }

    private bool PruneManagerIssues(CacheDocument document, DateTimeOffset now)
    {
        var cutoff = now.AddDays(-30);
        var changed = document.ManagerIssues.RemoveAll(i =>
            (string.Equals(i.State, ManagerIssueStates.Resolved, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(i.State, ManagerIssueStates.Ignored, StringComparison.OrdinalIgnoreCase)) &&
            ParseDate(i.ResolvedAtUtc) is DateTimeOffset resolved &&
            resolved < cutoff) > 0;

        // Bound the row count per server so a burst of transient errors cannot grow
        // the shared cache document without limit. Drop terminal (Resolved/Ignored)
        // issues first, oldest last-seen first; active Open/Deferred issues are only
        // touched as a last resort.
        var serverIssues = document.ManagerIssues.Where(i => IsCurrentServer(i.ServerKind)).ToList();
        if (serverIssues.Count > ManagerIssueLimit)
        {
            var toRemove = serverIssues
                .OrderBy(i => IsTerminalIssueState(i.State) ? 0 : 1)
                .ThenBy(i => ParseDate(i.LastSeenUtc) ?? DateTimeOffset.MinValue)
                .Take(serverIssues.Count - ManagerIssueLimit)
                .ToHashSet();
            changed |= document.ManagerIssues.RemoveAll(toRemove.Contains) > 0;
        }

        return changed;
    }

    private static bool IsTerminalIssueState(string? state)
    {
        return string.Equals(state, ManagerIssueStates.Resolved, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(state, ManagerIssueStates.Ignored, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidManagerIssueState(string state)
    {
        return string.Equals(state, ManagerIssueStates.Open, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(state, ManagerIssueStates.Deferred, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(state, ManagerIssueStates.Ignored, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(state, ManagerIssueStates.Resolved, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIssueValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string ClampText(string? value, int maxLength)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string? ClampNullableText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ClampText(value, maxLength);
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
            // File.Replace keeps the destination present at all times, unlike Delete+Move.
            File.Replace(tempPath, DatabasePath, null);
        }
        else
        {
            File.Move(tempPath, DatabasePath);
        }

        State.Cache = document;
        State.BroadcastSeasonsByFilter.Clear();
        State.QueryResultsByFilter.Clear();
    }

    /// <summary>
    /// Completes a save that was interrupted between writing the temp file and
    /// replacing the destination (the pre-Replace Delete+Move scheme could leave
    /// only the temp file behind). A temp file with unreadable JSON is quarantined.
    /// A temp file that is locked by another writer (external process, antivirus,
    /// or backup scan) is left alone: recovery is opportunistic housekeeping and
    /// runs again on the next cold load, so failing the whole load here would turn
    /// a transient lock into a plugin error.
    /// </summary>
    private void RecoverFromIncompleteSave()
    {
        var tempPath = DatabasePath + ".tmp";
        if (!File.Exists(tempPath))
        {
            return;
        }

        try
        {
            if (File.Exists(DatabasePath))
            {
                // The destination survived, so the temp file is a leftover partial write.
                QuarantineFile(tempPath);
                _logger?.LogWarning("Quarantined a leftover AnimeThemes cache temp file next to {Path}.", DatabasePath);
                return;
            }

            _ = JsonSerializer.Deserialize<CacheDocument>(File.ReadAllText(tempPath), _jsonOptions);
            File.Move(tempPath, DatabasePath);
            _logger?.LogWarning("Recovered the AnimeThemes cache at {Path} from an interrupted save.", DatabasePath);
        }
        catch (JsonException ex)
        {
            try
            {
                QuarantineFile(tempPath);
                _logger?.LogWarning(ex, "Quarantined an unreadable AnimeThemes cache temp file next to {Path}.", DatabasePath);
            }
            catch (IOException)
            {
                // Locked unreadable temp file: leave it for the next cold load.
                _logger?.LogWarning("The AnimeThemes cache temp file next to {Path} is locked; recovery skipped until the next load.", DatabasePath);
            }
        }
        catch (IOException ex)
        {
            // Locked temp file: skip recovery for now; the main document load proceeds.
            _logger?.LogWarning(ex, "The AnimeThemes cache temp file next to {Path} is locked; recovery skipped until the next load.", DatabasePath);
        }
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
            LastRefreshedUtc = FormatDate(record.LastRefreshedUtc == default ? DateTimeOffset.UtcNow : record.LastRefreshedUtc),
            BroadcastSeasons = record.BroadcastSeasons ?? [],
            SeasonSummaries = record.SeasonSummaries ?? [],
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
            string.Equals(item.LinkStatus, "Manual", StringComparison.OrdinalIgnoreCase),
            (item.BroadcastSeasons ?? []).Select(i => i.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            item.SeasonSummaries ?? []);
    }

    private static bool MatchesBrowserQuery(StoredBrowserItem item, string? libraryId, string? searchTerm, string? itemType, string? linkFilter, string? savedFilter, string? broadcastSeason)
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

        if (!string.IsNullOrWhiteSpace(broadcastSeason) &&
            !string.Equals(broadcastSeason, "all", StringComparison.OrdinalIgnoreCase) &&
            !(item.BroadcastSeasons ?? []).Any(i => string.Equals(i.Key, broadcastSeason, StringComparison.OrdinalIgnoreCase)))
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

    private static int GetSeasonOrder(string season) => season.ToLowerInvariant() switch
    {
        "winter" => 0,
        "spring" => 1,
        "summer" => 2,
        "fall" => 3,
        _ => -1,
    };

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
        QuarantineFile(DatabasePath);
    }

    private static void QuarantineFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        File.Move(path, path + ".corrupt-" + timestamp);
    }

    private static bool Contains(string? value, string term)
    {
        return value?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool SameDirectory(string? leftPath, string? rightPath)
    {
        if (string.IsNullOrWhiteSpace(leftPath) || string.IsNullOrWhiteSpace(rightPath))
        {
            return false;
        }

        var leftDirectory = Path.GetDirectoryName(Path.GetFullPath(leftPath))?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rightDirectory = Path.GetDirectoryName(Path.GetFullPath(rightPath))?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(leftDirectory, rightDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeDownloadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (ArgumentException)
        {
            return path.Trim();
        }
        catch (NotSupportedException)
        {
            return path.Trim();
        }
        catch (PathTooLongException)
        {
            return path.Trim();
        }
    }

    private static string FormatDate(DateTimeOffset date)
    {
        return date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateTime date)
    {
        return DateTime.SpecifyKind(date, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
    }

    // Instances of this private type double as the lock object for all stores that
    // point at the same cache file; nothing outside this class can observe or lock it.
    private sealed class SharedState
    {
        public CacheDocument? Cache { get; set; }

        // Memoized QueryBrowserItems aggregations, cleared on every document write.
        public Dictionary<string, List<BroadcastSeasonValue>> BroadcastSeasonsByFilter { get; } = new(StringComparer.OrdinalIgnoreCase);

        // Memoized filtered+sorted QueryBrowserItems results, cleared on every document write.
        public Dictionary<string, List<StoredBrowserItem>> QueryResultsByFilter { get; } = new(StringComparer.Ordinal);
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

        public List<SeasonMetadataState> SeasonMetadataStates { get; set; } = [];

        public List<StoredDownloadFailure> DownloadFailures { get; set; } = [];

        public List<StoredManagerIssue> ManagerIssues { get; set; } = [];
    }

    private sealed class StoredManagerIssue
    {
        public string ServerKind { get; set; } = string.Empty;

        public string Id { get; set; } = string.Empty;

        public string Fingerprint { get; set; } = string.Empty;

        public string Category { get; set; } = ManagerIssueCategories.Task;

        public string Severity { get; set; } = ManagerIssueSeverities.Error;

        public string State { get; set; } = ManagerIssueStates.Open;

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? TargetName { get; set; }

        public string? ItemId { get; set; }

        public string? SeriesItemId { get; set; }

        public string? SeasonItemId { get; set; }

        public string? RowId { get; set; }

        public string? TaskName { get; set; }

        public string? Operation { get; set; }

        public string? Url { get; set; }

        public string? DestinationPath { get; set; }

        public int? HttpStatusCode { get; set; }

        public string? Stage { get; set; }

        public string? SuggestedAction { get; set; }

        public int OccurrenceCount { get; set; }

        public string FirstSeenUtc { get; set; } = string.Empty;

        public string LastSeenUtc { get; set; } = string.Empty;

        public string? NextRetryUtc { get; set; }

        public string? ResolvedAtUtc { get; set; }

        public string UpdatedAtUtc { get; set; } = string.Empty;
    }

    private sealed class StoredDownloadFailure
    {
        public string ServerKind { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public string DestinationPath { get; set; } = string.Empty;

        public string Status { get; set; } = DownloadFailureStatuses.TransientFailed;

        public int AttemptCount { get; set; }

        public string LastError { get; set; } = string.Empty;

        public int? LastStatusCode { get; set; }

        public string? NextRetryUtc { get; set; }

        public string UpdatedUtc { get; set; } = string.Empty;
    }

    private sealed class StoredExtraFile
    {
        public string ServerKind { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public string? LogicalItemId { get; set; }

        public string? OutputRootItemId { get; set; }

        public string? OutputRootPath { get; set; }

        public string? OutputScope { get; set; }

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

        public List<BroadcastSeasonValue> BroadcastSeasons { get; set; } = [];

        public List<SeasonSummary> SeasonSummaries { get; set; } = [];
    }

    private sealed class StoredThemeFile
    {
        public string ServerKind { get; set; } = string.Empty;

        public string LogicalItemId { get; set; } = string.Empty;

        public string? OutputRootItemId { get; set; }

        public string? OutputRootPath { get; set; }

        public string? OutputScope { get; set; }

        // Schema v1 compatibility. New writes use LogicalItemId.
        public string? ItemId { get; set; }

        public string ThemeKey { get; set; } = string.Empty;

        public string FileKind { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string Source { get; set; } = "TrackedUnknown";

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
