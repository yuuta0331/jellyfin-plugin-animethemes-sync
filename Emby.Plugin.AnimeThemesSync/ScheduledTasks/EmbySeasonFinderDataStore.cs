using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using SQLitePCL.pretty;

namespace Emby.Plugin.AnimeThemesSync.ScheduledTasks;

/// <summary>
/// Normalized Season Finder SQLite storage backed by Emby's SQLitePCL.pretty runtime.
/// </summary>
internal sealed class EmbySeasonFinderDataStore : ISeasonFinderDataStore
{
    private const int CurrentSchemaVersion = 1;
    private const int DefaultLimit = 80;
    private const int MaxLimit = 100;
    private const int SearchCacheLimit = 200;
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IAnimeThemesDataPathProvider _pathProvider;
    private readonly IAnimeThemesServerIdentityProvider _serverIdentity;
    private readonly object _syncRoot = new();
    private bool _initialized;

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
            if (_initialized)
            {
                return;
            }

            Directory.CreateDirectory(_pathProvider.GetPluginDataDirectory());
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                foreach (var schemaStatement in """
                    CREATE TABLE IF NOT EXISTS SchemaMetadata (
                        Key TEXT NOT NULL PRIMARY KEY,
                        Value TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS SeasonThemeMappings (
                        ServerKind TEXT NOT NULL,
                        MappingKey TEXT NOT NULL,
                        SeriesItemId TEXT NULL,
                        SeriesPath TEXT NULL,
                        SeasonItemId TEXT NULL,
                        SeasonPath TEXT NULL,
                        SeasonNumber INTEGER NULL,
                        AnimeThemesSlug TEXT NULL,
                        AniListId INTEGER NULL,
                        MyAnimeListId INTEGER NULL,
                        Locked INTEGER NOT NULL,
                        Enabled INTEGER NOT NULL,
                        Source TEXT NOT NULL,
                        UpdatedAtUtc TEXT NOT NULL,
                        PRIMARY KEY (ServerKind, MappingKey)
                    );
                    CREATE INDEX IF NOT EXISTS IX_SeasonThemeMappings_SeasonItem
                        ON SeasonThemeMappings(ServerKind, SeasonItemId);
                    CREATE INDEX IF NOT EXISTS IX_SeasonThemeMappings_SeasonPath
                        ON SeasonThemeMappings(ServerKind, SeasonPath);
                    CREATE TABLE IF NOT EXISTS SeasonFinderRows (
                        ServerKind TEXT NOT NULL,
                        LibraryId TEXT NULL,
                        SeriesItemId TEXT NOT NULL,
                        SeriesName TEXT NOT NULL,
                        SeriesPath TEXT NULL,
                        SeasonItemId TEXT NOT NULL,
                        SeasonName TEXT NOT NULL,
                        SeasonPath TEXT NULL,
                        SeasonNumber INTEGER NULL,
                        Status TEXT NOT NULL,
                        Source TEXT NOT NULL,
                        SameAsSeries INTEGER NOT NULL,
                        AnimeName TEXT NULL,
                        AnimeThemesId INTEGER NULL,
                        AnimeThemesSlug TEXT NULL,
                        AnimeThemesUrl TEXT NULL,
                        AniListId INTEGER NULL,
                        MyAnimeListId INTEGER NULL,
                        PrimaryImageUrl TEXT NULL,
                        OutputRootItemId TEXT NULL,
                        OutputRootPath TEXT NULL,
                        OutputScope TEXT NULL,
                        SearchText TEXT NOT NULL,
                        UpdatedAtUtc TEXT NOT NULL,
                        PRIMARY KEY (ServerKind, SeasonItemId)
                    );
                    CREATE INDEX IF NOT EXISTS IX_SeasonFinderRows_Status
                        ON SeasonFinderRows(ServerKind, Status, SeriesName, SeasonNumber);
                    CREATE INDEX IF NOT EXISTS IX_SeasonFinderRows_Library
                        ON SeasonFinderRows(ServerKind, LibraryId, SeriesName, SeasonNumber);
                    CREATE TABLE IF NOT EXISTS AnimeSearchCache (
                        ServerKind TEXT NOT NULL,
                        QueryKey TEXT NOT NULL,
                        Query TEXT NOT NULL,
                        Year INTEGER NULL,
                        ResultJson TEXT NOT NULL,
                        CreatedAtUtc TEXT NOT NULL,
                        ExpiresAtUtc TEXT NOT NULL,
                        PRIMARY KEY (ServerKind, QueryKey)
                    );
                    CREATE INDEX IF NOT EXISTS IX_AnimeSearchCache_Expiry
                        ON AnimeSearchCache(ServerKind, ExpiresAtUtc);
                    CREATE TABLE IF NOT EXISTS SeasonFinderCacheState (
                        ServerKind TEXT NOT NULL PRIMARY KEY,
                        Ready INTEGER NOT NULL,
                        CacheVersion TEXT NOT NULL,
                        LastFullScanUtc TEXT NULL,
                        LastError TEXT NULL,
                        UpdatedAtUtc TEXT NOT NULL
                    );
                    """.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    connection.Execute(schemaStatement + ";");
                }
                MigrateDocumentStore(connection);
                UpsertMetadata(connection, "SchemaVersion", CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture));
            });
            _initialized = true;
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
            EnsureInitialized();
            using var connection = OpenConnection();
            var marker = "LegacyMappingsMigrated:" + ServerKind;
            if (GetMetadata(connection, marker) == "1")
            {
                return;
            }

            InTransaction(connection, () =>
            {
                foreach (var mapping in SeasonThemeMappingKeyHelper.Deduplicate(mappings))
                {
                    UpsertMapping(connection, mapping, "Legacy");
                }

                UpsertMetadata(connection, marker, "1");
            });
        }
    }

    public List<SeasonThemeMapping> GetSeasonThemeMappings()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var statement = Prepare(connection, """
                SELECT Enabled, SeriesItemId, SeriesPath, SeasonItemId, SeasonPath, SeasonNumber,
                       AnimeThemesSlug, AniListId, MyAnimeListId, Locked
                FROM SeasonThemeMappings WHERE ServerKind = $serverKind;
                """, ("$serverKind", ServerKind));
            var result = new List<SeasonThemeMapping>();
            while (statement.MoveNext())
            {
                var row = statement.Current;
                result.Add(new SeasonThemeMapping
                {
                    Enabled = row.GetInt64(0) != 0,
                    SeriesItemId = GetNullableString(row, 1),
                    SeriesPath = GetNullableString(row, 2),
                    SeasonItemId = GetNullableString(row, 3),
                    SeasonPath = GetNullableString(row, 4),
                    SeasonNumber = GetNullableInt32(row, 5),
                    AnimeThemesSlug = GetNullableString(row, 6),
                    AniListId = GetNullableInt32(row, 7),
                    MyAnimeListId = GetNullableInt32(row, 8),
                    Locked = row.GetInt64(9) != 0,
                });
            }

            return result;
        }
    }

    public void ReplaceSeasonThemeMappings(IEnumerable<SeasonThemeMapping> mappings, string source)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                Execute(connection, "DELETE FROM SeasonThemeMappings WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
                foreach (var mapping in SeasonThemeMappingKeyHelper.Deduplicate(mappings))
                {
                    UpsertMapping(connection, mapping, source);
                }
            });
        }
    }

    public void ApplySeasonThemeMappingChanges(IReadOnlyList<SeasonThemeMappingChange> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                foreach (var change in changes)
                {
                    foreach (var key in SeasonThemeMappingKeyHelper.BuildTargetKeys(change.Target))
                    {
                        Execute(connection,
                            "DELETE FROM SeasonThemeMappings WHERE ServerKind = $serverKind AND MappingKey = $key;",
                            ("$serverKind", ServerKind), ("$key", key));
                    }

                    if (change.Mapping != null)
                    {
                        UpsertMapping(connection, change.Mapping, change.Source);
                    }
                }
            });
        }
    }

    public void ReplaceRows(IEnumerable<SeasonFinderRowRecord> records)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                Execute(connection, "DELETE FROM SeasonFinderRows WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
                foreach (var record in records.GroupBy(item => item.Row.SeasonItemId).Select(group => group.Last()))
                {
                    UpsertRow(connection, record);
                }

                var now = FormatDate(DateTimeOffset.UtcNow);
                Execute(connection, """
                    INSERT INTO SeasonFinderCacheState (ServerKind, Ready, CacheVersion, LastFullScanUtc, LastError, UpdatedAtUtc)
                    VALUES ($serverKind, 1, $version, $now, NULL, $now)
                    ON CONFLICT(ServerKind) DO UPDATE SET Ready = 1, CacheVersion = excluded.CacheVersion,
                        LastFullScanUtc = excluded.LastFullScanUtc, LastError = NULL, UpdatedAtUtc = excluded.UpdatedAtUtc;
                    """, ("$serverKind", ServerKind), ("$version", now), ("$now", now));
            });
        }
    }

    public void UpsertRow(SeasonFinderRowRecord record)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                UpsertRow(connection, record);
                var now = FormatDate(DateTimeOffset.UtcNow);
                Execute(connection, "UPDATE SeasonFinderCacheState SET CacheVersion = $now, UpdatedAtUtc = $now WHERE ServerKind = $serverKind;",
                    ("$serverKind", ServerKind), ("$now", now));
            });
        }
    }

    public SeasonFinderItemsPage QueryRows(string? libraryId, int? startIndex, int? limit, string? searchTerm, string? status, string? sortBy, string? sortOrder)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            var normalizedStart = Math.Max(0, startIndex ?? 0);
            var normalizedLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
            var where = new List<string> { "ServerKind = $serverKind" };
            var parameters = new List<(string, object?)> { ("$serverKind", ServerKind) };
            if (!string.IsNullOrWhiteSpace(libraryId))
            {
                where.Add("LibraryId = $libraryId");
                parameters.Add(("$libraryId", libraryId.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                where.Add("SearchText LIKE $search ESCAPE '\\'");
                parameters.Add(("$search", "%" + EscapeLike(searchTerm.Trim().ToLowerInvariant()) + "%"));
            }

            var normalizedStatus = status?.Trim().ToLowerInvariant();
            if (normalizedStatus == "auto")
            {
                where.Add("Status IN ('Auto', 'Direct', 'Series')");
            }
            else if (!string.IsNullOrWhiteSpace(normalizedStatus) && normalizedStatus != "all")
            {
                where.Add("lower(Status) = $status");
                parameters.Add(("$status", normalizedStatus));
            }

            var orderColumn = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "seasonnumber" => "SeasonNumber",
                "status" => "Status",
                "updated" => "UpdatedAtUtc",
                _ => "SeriesName",
            };
            var direction = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(sortOrder, "descending", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            var whereSql = string.Join(" AND ", where);

            using var connection = OpenConnection();
            var count = ScalarInt(connection, "SELECT COUNT(*) FROM SeasonFinderRows WHERE " + whereSql + ";", parameters.ToArray());
            parameters.Add(("$limit", normalizedLimit));
            parameters.Add(("$start", normalizedStart));
            using var statement = Prepare(connection, $"""
                SELECT SeriesItemId, SeriesName, SeriesPath, SeasonItemId, SeasonName, SeasonPath,
                       SeasonNumber, Status, Source, SameAsSeries, AnimeName, AnimeThemesId,
                       AnimeThemesSlug, AnimeThemesUrl, AniListId, MyAnimeListId, PrimaryImageUrl
                FROM SeasonFinderRows WHERE {whereSql}
                ORDER BY {orderColumn} {direction}, SeriesName ASC, SeasonNumber ASC, SeasonName ASC
                LIMIT $limit OFFSET $start;
                """, parameters.ToArray());
            var items = new List<SeasonThemeMappingRow>();
            while (statement.MoveNext())
            {
                items.Add(ReadRow(statement.Current));
            }

            var cacheState = GetCacheState(connection);
            return new SeasonFinderItemsPage(items, count, normalizedStart, normalizedLimit, cacheState.Version, cacheState.Ready);
        }
    }

    public IReadOnlyList<SeasonThemeMappingRow> GetAllRows()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var statement = Prepare(connection, """
                SELECT SeriesItemId, SeriesName, SeriesPath, SeasonItemId, SeasonName, SeasonPath,
                       SeasonNumber, Status, Source, SameAsSeries, AnimeName, AnimeThemesId,
                       AnimeThemesSlug, AnimeThemesUrl, AniListId, MyAnimeListId, PrimaryImageUrl
                FROM SeasonFinderRows WHERE ServerKind = $serverKind
                ORDER BY SeriesName, SeasonNumber, SeasonName;
                """, ("$serverKind", ServerKind));
            var rows = new List<SeasonThemeMappingRow>();
            while (statement.MoveNext())
            {
                rows.Add(ReadRow(statement.Current));
            }

            return rows;
        }
    }

    public bool IsCacheReady()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            return GetCacheState(connection).Ready;
        }
    }

    public SeasonFinderStorageStatus GetStorageStatus()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var statement = Prepare(connection, """
                SELECT Ready, CacheVersion, LastFullScanUtc, LastError,
                       (SELECT COUNT(*) FROM SeasonFinderRows WHERE ServerKind = $serverKind)
                FROM SeasonFinderCacheState WHERE ServerKind = $serverKind;
                """, ("$serverKind", ServerKind));
            var file = new FileInfo(DatabasePath);
            if (!statement.MoveNext())
            {
                return new SeasonFinderStorageStatus(DatabasePath, file.Exists ? file.Length : 0, 0, string.Empty, false, null, null);
            }

            var row = statement.Current;
            return new SeasonFinderStorageStatus(DatabasePath, file.Exists ? file.Length : 0, row.GetInt(4), row.GetString(1),
                row.GetInt64(0) != 0, GetNullableString(row, 2), GetNullableString(row, 3));
        }
    }

    public void SetRebuildError(string? error)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            var now = FormatDate(DateTimeOffset.UtcNow);
            Execute(connection, """
                INSERT INTO SeasonFinderCacheState (ServerKind, Ready, CacheVersion, LastFullScanUtc, LastError, UpdatedAtUtc)
                VALUES ($serverKind, 0, '', NULL, $error, $now)
                ON CONFLICT(ServerKind) DO UPDATE SET LastError = excluded.LastError, UpdatedAtUtc = excluded.UpdatedAtUtc;
                """, ("$serverKind", ServerKind), ("$error", error), ("$now", now));
        }
    }

    public void ClearCache()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                Execute(connection, "DELETE FROM SeasonFinderRows WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
                Execute(connection, "DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
                Execute(connection, "UPDATE SeasonFinderCacheState SET Ready = 0, CacheVersion = '', LastFullScanUtc = NULL, LastError = NULL, UpdatedAtUtc = $now WHERE ServerKind = $serverKind;",
                    ("$serverKind", ServerKind), ("$now", FormatDate(DateTimeOffset.UtcNow)));
            });
        }
    }

    public bool TryGetSearch(string query, int? year, out string json)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            var key = BuildQueryKey(query, year);
            using var statement = Prepare(connection,
                "SELECT ResultJson, ExpiresAtUtc FROM AnimeSearchCache WHERE ServerKind = $serverKind AND QueryKey = $key;",
                ("$serverKind", ServerKind), ("$key", key));
            if (statement.MoveNext() && DateTimeOffset.TryParse(statement.Current.GetString(1), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var expires) && expires > DateTimeOffset.UtcNow)
            {
                json = statement.Current.GetString(0);
                return true;
            }

            Execute(connection, "DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind AND QueryKey = $key;",
                ("$serverKind", ServerKind), ("$key", key));
            json = string.Empty;
            return false;
        }
    }

    public void SetSearch(string query, int? year, string json)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                var now = DateTimeOffset.UtcNow;
                UpsertSearch(connection, BuildQueryKey(query, year), query.Trim(), year, json, FormatDate(now), FormatDate(now.Add(SearchCacheTtl)));
                Execute(connection, "DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind AND ExpiresAtUtc <= $now;",
                    ("$serverKind", ServerKind), ("$now", FormatDate(now)));
                Execute(connection, """
                    DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind AND QueryKey IN (
                        SELECT QueryKey FROM AnimeSearchCache WHERE ServerKind = $serverKind
                        ORDER BY CreatedAtUtc DESC LIMIT -1 OFFSET $limit
                    );
                    """, ("$serverKind", ServerKind), ("$limit", SearchCacheLimit));
            });
        }
    }

    private SQLiteDatabaseConnection OpenConnection()
    {
        var connection = SQLite3.Open(DatabasePath, ConnectionFlags.ReadWrite | ConnectionFlags.Create, null, true);
        connection.Execute("PRAGMA busy_timeout=5000;");
        try
        {
            connection.Execute("PRAGMA journal_mode=WAL;");
        }
        catch (SQLiteException)
        {
            // Ignore if WAL mode cannot be set (e.g. database is locked by another connection)
        }
        connection.Execute("PRAGMA foreign_keys=ON;");
        return connection;
    }

    private void MigrateDocumentStore(IDatabaseConnection connection)
    {
        var marker = "EmbyDocumentStoreMigrated:" + ServerKind;
        if (GetMetadata(connection, marker) == "1" || !TableExists(connection, "AnimeThemesSyncState"))
        {
            return;
        }

        var existing = ScalarInt(connection, """
            SELECT (SELECT COUNT(*) FROM SeasonThemeMappings WHERE ServerKind = $serverKind) +
                   (SELECT COUNT(*) FROM SeasonFinderRows WHERE ServerKind = $serverKind) +
                   (SELECT COUNT(*) FROM AnimeSearchCache WHERE ServerKind = $serverKind) +
                   (SELECT COUNT(*) FROM SeasonFinderCacheState WHERE ServerKind = $serverKind);
            """, ("$serverKind", ServerKind));
        using var statement = Prepare(connection, "SELECT DocumentJson FROM AnimeThemesSyncState WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
        if (existing == 0 && statement.MoveNext())
        {
            var document = JsonSerializer.Deserialize<LegacyStoreDocument>(statement.Current.GetString(0), JsonOptions) ?? new LegacyStoreDocument();
            foreach (var mapping in SeasonThemeMappingKeyHelper.Deduplicate(document.Mappings ?? []))
            {
                UpsertMapping(connection, mapping, "EmbyDocumentMigration");
            }

            foreach (var record in (document.Rows ?? []).GroupBy(item => item.Row.SeasonItemId).Select(group => group.Last()))
            {
                UpsertRow(connection, record);
            }

            foreach (var entry in document.SearchCache ?? new Dictionary<string, LegacySearchCacheEntry>())
            {
                ParseQueryKey(entry.Key, out var query, out var year);
                UpsertSearch(connection, entry.Key, query, year, entry.Value.ResultJson, entry.Value.CreatedAtUtc, entry.Value.ExpiresAtUtc);
            }

            var updated = FormatDate(DateTimeOffset.UtcNow);
            Execute(connection, """
                INSERT INTO SeasonFinderCacheState (ServerKind, Ready, CacheVersion, LastFullScanUtc, LastError, UpdatedAtUtc)
                VALUES ($serverKind, $ready, $version, $scan, $error, $updated)
                ON CONFLICT(ServerKind) DO UPDATE SET Ready = excluded.Ready, CacheVersion = excluded.CacheVersion,
                    LastFullScanUtc = excluded.LastFullScanUtc, LastError = excluded.LastError, UpdatedAtUtc = excluded.UpdatedAtUtc;
                """, ("$serverKind", ServerKind), ("$ready", document.CacheReady ? 1 : 0),
                ("$version", document.CacheVersion ?? string.Empty), ("$scan", document.LastFullScanUtc),
                ("$error", document.LastError), ("$updated", updated));
            if (document.LegacyMappingsMigrated)
            {
                UpsertMetadata(connection, "LegacyMappingsMigrated:" + ServerKind, "1");
            }
        }

        UpsertMetadata(connection, marker, "1");
    }

    private void UpsertMapping(IDatabaseConnection connection, SeasonThemeMapping mapping, string source)
    {
        var key = SeasonThemeMappingKeyHelper.BuildMappingKey(mapping);
        if (key == null)
        {
            return;
        }

        Execute(connection, """
            INSERT INTO SeasonThemeMappings (ServerKind, MappingKey, SeriesItemId, SeriesPath, SeasonItemId,
                SeasonPath, SeasonNumber, AnimeThemesSlug, AniListId, MyAnimeListId, Locked, Enabled, Source, UpdatedAtUtc)
            VALUES ($serverKind, $key, $seriesId, $seriesPath, $seasonId, $seasonPath, $seasonNumber,
                $slug, $aniListId, $malId, $locked, $enabled, $source, $updated)
            ON CONFLICT(ServerKind, MappingKey) DO UPDATE SET SeriesItemId = excluded.SeriesItemId,
                SeriesPath = excluded.SeriesPath, SeasonItemId = excluded.SeasonItemId, SeasonPath = excluded.SeasonPath,
                SeasonNumber = excluded.SeasonNumber, AnimeThemesSlug = excluded.AnimeThemesSlug,
                AniListId = excluded.AniListId, MyAnimeListId = excluded.MyAnimeListId, Locked = excluded.Locked,
                Enabled = excluded.Enabled, Source = excluded.Source, UpdatedAtUtc = excluded.UpdatedAtUtc;
            """, ("$serverKind", ServerKind), ("$key", key), ("$seriesId", mapping.SeriesItemId),
            ("$seriesPath", mapping.SeriesPath), ("$seasonId", mapping.SeasonItemId), ("$seasonPath", mapping.SeasonPath),
            ("$seasonNumber", mapping.SeasonNumber), ("$slug", mapping.AnimeThemesSlug), ("$aniListId", mapping.AniListId),
            ("$malId", mapping.MyAnimeListId), ("$locked", mapping.Locked ? 1 : 0), ("$enabled", mapping.Enabled ? 1 : 0),
            ("$source", source), ("$updated", FormatDate(DateTimeOffset.UtcNow)));
    }

    private void UpsertRow(IDatabaseConnection connection, SeasonFinderRowRecord record)
    {
        var row = record.Row;
        var searchText = string.Join(" ", new[] { row.SeriesName, row.SeasonName, row.AnimeName, row.AnimeThemesSlug,
            row.AniListId?.ToString(CultureInfo.InvariantCulture), row.MyAnimeListId?.ToString(CultureInfo.InvariantCulture) }
            .Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();
        Execute(connection, """
            INSERT INTO SeasonFinderRows (ServerKind, LibraryId, SeriesItemId, SeriesName, SeriesPath, SeasonItemId,
                SeasonName, SeasonPath, SeasonNumber, Status, Source, SameAsSeries, AnimeName, AnimeThemesId,
                AnimeThemesSlug, AnimeThemesUrl, AniListId, MyAnimeListId, PrimaryImageUrl, OutputRootItemId,
                OutputRootPath, OutputScope, SearchText, UpdatedAtUtc)
            VALUES ($serverKind, $libraryId, $seriesId, $seriesName, $seriesPath, $seasonId, $seasonName, $seasonPath,
                $seasonNumber, $status, $source, $sameAsSeries, $animeName, $animeThemesId, $slug, $url,
                $aniListId, $malId, $image, $outputId, $outputPath, $outputScope, $search, $updated)
            ON CONFLICT(ServerKind, SeasonItemId) DO UPDATE SET LibraryId = excluded.LibraryId,
                SeriesItemId = excluded.SeriesItemId, SeriesName = excluded.SeriesName, SeriesPath = excluded.SeriesPath,
                SeasonName = excluded.SeasonName, SeasonPath = excluded.SeasonPath, SeasonNumber = excluded.SeasonNumber,
                Status = excluded.Status, Source = excluded.Source, SameAsSeries = excluded.SameAsSeries,
                AnimeName = excluded.AnimeName, AnimeThemesId = excluded.AnimeThemesId,
                AnimeThemesSlug = excluded.AnimeThemesSlug, AnimeThemesUrl = excluded.AnimeThemesUrl,
                AniListId = excluded.AniListId, MyAnimeListId = excluded.MyAnimeListId,
                PrimaryImageUrl = excluded.PrimaryImageUrl, OutputRootItemId = excluded.OutputRootItemId,
                OutputRootPath = excluded.OutputRootPath, OutputScope = excluded.OutputScope,
                SearchText = excluded.SearchText, UpdatedAtUtc = excluded.UpdatedAtUtc;
            """, ("$serverKind", ServerKind), ("$libraryId", record.LibraryId), ("$seriesId", row.SeriesItemId.ToString("D")),
            ("$seriesName", row.SeriesName), ("$seriesPath", row.SeriesPath), ("$seasonId", row.SeasonItemId.ToString("D")),
            ("$seasonName", row.SeasonName), ("$seasonPath", row.SeasonPath), ("$seasonNumber", row.SeasonNumber),
            ("$status", row.Status), ("$source", row.Source), ("$sameAsSeries", row.SameAsSeries ? 1 : 0),
            ("$animeName", row.AnimeName), ("$animeThemesId", row.AnimeThemesId), ("$slug", row.AnimeThemesSlug),
            ("$url", row.AnimeThemesUrl), ("$aniListId", row.AniListId), ("$malId", row.MyAnimeListId),
            ("$image", row.PrimaryImageUrl), ("$outputId", record.OutputRootItemId), ("$outputPath", record.OutputRootPath),
            ("$outputScope", record.OutputScope), ("$search", searchText),
            ("$updated", record.UpdatedAtUtc ?? FormatDate(DateTimeOffset.UtcNow)));
    }

    private void UpsertSearch(IDatabaseConnection connection, string key, string query, int? year, string json, string created, string expires)
    {
        Execute(connection, """
            INSERT INTO AnimeSearchCache (ServerKind, QueryKey, Query, Year, ResultJson, CreatedAtUtc, ExpiresAtUtc)
            VALUES ($serverKind, $key, $query, $year, $json, $created, $expires)
            ON CONFLICT(ServerKind, QueryKey) DO UPDATE SET Query = excluded.Query, Year = excluded.Year,
                ResultJson = excluded.ResultJson, CreatedAtUtc = excluded.CreatedAtUtc, ExpiresAtUtc = excluded.ExpiresAtUtc;
            """, ("$serverKind", ServerKind), ("$key", key), ("$query", query), ("$year", year),
            ("$json", json), ("$created", created), ("$expires", expires));
    }

    private (bool Ready, string Version) GetCacheState(IDatabaseConnection connection)
    {
        using var statement = Prepare(connection, "SELECT Ready, CacheVersion FROM SeasonFinderCacheState WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
        return statement.MoveNext() ? (statement.Current.GetInt64(0) != 0, statement.Current.GetString(1)) : (false, string.Empty);
    }

    private static SeasonThemeMappingRow ReadRow(IResultSet row) => new(
        Guid.Parse(row.GetString(0)), row.GetString(1), GetNullableString(row, 2), Guid.Parse(row.GetString(3)),
        row.GetString(4), GetNullableString(row, 5), GetNullableInt32(row, 6), row.GetString(7), row.GetString(8),
        row.GetInt64(9) != 0, GetNullableString(row, 10), GetNullableInt32(row, 11), GetNullableString(row, 12),
        GetNullableString(row, 13), GetNullableInt32(row, 14), GetNullableInt32(row, 15), GetNullableString(row, 16));

    private static void InTransaction(IDatabaseConnection connection, Action action)
    {
        connection.Execute("BEGIN IMMEDIATE;");
        try
        {
            action();
            connection.Execute("COMMIT;");
        }
        catch
        {
            connection.Execute("ROLLBACK;");
            throw;
        }
    }

    private static IStatement Prepare(IDatabaseConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        var statement = connection.PrepareStatement(sql);
        foreach (var parameter in parameters)
        {
            Bind(statement.BindParameters[parameter.Name], parameter.Value);
        }

        return statement;
    }

    private static void Execute(IDatabaseConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        using var statement = Prepare(connection, sql, parameters);
        _ = statement.MoveNext();
    }

    private static void Bind(IBindParameter parameter, object? value)
    {
        switch (value)
        {
            case null:
                parameter.BindNull();
                break;
            case int intValue:
                parameter.Bind(intValue);
                break;
            case long longValue:
                parameter.Bind(longValue);
                break;
            case string stringValue:
                parameter.Bind(stringValue);
                break;
            default:
                parameter.Bind(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    private static int ScalarInt(IDatabaseConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        using var statement = Prepare(connection, sql, parameters);
        return statement.MoveNext() ? statement.Current.GetInt(0) : 0;
    }

    private static bool TableExists(IDatabaseConnection connection, string name) =>
        ScalarInt(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;", ("$name", name)) > 0;

    private static string? GetMetadata(IDatabaseConnection connection, string key)
    {
        using var statement = Prepare(connection, "SELECT Value FROM SchemaMetadata WHERE Key = $key;", ("$key", key));
        return statement.MoveNext() ? statement.Current.GetString(0) : null;
    }

    private static void UpsertMetadata(IDatabaseConnection connection, string key, string value) =>
        Execute(connection, "INSERT INTO SchemaMetadata (Key, Value) VALUES ($key, $value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;",
            ("$key", key), ("$value", value));

    private static string BuildQueryKey(string query, int? year) =>
        string.Join("|", query.Trim().ToLowerInvariant(), year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private static void ParseQueryKey(string key, out string query, out int? year)
    {
        var separator = key.LastIndexOf('|');
        query = separator < 0 ? key : key[..separator];
        year = separator >= 0 && int.TryParse(key[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private static string? GetNullableString(IResultSet row, int ordinal) => row.IsDBNull(ordinal) ? null : row.GetString(ordinal);

    private static int? GetNullableInt32(IResultSet row, int ordinal) => row.IsDBNull(ordinal) ? null : row.GetInt(ordinal);

    private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private sealed class LegacyStoreDocument
    {
        public bool LegacyMappingsMigrated { get; set; }
        public List<SeasonThemeMapping>? Mappings { get; set; }
        public List<SeasonFinderRowRecord>? Rows { get; set; }
        public Dictionary<string, LegacySearchCacheEntry>? SearchCache { get; set; }
        public bool CacheReady { get; set; }
        public string? CacheVersion { get; set; }
        public string? LastFullScanUtc { get; set; }
        public string? LastError { get; set; }
    }

    private sealed class LegacySearchCacheEntry
    {
        public string ResultJson { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string ExpiresAtUtc { get; set; } = string.Empty;
    }
}
