using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;
using SQLitePCL.pretty;

namespace Emby.Plugin.AnimeThemesSync.ScheduledTasks;

/// <summary>
/// Emby-hosted SQLite storage for Season Finder data.
/// </summary>
/// <remarks>
/// Emby ships SQLitePCL.pretty backed by its own SQLitePCLRawEx runtime. This store deliberately
/// references only the host's SQLitePCL.pretty contract and keeps the plugin package free of a
/// competing Microsoft.Data.Sqlite/SQLitePCLRaw runtime.
/// </remarks>
internal sealed class EmbySeasonFinderDataStore : ISeasonFinderDataStore
{
    private const int DefaultLimit = 80;
    private const int MaxLimit = 100;
    private const int SearchCacheLimit = 200;
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IAnimeThemesDataPathProvider _pathProvider;
    private readonly IAnimeThemesServerIdentityProvider _serverIdentity;
    private readonly object _syncRoot = new();
    private StoreDocument? _document;

    public EmbySeasonFinderDataStore(IAnimeThemesDataPathProvider pathProvider, IAnimeThemesServerIdentityProvider serverIdentity)
    {
        _pathProvider = pathProvider;
        _serverIdentity = serverIdentity;
    }

    public string DatabasePath => Path.Combine(_pathProvider.GetPluginDataDirectory(), "animethemes-sync.db");

    private string ServerKind => _serverIdentity.ServerKind;

    public void EnsureInitialized()
    {
        lock (_syncRoot)
        {
            _ = LoadDocument();
        }
    }

    public void MigrateLegacyMappings(IEnumerable<SeasonThemeMapping>? mappings)
    {
        if (mappings == null)
        {
            return;
        }

        lock (_syncRoot)
        {
            var document = LoadDocument();
            if (document.LegacyMappingsMigrated)
            {
                return;
            }

            document.Mappings = DeduplicateMappings(mappings).ToList();
            document.LegacyMappingsMigrated = true;
            SaveDocument(document);
        }
    }

    public List<SeasonThemeMapping> GetSeasonThemeMappings()
    {
        lock (_syncRoot)
        {
            return LoadDocument().Mappings.Select(CloneMapping).ToList();
        }
    }

    public void ReplaceSeasonThemeMappings(IEnumerable<SeasonThemeMapping> mappings, string source)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            document.Mappings = DeduplicateMappings(mappings).ToList();
            SaveDocument(document);
        }
    }

    public void ReplaceRows(IEnumerable<SeasonFinderRowRecord> records)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            var now = FormatDate(DateTimeOffset.UtcNow);
            document.Rows = records
                .GroupBy(record => record.Row.SeasonItemId)
                .Select(group => group.Last())
                .ToList();
            foreach (var record in document.Rows)
            {
                record.UpdatedAtUtc = now;
            }

            document.CacheReady = true;
            document.CacheVersion = now;
            document.LastFullScanUtc = now;
            document.LastError = null;
            SaveDocument(document);
        }
    }

    public void UpsertRow(SeasonFinderRowRecord record)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            document.Rows.RemoveAll(existing => existing.Row.SeasonItemId == record.Row.SeasonItemId);
            record.UpdatedAtUtc = FormatDate(DateTimeOffset.UtcNow);
            document.Rows.Add(record);
            SaveDocument(document);
        }
    }

    public SeasonFinderItemsPage QueryRows(
        string? libraryId,
        int? startIndex,
        int? limit,
        string? searchTerm,
        string? status,
        string? sortBy,
        string? sortOrder)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            var normalizedStart = Math.Max(0, startIndex ?? 0);
            var normalizedLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
            IEnumerable<SeasonFinderRowRecord> filtered = document.Rows;
            if (!string.IsNullOrWhiteSpace(libraryId))
            {
                filtered = filtered.Where(record => string.Equals(record.LibraryId, libraryId.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                filtered = filtered.Where(record => MatchesSearch(record.Row, term));
            }

            var normalizedStatus = status?.Trim();
            if (string.Equals(normalizedStatus, "auto", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(record => record.Row.Status is "Auto" or "Direct" or "Series");
            }
            else if (!string.IsNullOrWhiteSpace(normalizedStatus) && !string.Equals(normalizedStatus, "all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(record => string.Equals(record.Row.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase));
            }

            var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(sortOrder, "descending", StringComparison.OrdinalIgnoreCase);
            filtered = SortRows(filtered, sortBy, descending);
            var materialized = filtered.ToList();
            return new SeasonFinderItemsPage(
                materialized.Skip(normalizedStart).Take(normalizedLimit).Select(record => record.Row).ToList(),
                materialized.Count,
                normalizedStart,
                normalizedLimit,
                document.CacheVersion,
                document.CacheReady);
        }
    }

    public IReadOnlyList<SeasonThemeMappingRow> GetAllRows()
    {
        lock (_syncRoot)
        {
            return SortRows(LoadDocument().Rows, "seriesName", false).Select(record => record.Row).ToList();
        }
    }

    public bool IsCacheReady()
    {
        lock (_syncRoot)
        {
            return LoadDocument().CacheReady;
        }
    }

    public SeasonFinderStorageStatus GetStorageStatus()
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            var file = new FileInfo(DatabasePath);
            return new SeasonFinderStorageStatus(
                DatabasePath,
                file.Exists ? file.Length : 0,
                document.Rows.Count,
                document.CacheVersion,
                document.CacheReady,
                document.LastFullScanUtc,
                document.LastError);
        }
    }

    public void SetRebuildError(string? error)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            document.LastError = error;
            SaveDocument(document);
        }
    }

    public void ClearCache()
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            document.Rows.Clear();
            document.SearchCache.Clear();
            document.CacheReady = false;
            document.CacheVersion = string.Empty;
            document.LastFullScanUtc = null;
            document.LastError = null;
            SaveDocument(document);
        }
    }

    public bool TryGetSearch(string query, int? year, out string json)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            var key = BuildQueryKey(query, year);
            if (document.SearchCache.TryGetValue(key, out var entry) &&
                DateTimeOffset.TryParse(entry.ExpiresAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expires) &&
                expires > DateTimeOffset.UtcNow)
            {
                json = entry.ResultJson;
                return true;
            }

            document.SearchCache.Remove(key);
            json = string.Empty;
            return false;
        }
    }

    public void SetSearch(string query, int? year, string json)
    {
        lock (_syncRoot)
        {
            var document = LoadDocument();
            var now = DateTimeOffset.UtcNow;
            document.SearchCache[BuildQueryKey(query, year)] = new SearchCacheEntry
            {
                ResultJson = json,
                CreatedAtUtc = FormatDate(now),
                ExpiresAtUtc = FormatDate(now.Add(SearchCacheTtl)),
            };
            var expired = document.SearchCache
                .Where(pair => !DateTimeOffset.TryParse(pair.Value.ExpiresAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expires) || expires <= now)
                .Select(pair => pair.Key)
                .ToList();
            foreach (var key in expired)
            {
                document.SearchCache.Remove(key);
            }

            foreach (var key in document.SearchCache
                         .OrderByDescending(pair => pair.Value.CreatedAtUtc, StringComparer.Ordinal)
                         .Skip(SearchCacheLimit)
                         .Select(pair => pair.Key)
                         .ToList())
            {
                document.SearchCache.Remove(key);
            }

            SaveDocument(document);
        }
    }

    private StoreDocument LoadDocument()
    {
        if (_document != null)
        {
            return _document;
        }

        Directory.CreateDirectory(_pathProvider.GetPluginDataDirectory());
        using var connection = OpenConnection();
        connection.Execute("PRAGMA journal_mode=WAL;");
        connection.Execute("PRAGMA busy_timeout=5000;");
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS AnimeThemesSyncState (
                ServerKind TEXT NOT NULL PRIMARY KEY,
                DocumentJson TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            """);
        using var statement = connection.PrepareStatement("SELECT DocumentJson FROM AnimeThemesSyncState WHERE ServerKind = $serverKind;");
        statement.BindParameters["$serverKind"].Bind(ServerKind);
        if (statement.MoveNext())
        {
            _document = JsonSerializer.Deserialize<StoreDocument>(statement.Current.GetString(0), JsonOptions) ?? new StoreDocument();
        }
        else
        {
            _document = new StoreDocument();
            SaveDocument(_document, connection);
        }

        NormalizeDocument(_document);
        return _document;
    }

    private void SaveDocument(StoreDocument document, IDatabaseConnection? existingConnection = null)
    {
        var ownsConnection = existingConnection == null;
        var connection = existingConnection ?? OpenConnection();
        var transactionStarted = false;
        try
        {
            connection.Execute("BEGIN IMMEDIATE;");
            transactionStarted = true;
            using var statement = connection.PrepareStatement("""
                INSERT INTO AnimeThemesSyncState (ServerKind, DocumentJson, UpdatedAtUtc)
                VALUES ($serverKind, $json, $updated)
                ON CONFLICT(ServerKind) DO UPDATE SET DocumentJson = excluded.DocumentJson, UpdatedAtUtc = excluded.UpdatedAtUtc;
                """);
            statement.BindParameters["$serverKind"].Bind(ServerKind);
            statement.BindParameters["$json"].Bind(JsonSerializer.Serialize(document, JsonOptions));
            statement.BindParameters["$updated"].Bind(FormatDate(DateTimeOffset.UtcNow));
            _ = statement.MoveNext();
            connection.Execute("COMMIT;");
            transactionStarted = false;
            _document = document;
        }
        catch
        {
            if (transactionStarted)
            {
                connection.Execute("ROLLBACK;");
            }

            throw;
        }
        finally
        {
            if (ownsConnection)
            {
                connection.Dispose();
            }
        }
    }

    private SQLiteDatabaseConnection OpenConnection()
    {
        return SQLite3.Open(DatabasePath, ConnectionFlags.ReadWrite | ConnectionFlags.Create, null, false);
    }

    private static IEnumerable<SeasonFinderRowRecord> SortRows(IEnumerable<SeasonFinderRowRecord> rows, string? sortBy, bool descending)
    {
        Func<SeasonFinderRowRecord, object?> selector = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "seasonnumber" => record => record.Row.SeasonNumber,
            "status" => record => record.Row.Status,
            "updated" => record => record.UpdatedAtUtc,
            _ => record => record.Row.SeriesName,
        };
        var sorted = descending
            ? rows.OrderByDescending(selector, ObjectComparer.Instance)
            : rows.OrderBy(selector, ObjectComparer.Instance);
        return sorted.ThenBy(record => record.Row.SeriesName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Row.SeasonNumber)
            .ThenBy(record => record.Row.SeasonName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesSearch(SeasonThemeMappingRow row, string term)
    {
        return new[]
        {
            row.SeriesName,
            row.SeasonName,
            row.AnimeName,
            row.AnimeThemesSlug,
            row.AniListId?.ToString(CultureInfo.InvariantCulture),
            row.MyAnimeListId?.ToString(CultureInfo.InvariantCulture),
        }.Any(value => value?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static IEnumerable<SeasonThemeMapping> DeduplicateMappings(IEnumerable<SeasonThemeMapping> mappings)
    {
        return mappings
            .Where(mapping => BuildMappingKey(mapping) != null)
            .GroupBy(mapping => BuildMappingKey(mapping)!, StringComparer.OrdinalIgnoreCase)
            .Select(group => CloneMapping(group.Last()));
    }

    private static string? BuildMappingKey(SeasonThemeMapping mapping)
    {
        if (!string.IsNullOrWhiteSpace(mapping.SeasonItemId))
        {
            return "id:" + NormalizeId(mapping.SeasonItemId);
        }

        if (!string.IsNullOrWhiteSpace(mapping.SeasonPath))
        {
            return "path:" + NormalizePath(mapping.SeasonPath);
        }

        var series = !string.IsNullOrWhiteSpace(mapping.SeriesItemId)
            ? "id:" + NormalizeId(mapping.SeriesItemId)
            : !string.IsNullOrWhiteSpace(mapping.SeriesPath) ? "path:" + NormalizePath(mapping.SeriesPath) : null;
        return series != null && mapping.SeasonNumber.HasValue ? $"series:{series}:{mapping.SeasonNumber.Value}" : null;
    }

    private static SeasonThemeMapping CloneMapping(SeasonThemeMapping mapping)
    {
        return new SeasonThemeMapping
        {
            Enabled = mapping.Enabled,
            SeriesItemId = mapping.SeriesItemId,
            SeriesPath = mapping.SeriesPath,
            SeasonItemId = mapping.SeasonItemId,
            SeasonPath = mapping.SeasonPath,
            SeasonNumber = mapping.SeasonNumber,
            AnimeThemesSlug = mapping.AnimeThemesSlug,
            AniListId = mapping.AniListId,
            MyAnimeListId = mapping.MyAnimeListId,
            Locked = mapping.Locked,
        };
    }

    private static string NormalizeId(string value) => Guid.TryParse(value, out var parsed) ? parsed.ToString("D") : value.Trim().ToLowerInvariant();

    private static string NormalizePath(string value) => value.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();

    private static string BuildQueryKey(string query, int? year) => string.Join("|", query.Trim().ToLowerInvariant(), year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static void NormalizeDocument(StoreDocument document)
    {
        document.Mappings ??= [];
        document.Rows ??= [];
        document.SearchCache ??= new Dictionary<string, SearchCacheEntry>(StringComparer.OrdinalIgnoreCase);
        if (document.SearchCache.Comparer != StringComparer.OrdinalIgnoreCase)
        {
            document.SearchCache = new Dictionary<string, SearchCacheEntry>(document.SearchCache, StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed class StoreDocument
    {
        public bool LegacyMappingsMigrated { get; set; }

        public List<SeasonThemeMapping> Mappings { get; set; } = [];

        public List<SeasonFinderRowRecord> Rows { get; set; } = [];

        public Dictionary<string, SearchCacheEntry> SearchCache { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public bool CacheReady { get; set; }

        public string CacheVersion { get; set; } = string.Empty;

        public string? LastFullScanUtc { get; set; }

        public string? LastError { get; set; }
    }

    private sealed class SearchCacheEntry
    {
        public string ResultJson { get; set; } = string.Empty;

        public string CreatedAtUtc { get; set; } = string.Empty;

        public string ExpiresAtUtc { get; set; } = string.Empty;
    }

    private sealed class ObjectComparer : IComparer<object?>
    {
        public static ObjectComparer Instance { get; } = new();

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x == null)
            {
                return 1;
            }

            if (y == null)
            {
                return -1;
            }

            if (x is int leftInt && y is int rightInt)
            {
                return leftInt.CompareTo(rightInt);
            }

            return StringComparer.OrdinalIgnoreCase.Compare(Convert.ToString(x, CultureInfo.InvariantCulture), Convert.ToString(y, CultureInfo.InvariantCulture));
        }
    }
}
