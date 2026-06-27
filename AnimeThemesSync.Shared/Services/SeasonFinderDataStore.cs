using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;
using Microsoft.Data.Sqlite;

#pragma warning disable SA1117

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// SQLite-backed persistent storage for Season Finder rows and mappings.
/// </summary>
public sealed class SeasonFinderDataStore : ISeasonFinderDataStore
{
    private const int CurrentSchemaVersion = 1;
    private const int DefaultLimit = 80;
    private const int MaxLimit = 100;
    private const int SearchCacheLimit = 200;
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromDays(7);
    private readonly IAnimeThemesDataPathProvider _pathProvider;
    private readonly IAnimeThemesServerIdentityProvider _serverIdentity;
    private readonly object _syncRoot = new();
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonFinderDataStore"/> class.
    /// </summary>
    public SeasonFinderDataStore(IAnimeThemesDataPathProvider pathProvider, IAnimeThemesServerIdentityProvider serverIdentity)
    {
        _pathProvider = pathProvider;
        _serverIdentity = serverIdentity;
    }

    /// <summary>
    /// Gets the SQLite database path.
    /// </summary>
    public string DatabasePath => Path.Combine(_pathProvider.GetPluginDataDirectory(), "animethemes-sync.db");

    private string ServerKind => _serverIdentity.ServerKind;

    /// <summary>
    /// Creates or upgrades the database.
    /// </summary>
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
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, """
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
                """);
            UpsertMetadata(connection, transaction, "SchemaVersion", CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture));
            transaction.Commit();
            _initialized = true;
        }
    }

    /// <summary>
    /// Imports legacy configuration mappings once per server.
    /// </summary>
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

            using var transaction = connection.BeginTransaction();
            foreach (var mapping in SeasonThemeMappingKeyHelper.Deduplicate(mappings))
            {
                UpsertMapping(connection, transaction, mapping, "Legacy");
            }

            UpsertMetadata(connection, transaction, marker, "1");
            transaction.Commit();
        }
    }

    /// <summary>
    /// Gets all persisted season mappings for the current server.
    /// </summary>
    public List<SeasonThemeMapping> GetSeasonThemeMappings()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Enabled, SeriesItemId, SeriesPath, SeasonItemId, SeasonPath, SeasonNumber,
                       AnimeThemesSlug, AniListId, MyAnimeListId, Locked
                FROM SeasonThemeMappings
                WHERE ServerKind = $serverKind;
                """;
            command.Parameters.AddWithValue("$serverKind", ServerKind);
            using var reader = command.ExecuteReader();
            var result = new List<SeasonThemeMapping>();
            while (reader.Read())
            {
                result.Add(new SeasonThemeMapping
                {
                    Enabled = reader.GetInt64(0) != 0,
                    SeriesItemId = GetNullableString(reader, 1),
                    SeriesPath = GetNullableString(reader, 2),
                    SeasonItemId = GetNullableString(reader, 3),
                    SeasonPath = GetNullableString(reader, 4),
                    SeasonNumber = GetNullableInt32(reader, 5),
                    AnimeThemesSlug = GetNullableString(reader, 6),
                    AniListId = GetNullableInt32(reader, 7),
                    MyAnimeListId = GetNullableInt32(reader, 8),
                    Locked = reader.GetInt64(9) != 0,
                });
            }

            return result;
        }
    }

    /// <summary>
    /// Replaces all mappings for the current server.
    /// </summary>
    public void ReplaceSeasonThemeMappings(IEnumerable<SeasonThemeMapping> mappings, string source)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, "DELETE FROM SeasonThemeMappings WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
            foreach (var mapping in SeasonThemeMappingKeyHelper.Deduplicate(mappings))
            {
                UpsertMapping(connection, transaction, mapping, source);
            }

            transaction.Commit();
        }
    }

    /// <summary>
    /// Atomically replaces only mappings that identify the supplied seasons.
    /// </summary>
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
            using var transaction = connection.BeginTransaction();
            foreach (var change in changes)
            {
                foreach (var key in SeasonThemeMappingKeyHelper.BuildTargetKeys(change.Target))
                {
                    Execute(connection, transaction,
                        "DELETE FROM SeasonThemeMappings WHERE ServerKind = $serverKind AND MappingKey = $key;",
                        ("$serverKind", ServerKind), ("$key", key));
                }

                if (change.Mapping != null)
                {
                    UpsertMapping(connection, transaction, change.Mapping, change.Source);
                }
            }

            transaction.Commit();
        }
    }

    /// <summary>
    /// Atomically replaces all Season Finder rows.
    /// </summary>
    public void ReplaceRows(IEnumerable<SeasonFinderRowRecord> records)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, "DELETE FROM SeasonFinderRows WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
            foreach (var record in records)
            {
                UpsertRow(connection, transaction, record);
            }

            var now = FormatDate(DateTimeOffset.UtcNow);
            Execute(connection, transaction, """
                INSERT INTO SeasonFinderCacheState (ServerKind, Ready, CacheVersion, LastFullScanUtc, LastError, UpdatedAtUtc)
                VALUES ($serverKind, 1, $version, $now, NULL, $now)
                ON CONFLICT(ServerKind) DO UPDATE SET
                    Ready = 1, CacheVersion = excluded.CacheVersion, LastFullScanUtc = excluded.LastFullScanUtc,
                    LastError = NULL, UpdatedAtUtc = excluded.UpdatedAtUtc;
                """, ("$serverKind", ServerKind), ("$version", now), ("$now", now));
            transaction.Commit();
        }
    }

    /// <summary>
    /// Upserts one Season Finder row.
    /// </summary>
    public void UpsertRow(SeasonFinderRowRecord record)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            UpsertRow(connection, transaction, record);
            TouchCacheVersion(connection, transaction);
            transaction.Commit();
        }
    }

    /// <summary>
    /// Queries a page of Season Finder rows.
    /// </summary>
    public SeasonFinderItemsPage QueryRows(string? libraryId, int? startIndex, int? limit, string? searchTerm, string? status, string? sortBy, string? sortOrder)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            var normalizedStart = Math.Max(0, startIndex ?? 0);
            var normalizedLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
            using var connection = OpenConnection();
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

            var whereSql = string.Join(" AND ", where);
            var orderColumn = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "seasonnumber" => "SeasonNumber",
                "status" => "Status",
                "updated" => "UpdatedAtUtc",
                _ => "SeriesName",
            };
            var direction = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(sortOrder, "descending", StringComparison.OrdinalIgnoreCase)
                ? "DESC"
                : "ASC";

            using var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM SeasonFinderRows WHERE " + whereSql + ";";
            AddParameters(countCommand, parameters);
            var count = Convert.ToInt32(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);

            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT SeriesItemId, SeriesName, SeriesPath, SeasonItemId, SeasonName, SeasonPath,
                       SeasonNumber, Status, Source, SameAsSeries, AnimeName, AnimeThemesId,
                       AnimeThemesSlug, AnimeThemesUrl, AniListId, MyAnimeListId, PrimaryImageUrl
                FROM SeasonFinderRows
                WHERE {whereSql}
                ORDER BY {orderColumn} {direction}, SeriesName ASC, SeasonNumber ASC, SeasonName ASC
                LIMIT $limit OFFSET $start;
                """;
            AddParameters(command, parameters);
            command.Parameters.AddWithValue("$limit", normalizedLimit);
            command.Parameters.AddWithValue("$start", normalizedStart);
            using var reader = command.ExecuteReader();
            var items = new List<SeasonThemeMappingRow>();
            while (reader.Read())
            {
                items.Add(ReadRow(reader));
            }

            var cacheState = GetCacheState(connection);
            return new SeasonFinderItemsPage(items, count, normalizedStart, normalizedLimit, cacheState.Version, cacheState.Ready);
        }
    }

    /// <summary>
    /// Gets all cached rows for compatibility and exports.
    /// </summary>
    public IReadOnlyList<SeasonThemeMappingRow> GetAllRows()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT SeriesItemId, SeriesName, SeriesPath, SeasonItemId, SeasonName, SeasonPath,
                       SeasonNumber, Status, Source, SameAsSeries, AnimeName, AnimeThemesId,
                       AnimeThemesSlug, AnimeThemesUrl, AniListId, MyAnimeListId, PrimaryImageUrl
                FROM SeasonFinderRows WHERE ServerKind = $serverKind
                ORDER BY SeriesName, SeasonNumber, SeasonName;
                """;
            command.Parameters.AddWithValue("$serverKind", ServerKind);
            using var reader = command.ExecuteReader();
            var rows = new List<SeasonThemeMappingRow>();
            while (reader.Read())
            {
                rows.Add(ReadRow(reader));
            }

            return rows;
        }
    }

    /// <summary>
    /// Gets whether a complete cache is available.
    /// </summary>
    public bool IsCacheReady()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            return GetCacheState(connection).Ready;
        }
    }

    /// <summary>
    /// Gets Season Finder database and cache status.
    /// </summary>
    public SeasonFinderStorageStatus GetStorageStatus()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT s.Ready, s.CacheVersion, s.LastFullScanUtc, s.LastError,
                       (SELECT COUNT(*) FROM SeasonFinderRows r WHERE r.ServerKind = $serverKind)
                FROM SeasonFinderCacheState s WHERE s.ServerKind = $serverKind;
                """;
            command.Parameters.AddWithValue("$serverKind", ServerKind);
            using var reader = command.ExecuteReader();
            var file = new FileInfo(DatabasePath);
            if (!reader.Read())
            {
                return new SeasonFinderStorageStatus(DatabasePath, file.Exists ? file.Length : 0, 0, string.Empty, false, null, null);
            }

            return new SeasonFinderStorageStatus(
                DatabasePath,
                file.Exists ? file.Length : 0,
                reader.GetInt32(4),
                reader.GetString(1),
                reader.GetInt64(0) != 0,
                GetNullableString(reader, 2),
                GetNullableString(reader, 3));
        }
    }

    /// <summary>
    /// Stores the last rebuild error.
    /// </summary>
    public void SetRebuildError(string? error)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            var now = FormatDate(DateTimeOffset.UtcNow);
            Execute(connection, null, """
                INSERT INTO SeasonFinderCacheState (ServerKind, Ready, CacheVersion, LastFullScanUtc, LastError, UpdatedAtUtc)
                VALUES ($serverKind, 0, '', NULL, $error, $now)
                ON CONFLICT(ServerKind) DO UPDATE SET LastError = excluded.LastError, UpdatedAtUtc = excluded.UpdatedAtUtc;
                """, ("$serverKind", ServerKind), ("$error", error), ("$now", now));
        }
    }

    /// <summary>
    /// Clears rebuildable Season Finder data without deleting mappings.
    /// </summary>
    public void ClearCache()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, "DELETE FROM SeasonFinderRows WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
            Execute(connection, transaction, "DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
            Execute(connection, transaction, "UPDATE SeasonFinderCacheState SET Ready = 0, CacheVersion = '', LastFullScanUtc = NULL, LastError = NULL, UpdatedAtUtc = $now WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind), ("$now", FormatDate(DateTimeOffset.UtcNow)));
            transaction.Commit();
        }
    }

    /// <summary>
    /// Tries to get a non-expired AnimeThemes search response.
    /// </summary>
    public bool TryGetSearch(string query, int? year, out string json)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT ResultJson, ExpiresAtUtc FROM AnimeSearchCache WHERE ServerKind = $serverKind AND QueryKey = $key;";
            command.Parameters.AddWithValue("$serverKind", ServerKind);
            command.Parameters.AddWithValue("$key", BuildQueryKey(query, year));
            using var reader = command.ExecuteReader();
            if (reader.Read() && DateTimeOffset.TryParse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expires) && expires > DateTimeOffset.UtcNow)
            {
                json = reader.GetString(0);
                return true;
            }

            json = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// Stores an AnimeThemes search response.
    /// </summary>
    public void SetSearch(string query, int? year, string json)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            var now = DateTimeOffset.UtcNow;
            Execute(connection, transaction, """
                INSERT INTO AnimeSearchCache (ServerKind, QueryKey, Query, Year, ResultJson, CreatedAtUtc, ExpiresAtUtc)
                VALUES ($serverKind, $key, $query, $year, $json, $created, $expires)
                ON CONFLICT(ServerKind, QueryKey) DO UPDATE SET
                    Query = excluded.Query, Year = excluded.Year, ResultJson = excluded.ResultJson,
                    CreatedAtUtc = excluded.CreatedAtUtc, ExpiresAtUtc = excluded.ExpiresAtUtc;
                """, ("$serverKind", ServerKind), ("$key", BuildQueryKey(query, year)), ("$query", query.Trim()),
                ("$year", year), ("$json", json), ("$created", FormatDate(now)), ("$expires", FormatDate(now.Add(SearchCacheTtl))));
            Execute(connection, transaction, "DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind AND ExpiresAtUtc <= $now;", ("$serverKind", ServerKind), ("$now", FormatDate(now)));
            Execute(connection, transaction, """
                DELETE FROM AnimeSearchCache
                WHERE ServerKind = $serverKind AND QueryKey IN (
                    SELECT QueryKey FROM AnimeSearchCache WHERE ServerKind = $serverKind
                    ORDER BY CreatedAtUtc DESC LIMIT -1 OFFSET $limit
                );
                """, ("$serverKind", ServerKind), ("$limit", SearchCacheLimit));
            transaction.Commit();
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";
        command.ExecuteNonQuery();
        return connection;
    }

    private void UpsertMapping(SqliteConnection connection, SqliteTransaction transaction, SeasonThemeMapping mapping, string source)
    {
        var key = SeasonThemeMappingKeyHelper.BuildMappingKey(mapping);
        if (key == null)
        {
            return;
        }

        Execute(connection, transaction, """
            INSERT INTO SeasonThemeMappings (
                ServerKind, MappingKey, SeriesItemId, SeriesPath, SeasonItemId, SeasonPath, SeasonNumber,
                AnimeThemesSlug, AniListId, MyAnimeListId, Locked, Enabled, Source, UpdatedAtUtc)
            VALUES ($serverKind, $key, $seriesId, $seriesPath, $seasonId, $seasonPath, $seasonNumber,
                    $slug, $aniListId, $malId, $locked, $enabled, $source, $updated)
            ON CONFLICT(ServerKind, MappingKey) DO UPDATE SET
                SeriesItemId = excluded.SeriesItemId, SeriesPath = excluded.SeriesPath,
                SeasonItemId = excluded.SeasonItemId, SeasonPath = excluded.SeasonPath,
                SeasonNumber = excluded.SeasonNumber, AnimeThemesSlug = excluded.AnimeThemesSlug,
                AniListId = excluded.AniListId, MyAnimeListId = excluded.MyAnimeListId,
                Locked = excluded.Locked, Enabled = excluded.Enabled, Source = excluded.Source,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """, ("$serverKind", ServerKind), ("$key", key), ("$seriesId", mapping.SeriesItemId),
            ("$seriesPath", mapping.SeriesPath), ("$seasonId", mapping.SeasonItemId), ("$seasonPath", mapping.SeasonPath),
            ("$seasonNumber", mapping.SeasonNumber), ("$slug", mapping.AnimeThemesSlug), ("$aniListId", mapping.AniListId),
            ("$malId", mapping.MyAnimeListId), ("$locked", mapping.Locked ? 1 : 0), ("$enabled", mapping.Enabled ? 1 : 0),
            ("$source", source), ("$updated", FormatDate(DateTimeOffset.UtcNow)));
    }

    private void UpsertRow(SqliteConnection connection, SqliteTransaction transaction, SeasonFinderRowRecord record)
    {
        var row = record.Row;
        var searchText = string.Join(" ", new[]
        {
            row.SeriesName, row.SeasonName, row.AnimeName, row.AnimeThemesSlug,
            row.AniListId?.ToString(CultureInfo.InvariantCulture), row.MyAnimeListId?.ToString(CultureInfo.InvariantCulture),
        }.Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();
        Execute(connection, transaction, """
            INSERT INTO SeasonFinderRows (
                ServerKind, LibraryId, SeriesItemId, SeriesName, SeriesPath, SeasonItemId, SeasonName,
                SeasonPath, SeasonNumber, Status, Source, SameAsSeries, AnimeName, AnimeThemesId,
                AnimeThemesSlug, AnimeThemesUrl, AniListId, MyAnimeListId, PrimaryImageUrl,
                OutputRootItemId, OutputRootPath, OutputScope, SearchText, UpdatedAtUtc)
            VALUES ($serverKind, $libraryId, $seriesId, $seriesName, $seriesPath, $seasonId, $seasonName,
                    $seasonPath, $seasonNumber, $status, $source, $sameAsSeries, $animeName, $animeThemesId,
                    $slug, $url, $aniListId, $malId, $image, $outputId, $outputPath, $outputScope, $search, $updated)
            ON CONFLICT(ServerKind, SeasonItemId) DO UPDATE SET
                LibraryId = excluded.LibraryId, SeriesItemId = excluded.SeriesItemId, SeriesName = excluded.SeriesName,
                SeriesPath = excluded.SeriesPath, SeasonName = excluded.SeasonName, SeasonPath = excluded.SeasonPath,
                SeasonNumber = excluded.SeasonNumber, Status = excluded.Status, Source = excluded.Source,
                SameAsSeries = excluded.SameAsSeries, AnimeName = excluded.AnimeName,
                AnimeThemesId = excluded.AnimeThemesId, AnimeThemesSlug = excluded.AnimeThemesSlug,
                AnimeThemesUrl = excluded.AnimeThemesUrl, AniListId = excluded.AniListId,
                MyAnimeListId = excluded.MyAnimeListId, PrimaryImageUrl = excluded.PrimaryImageUrl,
                OutputRootItemId = excluded.OutputRootItemId, OutputRootPath = excluded.OutputRootPath,
                OutputScope = excluded.OutputScope, SearchText = excluded.SearchText, UpdatedAtUtc = excluded.UpdatedAtUtc;
            """, ("$serverKind", ServerKind), ("$libraryId", record.LibraryId), ("$seriesId", row.SeriesItemId.ToString("D")),
            ("$seriesName", row.SeriesName), ("$seriesPath", row.SeriesPath), ("$seasonId", row.SeasonItemId.ToString("D")),
            ("$seasonName", row.SeasonName), ("$seasonPath", row.SeasonPath), ("$seasonNumber", row.SeasonNumber),
            ("$status", row.Status), ("$source", row.Source), ("$sameAsSeries", row.SameAsSeries ? 1 : 0),
            ("$animeName", row.AnimeName), ("$animeThemesId", row.AnimeThemesId), ("$slug", row.AnimeThemesSlug),
            ("$url", row.AnimeThemesUrl), ("$aniListId", row.AniListId), ("$malId", row.MyAnimeListId),
            ("$image", row.PrimaryImageUrl), ("$outputId", record.OutputRootItemId), ("$outputPath", record.OutputRootPath),
            ("$outputScope", record.OutputScope), ("$search", searchText), ("$updated", FormatDate(DateTimeOffset.UtcNow)));
    }

    private static SeasonThemeMappingRow ReadRow(SqliteDataReader reader)
    {
        return new SeasonThemeMappingRow(
            Guid.Parse(reader.GetString(0)), reader.GetString(1), GetNullableString(reader, 2),
            Guid.Parse(reader.GetString(3)), reader.GetString(4), GetNullableString(reader, 5),
            GetNullableInt32(reader, 6), reader.GetString(7), reader.GetString(8), reader.GetInt64(9) != 0,
            GetNullableString(reader, 10), GetNullableInt32(reader, 11), GetNullableString(reader, 12),
            GetNullableString(reader, 13), GetNullableInt32(reader, 14), GetNullableInt32(reader, 15), GetNullableString(reader, 16));
    }

    private (bool Ready, string Version) GetCacheState(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Ready, CacheVersion FROM SeasonFinderCacheState WHERE ServerKind = $serverKind;";
        command.Parameters.AddWithValue("$serverKind", ServerKind);
        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt64(0) != 0, reader.GetString(1)) : (false, string.Empty);
    }

    private void TouchCacheVersion(SqliteConnection connection, SqliteTransaction transaction)
    {
        var now = FormatDate(DateTimeOffset.UtcNow);
        Execute(connection, transaction, """
            UPDATE SeasonFinderCacheState
            SET CacheVersion = $version, UpdatedAtUtc = $version
            WHERE ServerKind = $serverKind;
            """, ("$serverKind", ServerKind), ("$version", now));
    }

    private static string BuildQueryKey(string query, int? year) => string.Join("|", query.Trim().ToLowerInvariant(), year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private static string? GetNullableString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static int? GetNullableInt32(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static void AddParameters(SqliteCommand command, IEnumerable<(string Name, object? Value)> parameters)
    {
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        command.ExecuteNonQuery();
    }

    private static string? GetMetadata(SqliteConnection connection, string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM SchemaMetadata WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static void UpsertMetadata(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
    {
        Execute(connection, transaction, "INSERT INTO SchemaMetadata (Key, Value) VALUES ($key, $value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;", ("$key", key), ("$value", value));
    }
}

#pragma warning restore SA1117

