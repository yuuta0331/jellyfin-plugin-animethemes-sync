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
    private const int CurrentSchemaVersion = 5;
    private const int DefaultLimit = 80;
    private const int MaxLimit = 100;
    private const int SearchCacheLimit = 200;
    private const int ApiFetchCacheLimit = 2000;
    private static readonly TimeSpan ApiFetchCacheRetention = TimeSpan.FromDays(180);
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
                    AnimeYear INTEGER NULL,
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
                CREATE TABLE IF NOT EXISTS SeasonAutomationRules (
                    ServerKind TEXT NOT NULL,
                    RuleKey TEXT NOT NULL,
                    SeriesItemId TEXT NOT NULL,
                    SeasonItemId TEXT NOT NULL,
                    SeasonName TEXT NOT NULL,
                    SeasonNumber INTEGER NULL,
                    AnimeThemesSlug TEXT NULL,
                    AniListId INTEGER NULL,
                    MyAnimeListId INTEGER NULL,
                    AnimeYear INTEGER NULL,
                    AnimeSeason TEXT NULL,
                    BroadcastSeasonKey TEXT NULL,
                    BroadcastSeasonLabel TEXT NULL,
                    Source TEXT NOT NULL,
                    ResolvedAtUtc TEXT NULL,
                    LastError TEXT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    PRIMARY KEY (ServerKind, RuleKey)
                );
                CREATE INDEX IF NOT EXISTS IX_SeasonAutomationRules_Series
                    ON SeasonAutomationRules(ServerKind, SeriesItemId, SeasonNumber);
                CREATE INDEX IF NOT EXISTS IX_SeasonAutomationRules_Broadcast
                    ON SeasonAutomationRules(ServerKind, BroadcastSeasonKey);
                CREATE TABLE IF NOT EXISTS SeasonAutomationTags (
                    ServerKind TEXT NOT NULL,
                    RuleKey TEXT NOT NULL,
                    TargetItemId TEXT NOT NULL,
                    TargetItemType TEXT NOT NULL,
                    TagName TEXT NOT NULL,
                    Source TEXT NOT NULL DEFAULT 'Manual',
                    AddedByPlugin INTEGER NOT NULL,
                    LastAppliedAtUtc TEXT NULL,
                    LastError TEXT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    PRIMARY KEY (ServerKind, RuleKey, TargetItemId, TagName)
                );
                CREATE INDEX IF NOT EXISTS IX_SeasonAutomationTags_Target
                    ON SeasonAutomationTags(ServerKind, TargetItemId);
                CREATE TABLE IF NOT EXISTS ManagedSeasonCollections (
                    ServerKind TEXT NOT NULL,
                    CollectionKey TEXT NOT NULL,
                    CollectionName TEXT NOT NULL,
                    CollectionItemId TEXT NULL,
                    IsPluginCreated INTEGER NOT NULL,
                    LastError TEXT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    PRIMARY KEY (ServerKind, CollectionKey)
                );
                CREATE TABLE IF NOT EXISTS ManagedSeasonCollectionMembers (
                    ServerKind TEXT NOT NULL,
                    CollectionKey TEXT NOT NULL,
                    RuleKey TEXT NOT NULL,
                    TargetItemId TEXT NOT NULL,
                    TargetItemType TEXT NOT NULL,
                    AddedByPlugin INTEGER NOT NULL,
                    LastAppliedAtUtc TEXT NULL,
                    LastError TEXT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    PRIMARY KEY (ServerKind, CollectionKey, TargetItemId)
                );
                CREATE INDEX IF NOT EXISTS IX_ManagedSeasonCollectionMembers_Rule
                    ON ManagedSeasonCollectionMembers(ServerKind, RuleKey);
                CREATE TABLE IF NOT EXISTS ManagedSeasonCollectionAssets (
                    ServerKind TEXT NOT NULL,
                    CollectionKey TEXT NOT NULL,
                    CollectionItemId TEXT NULL,
                    LockAppliedByPlugin INTEGER NOT NULL DEFAULT 0,
                    PrimaryFingerprint TEXT NULL,
                    ThumbFingerprint TEXT NULL,
                    BackdropFingerprint TEXT NULL,
                    PrimaryWrittenFileIdentity TEXT NULL,
                    ThumbWrittenFileIdentity TEXT NULL,
                    BackdropWrittenFileIdentity TEXT NULL,
                    LastGeneratedAtUtc TEXT NULL,
                    LastError TEXT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    PRIMARY KEY (ServerKind, CollectionKey)
                );
                CREATE TABLE IF NOT EXISTS SeasonMetadataSnapshots (
                    ServerKind TEXT NOT NULL,
                    SeriesItemId TEXT NOT NULL,
                    SeriesName TEXT NULL,
                    InputFingerprint TEXT NOT NULL,
                    ResolvedAtUtc TEXT NOT NULL,
                    ExpiresAtUtc TEXT NOT NULL,
                    LastError TEXT NULL,
                    PRIMARY KEY (ServerKind, SeriesItemId)
                );
                CREATE INDEX IF NOT EXISTS IX_SeasonMetadataSnapshots_Expiry
                    ON SeasonMetadataSnapshots(ServerKind, ExpiresAtUtc);
                CREATE TABLE IF NOT EXISTS SeasonMetadataRows (
                    ServerKind TEXT NOT NULL,
                    SeriesItemId TEXT NOT NULL,
                    SeasonItemId TEXT NOT NULL,
                    SeasonName TEXT NOT NULL,
                    SeasonNumber INTEGER NULL,
                    Status TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    SameAsSeries INTEGER NOT NULL,
                    AnimeThemesSlug TEXT NULL,
                    AniListId INTEGER NULL,
                    MyAnimeListId INTEGER NULL,
                    AnimeYear INTEGER NULL,
                    AnimeSeason TEXT NULL,
                    PRIMARY KEY (ServerKind, SeriesItemId, SeasonItemId)
                );
                CREATE INDEX IF NOT EXISTS IX_SeasonMetadataRows_Broadcast
                    ON SeasonMetadataRows(ServerKind, AnimeYear, AnimeSeason);
                CREATE TABLE IF NOT EXISTS ApiFetchCache (
                    ServerKind TEXT NOT NULL,
                    CacheKey TEXT NOT NULL,
                    Provider TEXT NOT NULL,
                    PayloadJson TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    ExpiresAtUtc TEXT NOT NULL,
                    PRIMARY KEY (ServerKind, CacheKey)
                );
                CREATE INDEX IF NOT EXISTS IX_ApiFetchCache_Expiry
                    ON ApiFetchCache(ServerKind, ExpiresAtUtc);
                """);
            var storedSchemaVersion = GetStoredSchemaVersion(connection, transaction);

            // Ensure AnimeYear column exists unconditionally for safety (handles cases where schema version is 5 but column is missing)
            EnsureColumn(connection, transaction, "SeasonFinderRows", "AnimeYear", "INTEGER NULL");

            if (storedSchemaVersion < CurrentSchemaVersion)
            {
                UpsertMetadata(connection, transaction, "SchemaVersion", CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture));
            }

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
    public SeasonFinderItemsPage QueryRows(string? libraryId, int? startIndex, int? limit, string? searchTerm, string? status, int? seasonNumber, string? sortBy, string? sortOrder)
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

            if (seasonNumber.HasValue)
            {
                where.Add("SeasonNumber = $seasonNumber");
                parameters.Add(("$seasonNumber", seasonNumber.Value));
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
                       AnimeThemesSlug, AnimeThemesUrl, AniListId, MyAnimeListId, PrimaryImageUrl,
                       AnimeYear
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
            using var seasonNumbersCommand = connection.CreateCommand();
            seasonNumbersCommand.CommandText = "SELECT DISTINCT SeasonNumber FROM SeasonFinderRows WHERE ServerKind = $serverKind AND SeasonNumber IS NOT NULL ORDER BY SeasonNumber;";
            seasonNumbersCommand.Parameters.AddWithValue("$serverKind", ServerKind);
            using var seasonNumbersReader = seasonNumbersCommand.ExecuteReader();
            var seasonNumbers = new List<int>();
            while (seasonNumbersReader.Read())
            {
                seasonNumbers.Add(seasonNumbersReader.GetInt32(0));
            }

            return new SeasonFinderItemsPage(items, count, normalizedStart, normalizedLimit, cacheState.Version, cacheState.Ready, seasonNumbers);
        }
    }

    /// <inheritdoc />
    public SeasonAutomationState GetSeasonAutomationState(string seriesItemId)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            var state = new SeasonAutomationState { SeriesItemId = seriesItemId };
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT RuleKey, SeriesItemId, SeasonItemId, SeasonName, SeasonNumber, AnimeThemesSlug,
                           AniListId, MyAnimeListId, AnimeYear, AnimeSeason, BroadcastSeasonKey,
                           BroadcastSeasonLabel, Source, ResolvedAtUtc, LastError, UpdatedAtUtc
                    FROM SeasonAutomationRules WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId
                    ORDER BY SeasonNumber, SeasonName;
                    """;
                command.Parameters.AddWithValue("$serverKind", ServerKind);
                command.Parameters.AddWithValue("$seriesId", seriesItemId);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    state.Rules.Add(new SeasonAutomationRuleRecord
                    {
                        RuleKey = reader.GetString(0),
                        SeriesItemId = reader.GetString(1),
                        SeasonItemId = reader.GetString(2),
                        SeasonName = reader.GetString(3),
                        SeasonNumber = GetNullableInt32(reader, 4),
                        AnimeThemesSlug = GetNullableString(reader, 5),
                        AniListId = GetNullableInt32(reader, 6),
                        MyAnimeListId = GetNullableInt32(reader, 7),
                        AnimeYear = GetNullableInt32(reader, 8),
                        AnimeSeason = GetNullableString(reader, 9),
                        BroadcastSeasonKey = GetNullableString(reader, 10),
                        BroadcastSeasonLabel = GetNullableString(reader, 11),
                        Source = reader.GetString(12),
                        ResolvedAtUtc = GetNullableString(reader, 13),
                        LastError = GetNullableString(reader, 14),
                        UpdatedAtUtc = reader.GetString(15),
                    });
                }
            }

            var ruleKeys = state.Rules.Select(i => i.RuleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (ruleKeys.Count == 0)
            {
                return state;
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT t.RuleKey, t.TargetItemId, t.TargetItemType, t.TagName, t.Source,
                           t.AddedByPlugin, t.LastAppliedAtUtc, t.LastError, t.UpdatedAtUtc
                    FROM SeasonAutomationTags t
                    INNER JOIN SeasonAutomationRules r ON r.ServerKind = t.ServerKind AND r.RuleKey = t.RuleKey
                    WHERE t.ServerKind = $serverKind AND r.SeriesItemId = $seriesId;
                    """;
                command.Parameters.AddWithValue("$serverKind", ServerKind);
                command.Parameters.AddWithValue("$seriesId", seriesItemId);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    state.Tags.Add(new SeasonAutomationTagRecord
                    {
                        RuleKey = reader.GetString(0), TargetItemId = reader.GetString(1), TargetItemType = reader.GetString(2),
                        TagName = reader.GetString(3), Source = reader.GetString(4), AddedByPlugin = reader.GetInt32(5) != 0,
                        LastAppliedAtUtc = GetNullableString(reader, 6), LastError = GetNullableString(reader, 7), UpdatedAtUtc = reader.GetString(8),
                    });
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT m.CollectionKey, m.RuleKey, m.TargetItemId, m.TargetItemType, m.AddedByPlugin,
                           m.LastAppliedAtUtc, m.LastError, m.UpdatedAtUtc,
                           c.CollectionName, c.CollectionItemId, c.IsPluginCreated, c.LastError, c.UpdatedAtUtc
                    FROM ManagedSeasonCollectionMembers m
                    INNER JOIN SeasonAutomationRules r ON r.ServerKind = m.ServerKind AND r.RuleKey = m.RuleKey
                    LEFT JOIN ManagedSeasonCollections c ON c.ServerKind = m.ServerKind AND c.CollectionKey = m.CollectionKey
                    WHERE m.ServerKind = $serverKind AND r.SeriesItemId = $seriesId;
                    """;
                command.Parameters.AddWithValue("$serverKind", ServerKind);
                command.Parameters.AddWithValue("$seriesId", seriesItemId);
                using var reader = command.ExecuteReader();
                var collectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (reader.Read())
                {
                    state.CollectionMembers.Add(new ManagedSeasonCollectionMemberRecord
                    {
                        CollectionKey = reader.GetString(0), RuleKey = reader.GetString(1), TargetItemId = reader.GetString(2),
                        TargetItemType = reader.GetString(3), AddedByPlugin = reader.GetInt32(4) != 0,
                        LastAppliedAtUtc = GetNullableString(reader, 5), LastError = GetNullableString(reader, 6), UpdatedAtUtc = reader.GetString(7),
                    });
                    if (!reader.IsDBNull(8) && collectionKeys.Add(reader.GetString(0)))
                    {
                        state.Collections.Add(new ManagedSeasonCollectionRecord
                        {
                            CollectionKey = reader.GetString(0), CollectionName = reader.GetString(8),
                            CollectionItemId = GetNullableString(reader, 9), IsPluginCreated = reader.GetInt32(10) != 0,
                            LastError = GetNullableString(reader, 11), UpdatedAtUtc = reader.GetString(12),
                        });
                    }
                }
            }

            return state;
        }
    }

    /// <inheritdoc />
    public void SaveSeasonAutomationState(SeasonAutomationState state)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, """
                DELETE FROM SeasonAutomationTags WHERE ServerKind = $serverKind AND RuleKey IN
                    (SELECT RuleKey FROM SeasonAutomationRules WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId);
                DELETE FROM ManagedSeasonCollectionMembers WHERE ServerKind = $serverKind AND RuleKey IN
                    (SELECT RuleKey FROM SeasonAutomationRules WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId);
                DELETE FROM SeasonAutomationRules WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId;
                """, ("$serverKind", ServerKind), ("$seriesId", state.SeriesItemId));

            foreach (var rule in state.Rules)
            {
                Execute(connection, transaction, """
                    INSERT INTO SeasonAutomationRules (ServerKind, RuleKey, SeriesItemId, SeasonItemId, SeasonName,
                        SeasonNumber, AnimeThemesSlug, AniListId, MyAnimeListId, AnimeYear, AnimeSeason,
                        BroadcastSeasonKey, BroadcastSeasonLabel, Source, ResolvedAtUtc, LastError, UpdatedAtUtc)
                    VALUES ($serverKind, $ruleKey, $seriesId, $seasonId, $seasonName, $seasonNumber, $slug,
                        $aniListId, $malId, $year, $season, $broadcastKey, $broadcastLabel, $source,
                        $resolved, $error, $updated);
                    """, ("$serverKind", ServerKind), ("$ruleKey", rule.RuleKey), ("$seriesId", rule.SeriesItemId),
                    ("$seasonId", rule.SeasonItemId), ("$seasonName", rule.SeasonName), ("$seasonNumber", rule.SeasonNumber),
                    ("$slug", rule.AnimeThemesSlug), ("$aniListId", rule.AniListId), ("$malId", rule.MyAnimeListId),
                    ("$year", rule.AnimeYear), ("$season", rule.AnimeSeason), ("$broadcastKey", rule.BroadcastSeasonKey),
                    ("$broadcastLabel", rule.BroadcastSeasonLabel), ("$source", rule.Source),
                    ("$resolved", rule.ResolvedAtUtc), ("$error", rule.LastError), ("$updated", rule.UpdatedAtUtc));
            }

            foreach (var tag in state.Tags)
            {
                Execute(connection, transaction, """
                    INSERT INTO SeasonAutomationTags (ServerKind, RuleKey, TargetItemId, TargetItemType, TagName,
                        Source, AddedByPlugin, LastAppliedAtUtc, LastError, UpdatedAtUtc)
                    VALUES ($serverKind, $ruleKey, $targetId, $targetType, $tag, $source, $added, $applied, $error, $updated);
                    """, ("$serverKind", ServerKind), ("$ruleKey", tag.RuleKey), ("$targetId", tag.TargetItemId),
                    ("$targetType", tag.TargetItemType), ("$tag", tag.TagName), ("$source", tag.Source),
                    ("$added", tag.AddedByPlugin ? 1 : 0), ("$applied", tag.LastAppliedAtUtc),
                    ("$error", tag.LastError), ("$updated", tag.UpdatedAtUtc));
            }

            foreach (var collection in state.Collections)
            {
                Execute(connection, transaction, """
                    INSERT INTO ManagedSeasonCollections (ServerKind, CollectionKey, CollectionName, CollectionItemId,
                        IsPluginCreated, LastError, UpdatedAtUtc)
                    VALUES ($serverKind, $key, $name, $id, $created, $error, $updated)
                    ON CONFLICT(ServerKind, CollectionKey) DO UPDATE SET CollectionName = excluded.CollectionName,
                        CollectionItemId = excluded.CollectionItemId, IsPluginCreated = excluded.IsPluginCreated,
                        LastError = excluded.LastError, UpdatedAtUtc = excluded.UpdatedAtUtc;
                    """, ("$serverKind", ServerKind), ("$key", collection.CollectionKey), ("$name", collection.CollectionName),
                    ("$id", collection.CollectionItemId), ("$created", collection.IsPluginCreated ? 1 : 0),
                    ("$error", collection.LastError), ("$updated", collection.UpdatedAtUtc));
            }

            foreach (var member in state.CollectionMembers)
            {
                Execute(connection, transaction, """
                    INSERT INTO ManagedSeasonCollectionMembers (ServerKind, CollectionKey, RuleKey, TargetItemId,
                        TargetItemType, AddedByPlugin, LastAppliedAtUtc, LastError, UpdatedAtUtc)
                    VALUES ($serverKind, $key, $ruleKey, $targetId, $targetType, $added, $applied, $error, $updated);
                    """, ("$serverKind", ServerKind), ("$key", member.CollectionKey), ("$ruleKey", member.RuleKey),
                    ("$targetId", member.TargetItemId), ("$targetType", member.TargetItemType),
                    ("$added", member.AddedByPlugin ? 1 : 0), ("$applied", member.LastAppliedAtUtc),
                    ("$error", member.LastError), ("$updated", member.UpdatedAtUtc));
            }

            transaction.Commit();
        }
    }

    /// <inheritdoc />
    public SeasonMetadataSnapshot? GetSeasonMetadataSnapshot(string seriesItemId)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            SeasonMetadataSnapshot? snapshot;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT SeriesItemId, SeriesName, InputFingerprint, ResolvedAtUtc, ExpiresAtUtc, LastError
                    FROM SeasonMetadataSnapshots
                    WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId;
                    """;
                command.Parameters.AddWithValue("$serverKind", ServerKind);
                command.Parameters.AddWithValue("$seriesId", seriesItemId);
                using var reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    return null;
                }

                snapshot = new SeasonMetadataSnapshot
                {
                    SeriesItemId = reader.GetString(0),
                    SeriesName = GetNullableString(reader, 1),
                    InputFingerprint = reader.GetString(2),
                    ResolvedAtUtc = reader.GetString(3),
                    ExpiresAtUtc = reader.GetString(4),
                    LastError = GetNullableString(reader, 5),
                };
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT SeasonItemId, SeasonName, SeasonNumber, Status, Source, SameAsSeries,
                           AnimeThemesSlug, AniListId, MyAnimeListId, AnimeYear, AnimeSeason
                    FROM SeasonMetadataRows
                    WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId
                    ORDER BY SeasonNumber, SeasonName;
                    """;
                command.Parameters.AddWithValue("$serverKind", ServerKind);
                command.Parameters.AddWithValue("$seriesId", seriesItemId);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    snapshot.Seasons.Add(new SeasonMetadataRow
                    {
                        SeasonItemId = reader.GetString(0),
                        SeasonName = reader.GetString(1),
                        SeasonNumber = GetNullableInt32(reader, 2),
                        Status = reader.GetString(3),
                        Source = reader.GetString(4),
                        SameAsSeries = reader.GetInt32(5) != 0,
                        AnimeThemesSlug = GetNullableString(reader, 6),
                        AniListId = GetNullableInt32(reader, 7),
                        MyAnimeListId = GetNullableInt32(reader, 8),
                        AnimeYear = GetNullableInt32(reader, 9),
                        AnimeSeason = GetNullableString(reader, 10),
                    });
                }
            }

            return snapshot;
        }
    }

    /// <inheritdoc />
    public void SaveSeasonMetadataSnapshot(SeasonMetadataSnapshot snapshot)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, """
                INSERT INTO SeasonMetadataSnapshots (ServerKind, SeriesItemId, SeriesName, InputFingerprint,
                    ResolvedAtUtc, ExpiresAtUtc, LastError)
                VALUES ($serverKind, $seriesId, $seriesName, $fingerprint, $resolved, $expires, $error)
                ON CONFLICT(ServerKind, SeriesItemId) DO UPDATE SET SeriesName = excluded.SeriesName,
                    InputFingerprint = excluded.InputFingerprint, ResolvedAtUtc = excluded.ResolvedAtUtc,
                    ExpiresAtUtc = excluded.ExpiresAtUtc, LastError = excluded.LastError;
                DELETE FROM SeasonMetadataRows WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId;
                """, ("$serverKind", ServerKind), ("$seriesId", snapshot.SeriesItemId), ("$seriesName", snapshot.SeriesName),
                ("$fingerprint", snapshot.InputFingerprint), ("$resolved", snapshot.ResolvedAtUtc),
                ("$expires", snapshot.ExpiresAtUtc), ("$error", snapshot.LastError));
            foreach (var row in snapshot.Seasons)
            {
                Execute(connection, transaction, """
                    INSERT INTO SeasonMetadataRows (ServerKind, SeriesItemId, SeasonItemId, SeasonName, SeasonNumber,
                        Status, Source, SameAsSeries, AnimeThemesSlug, AniListId, MyAnimeListId, AnimeYear, AnimeSeason)
                    VALUES ($serverKind, $seriesId, $seasonId, $seasonName, $seasonNumber, $status, $source,
                        $sameAsSeries, $slug, $aniListId, $malId, $year, $season);
                    """, ("$serverKind", ServerKind), ("$seriesId", snapshot.SeriesItemId),
                    ("$seasonId", row.SeasonItemId), ("$seasonName", row.SeasonName), ("$seasonNumber", row.SeasonNumber),
                    ("$status", row.Status), ("$source", row.Source), ("$sameAsSeries", row.SameAsSeries ? 1 : 0),
                    ("$slug", row.AnimeThemesSlug), ("$aniListId", row.AniListId), ("$malId", row.MyAnimeListId),
                    ("$year", row.AnimeYear), ("$season", row.AnimeSeason));
            }

            transaction.Commit();
        }
    }

    /// <inheritdoc />
    public ApiFetchCacheEntry? GetApiFetchCache(string cacheKey)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT CacheKey, Provider, PayloadJson, CreatedAtUtc, ExpiresAtUtc
                FROM ApiFetchCache WHERE ServerKind = $serverKind AND CacheKey = $key;
                """;
            command.Parameters.AddWithValue("$serverKind", ServerKind);
            command.Parameters.AddWithValue("$key", cacheKey);
            using var reader = command.ExecuteReader();
            return reader.Read()
                ? new ApiFetchCacheEntry
                {
                    CacheKey = reader.GetString(0), Provider = reader.GetString(1), PayloadJson = reader.GetString(2),
                    CreatedAtUtc = reader.GetString(3), ExpiresAtUtc = reader.GetString(4),
                }
                : null;
        }
    }

    /// <inheritdoc />
    public void UpsertApiFetchCache(ApiFetchCacheEntry entry, int ttlDays = 30)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, """
                INSERT INTO ApiFetchCache (ServerKind, CacheKey, Provider, PayloadJson, CreatedAtUtc, ExpiresAtUtc)
                VALUES ($serverKind, $key, $provider, $json, $created, $expires)
                ON CONFLICT(ServerKind, CacheKey) DO UPDATE SET Provider = excluded.Provider,
                    PayloadJson = excluded.PayloadJson, CreatedAtUtc = excluded.CreatedAtUtc,
                    ExpiresAtUtc = excluded.ExpiresAtUtc;
                """, ("$serverKind", ServerKind), ("$key", entry.CacheKey), ("$provider", entry.Provider),
                ("$json", entry.PayloadJson), ("$created", entry.CreatedAtUtc), ("$expires", entry.ExpiresAtUtc));
            ttlDays = Math.Clamp(ttlDays, 1, 365);
            Execute(connection, transaction,
                "DELETE FROM ApiFetchCache WHERE ServerKind = $serverKind AND CreatedAtUtc < $retention;",
                ("$serverKind", ServerKind), ("$retention", FormatDate(DateTimeOffset.UtcNow.AddDays(-ttlDays).Subtract(ApiFetchCacheRetention))));
            Execute(connection, transaction, """
                DELETE FROM ApiFetchCache WHERE ServerKind = $serverKind AND CacheKey IN (
                    SELECT CacheKey FROM ApiFetchCache WHERE ServerKind = $serverKind
                    ORDER BY CreatedAtUtc DESC LIMIT -1 OFFSET $limit
                );
                """, ("$serverKind", ServerKind), ("$limit", ApiFetchCacheLimit));
            transaction.Commit();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ManagedSeasonCollectionAssetState> GetCollectionAssetStates()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT CollectionKey, CollectionItemId, LockAppliedByPlugin, PrimaryFingerprint, ThumbFingerprint,
                       BackdropFingerprint, PrimaryWrittenFileIdentity, ThumbWrittenFileIdentity,
                       BackdropWrittenFileIdentity, LastGeneratedAtUtc, LastError, UpdatedAtUtc
                FROM ManagedSeasonCollectionAssets WHERE ServerKind = $serverKind;
                """;
            command.Parameters.AddWithValue("$serverKind", ServerKind);
            using var reader = command.ExecuteReader();
            var states = new List<ManagedSeasonCollectionAssetState>();
            while (reader.Read())
            {
                states.Add(new ManagedSeasonCollectionAssetState
                {
                    CollectionKey = reader.GetString(0),
                    CollectionItemId = GetNullableString(reader, 1),
                    LockAppliedByPlugin = reader.GetInt32(2) != 0,
                    PrimaryFingerprint = GetNullableString(reader, 3),
                    ThumbFingerprint = GetNullableString(reader, 4),
                    BackdropFingerprint = GetNullableString(reader, 5),
                    PrimaryWrittenFileIdentity = GetNullableString(reader, 6),
                    ThumbWrittenFileIdentity = GetNullableString(reader, 7),
                    BackdropWrittenFileIdentity = GetNullableString(reader, 8),
                    LastGeneratedAtUtc = GetNullableString(reader, 9),
                    LastError = GetNullableString(reader, 10),
                    UpdatedAtUtc = reader.GetString(11),
                });
            }

            return states;
        }
    }

    /// <inheritdoc />
    public void UpsertCollectionAssetState(ManagedSeasonCollectionAssetState state)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            Execute(connection, null, """
                INSERT INTO ManagedSeasonCollectionAssets (ServerKind, CollectionKey, CollectionItemId,
                    LockAppliedByPlugin, PrimaryFingerprint, ThumbFingerprint, BackdropFingerprint,
                    PrimaryWrittenFileIdentity, ThumbWrittenFileIdentity, BackdropWrittenFileIdentity,
                    LastGeneratedAtUtc, LastError, UpdatedAtUtc)
                VALUES ($serverKind, $key, $itemId, $lock, $primaryFp, $thumbFp, $backdropFp,
                    $primaryId, $thumbId, $backdropId, $generated, $error, $updated)
                ON CONFLICT(ServerKind, CollectionKey) DO UPDATE SET
                    CollectionItemId = excluded.CollectionItemId,
                    LockAppliedByPlugin = excluded.LockAppliedByPlugin,
                    PrimaryFingerprint = excluded.PrimaryFingerprint,
                    ThumbFingerprint = excluded.ThumbFingerprint,
                    BackdropFingerprint = excluded.BackdropFingerprint,
                    PrimaryWrittenFileIdentity = excluded.PrimaryWrittenFileIdentity,
                    ThumbWrittenFileIdentity = excluded.ThumbWrittenFileIdentity,
                    BackdropWrittenFileIdentity = excluded.BackdropWrittenFileIdentity,
                    LastGeneratedAtUtc = excluded.LastGeneratedAtUtc,
                    LastError = excluded.LastError,
                    UpdatedAtUtc = excluded.UpdatedAtUtc;
                """, ("$serverKind", ServerKind), ("$key", state.CollectionKey), ("$itemId", state.CollectionItemId),
                ("$lock", state.LockAppliedByPlugin ? 1 : 0), ("$primaryFp", state.PrimaryFingerprint),
                ("$thumbFp", state.ThumbFingerprint), ("$backdropFp", state.BackdropFingerprint),
                ("$primaryId", state.PrimaryWrittenFileIdentity), ("$thumbId", state.ThumbWrittenFileIdentity),
                ("$backdropId", state.BackdropWrittenFileIdentity), ("$generated", state.LastGeneratedAtUtc),
                ("$error", state.LastError), ("$updated", state.UpdatedAtUtc));
        }
    }

    /// <inheritdoc />
    public void DeleteCollectionAssetState(string collectionKey)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            Execute(connection, null, "DELETE FROM ManagedSeasonCollectionAssets WHERE ServerKind = $serverKind AND CollectionKey = $key;",
                ("$serverKind", ServerKind), ("$key", collectionKey));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SeasonSummary> GetSeasonSummaries(string seriesItemId)
    {
        return GetSeasonAutomationState(seriesItemId).Rules.Select(rule => new SeasonSummary(
            rule.SeasonItemId, rule.SeasonNumber, rule.SeasonName, rule.BroadcastSeasonKey,
            rule.BroadcastSeasonLabel, string.IsNullOrWhiteSpace(rule.LastError) ? "Resolved" : "Warning")).ToList();
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
                       AnimeThemesSlug, AnimeThemesUrl, AniListId, MyAnimeListId, PrimaryImageUrl,
                       AnimeYear
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
    /// Removes provider cache rows that fell out of the stale-retention window
    /// (TTL plus <see cref="ApiFetchCacheRetention"/>). Writes also prune on upsert;
    /// this explicit pass keeps idle databases tidy and keeps
    /// <see cref="GetCacheMaintenanceStatus"/> read-only.
    /// </summary>
    public void PruneProviderCaches(int providerResponseTtlDays)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            providerResponseTtlDays = Math.Clamp(providerResponseTtlDays, 1, 365);
            var retentionCutoff = FormatDate(DateTimeOffset.UtcNow.AddDays(-providerResponseTtlDays).Subtract(ApiFetchCacheRetention));
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, "DELETE FROM ApiFetchCache WHERE ServerKind = $serverKind AND CreatedAtUtc < $retention;", ("$serverKind", ServerKind), ("$retention", retentionCutoff));
            Execute(connection, transaction, "DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind AND CreatedAtUtc < $retention;", ("$serverKind", ServerKind), ("$retention", retentionCutoff));
            transaction.Commit();
        }
    }

    /// <inheritdoc />
    public CacheMaintenanceStatus GetCacheMaintenanceStatus(int seasonMetadataTtlDays, int providerResponseTtlDays)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            seasonMetadataTtlDays = Math.Clamp(seasonMetadataTtlDays, 1, 365);
            providerResponseTtlDays = Math.Clamp(providerResponseTtlDays, 1, 365);
            var now = DateTimeOffset.UtcNow;
            var seasonCutoff = now.AddDays(-seasonMetadataTtlDays);
            var providerCutoff = now.AddDays(-providerResponseTtlDays);
            using var connection = OpenConnection();
            var seasonResolved = new List<DateTimeOffset>();
            var seasonErrors = 0;
            string? lastSeasonError = null;
            DateTimeOffset? lastSeasonErrorAt = null;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT ResolvedAtUtc, LastError FROM SeasonMetadataSnapshots WHERE ServerKind = $serverKind;";
                command.Parameters.AddWithValue("$serverKind", ServerKind);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (DateTimeOffset.TryParse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var resolved))
                    {
                        seasonResolved.Add(resolved);
                    }

                    if (!reader.IsDBNull(1) && !string.IsNullOrWhiteSpace(reader.GetString(1)))
                    {
                        seasonErrors++;
                        if (DateTimeOffset.TryParse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var errorAt) &&
                            (!lastSeasonErrorAt.HasValue || errorAt > lastSeasonErrorAt.Value))
                        {
                            lastSeasonErrorAt = errorAt;
                            lastSeasonError = reader.GetString(1);
                        }
                    }
                }
            }

            var providerCreated = new List<DateTimeOffset>();
            var animeThemesEntries = 0;
            var aniListEntries = 0;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Provider, CreatedAtUtc FROM ApiFetchCache WHERE ServerKind = $serverKind;";
                command.Parameters.AddWithValue("$serverKind", ServerKind);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(0), "AniList", StringComparison.OrdinalIgnoreCase))
                    {
                        aniListEntries++;
                    }
                    else
                    {
                        animeThemesEntries++;
                    }

                    if (DateTimeOffset.TryParse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created))
                    {
                        providerCreated.Add(created);
                    }
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT CreatedAtUtc FROM AnimeSearchCache WHERE ServerKind = $serverKind;";
                command.Parameters.AddWithValue("$serverKind", ServerKind);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (DateTimeOffset.TryParse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created))
                    {
                        providerCreated.Add(created);
                    }
                }
            }

            using var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM SeasonMetadataRows WHERE ServerKind = $serverKind;";
            countCommand.Parameters.AddWithValue("$serverKind", ServerKind);
            var seasonRows = Convert.ToInt32(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
            var freshSeason = seasonResolved.Count(i => i > seasonCutoff);
            var freshProvider = providerCreated.Count(i => i > providerCutoff);
            // ApiFetchCache rows with an unparseable CreatedAtUtc are counted in the
            // provider buckets but not in providerCreated, so clamp the derived value.
            var searchEntries = Math.Max(0, providerCreated.Count - animeThemesEntries - aniListEntries);
            return new CacheMaintenanceStatus(
                seasonResolved.Count,
                seasonRows,
                freshSeason,
                seasonResolved.Count - freshSeason,
                seasonErrors,
                providerCreated.Count,
                freshProvider,
                providerCreated.Count - freshProvider,
                animeThemesEntries,
                aniListEntries,
                searchEntries,
                seasonMetadataTtlDays,
                providerResponseTtlDays,
                ApiFetchCacheLimit,
                (int)ApiFetchCacheRetention.TotalDays,
                seasonResolved.Count > 0 ? FormatDate(seasonResolved.Max()) : null,
                NextExpiry(seasonResolved, seasonCutoff, seasonMetadataTtlDays),
                NextExpiry(providerCreated, providerCutoff, providerResponseTtlDays),
                lastSeasonError);
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
            Execute(connection, transaction, "UPDATE SeasonFinderCacheState SET Ready = 0, CacheVersion = '', LastFullScanUtc = NULL, LastError = NULL, UpdatedAtUtc = $now WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind), ("$now", FormatDate(DateTimeOffset.UtcNow)));
            transaction.Commit();
        }
    }

    /// <inheritdoc />
    public void ClearProviderCache()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, "DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind; DELETE FROM ApiFetchCache WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
            transaction.Commit();
        }
    }

    /// <summary>
    /// Tries to get a non-expired AnimeThemes search response.
    /// </summary>
    public bool TryGetSearch(string query, int? year, out string json, int ttlDays = 30)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT ResultJson, CreatedAtUtc FROM AnimeSearchCache WHERE ServerKind = $serverKind AND QueryKey = $key;";
            command.Parameters.AddWithValue("$serverKind", ServerKind);
            command.Parameters.AddWithValue("$key", BuildQueryKey(query, year));
            using var reader = command.ExecuteReader();
            ttlDays = Math.Clamp(ttlDays, 1, 365);
            if (reader.Read() && DateTimeOffset.TryParse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created) && created.AddDays(ttlDays) > DateTimeOffset.UtcNow)
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
    public void SetSearch(string query, int? year, string json, int ttlDays = 30)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            var now = DateTimeOffset.UtcNow;
            ttlDays = Math.Clamp(ttlDays, 1, 365);
            Execute(connection, transaction, """
                INSERT INTO AnimeSearchCache (ServerKind, QueryKey, Query, Year, ResultJson, CreatedAtUtc, ExpiresAtUtc)
                VALUES ($serverKind, $key, $query, $year, $json, $created, $expires)
                ON CONFLICT(ServerKind, QueryKey) DO UPDATE SET
                    Query = excluded.Query, Year = excluded.Year, ResultJson = excluded.ResultJson,
                    CreatedAtUtc = excluded.CreatedAtUtc, ExpiresAtUtc = excluded.ExpiresAtUtc;
                """, ("$serverKind", ServerKind), ("$key", BuildQueryKey(query, year)), ("$query", query.Trim()),
                ("$year", year), ("$json", json), ("$created", FormatDate(now)), ("$expires", FormatDate(now.AddDays(ttlDays))));
            Execute(connection, transaction, "DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind AND CreatedAtUtc < $retention;", ("$serverKind", ServerKind), ("$retention", FormatDate(now.AddDays(-ttlDays).Subtract(ApiFetchCacheRetention))));
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

    private static string? NextExpiry(IReadOnlyList<DateTimeOffset> timestamps, DateTimeOffset cutoff, int ttlDays)
    {
        var next = timestamps.Where(i => i > cutoff).Select(i => i.AddDays(ttlDays)).OrderBy(i => i).FirstOrDefault();
        return next == default ? null : FormatDate(next);
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
                OutputRootItemId, OutputRootPath, OutputScope, SearchText, UpdatedAtUtc, AnimeYear)
            VALUES ($serverKind, $libraryId, $seriesId, $seriesName, $seriesPath, $seasonId, $seasonName,
                    $seasonPath, $seasonNumber, $status, $source, $sameAsSeries, $animeName, $animeThemesId,
                    $slug, $url, $aniListId, $malId, $image, $outputId, $outputPath, $outputScope, $search, $updated, $year)
            ON CONFLICT(ServerKind, SeasonItemId) DO UPDATE SET
                LibraryId = excluded.LibraryId, SeriesItemId = excluded.SeriesItemId, SeriesName = excluded.SeriesName,
                SeriesPath = excluded.SeriesPath, SeasonName = excluded.SeasonName, SeasonPath = excluded.SeasonPath,
                SeasonNumber = excluded.SeasonNumber, Status = excluded.Status, Source = excluded.Source,
                SameAsSeries = excluded.SameAsSeries, AnimeName = excluded.AnimeName,
                AnimeThemesId = excluded.AnimeThemesId, AnimeThemesSlug = excluded.AnimeThemesSlug,
                AnimeThemesUrl = excluded.AnimeThemesUrl, AniListId = excluded.AniListId,
                MyAnimeListId = excluded.MyAnimeListId, PrimaryImageUrl = excluded.PrimaryImageUrl,
                OutputRootItemId = excluded.OutputRootItemId, OutputRootPath = excluded.OutputRootPath,
                OutputScope = excluded.OutputScope, SearchText = excluded.SearchText, UpdatedAtUtc = excluded.UpdatedAtUtc,
                AnimeYear = excluded.AnimeYear;
            """, ("$serverKind", ServerKind), ("$libraryId", record.LibraryId), ("$seriesId", row.SeriesItemId.ToString("D")),
            ("$seriesName", row.SeriesName), ("$seriesPath", row.SeriesPath), ("$seasonId", row.SeasonItemId.ToString("D")),
            ("$seasonName", row.SeasonName), ("$seasonPath", row.SeasonPath), ("$seasonNumber", row.SeasonNumber),
            ("$status", row.Status), ("$source", row.Source), ("$sameAsSeries", row.SameAsSeries ? 1 : 0),
            ("$animeName", row.AnimeName), ("$animeThemesId", row.AnimeThemesId), ("$slug", row.AnimeThemesSlug),
            ("$url", row.AnimeThemesUrl), ("$aniListId", row.AniListId), ("$malId", row.MyAnimeListId),
            ("$image", row.PrimaryImageUrl), ("$outputId", record.OutputRootItemId), ("$outputPath", record.OutputRootPath),
            ("$outputScope", record.OutputScope), ("$search", searchText), ("$updated", FormatDate(DateTimeOffset.UtcNow)),
            ("$year", row.AnimeYear));
    }

    private static SeasonThemeMappingRow ReadRow(SqliteDataReader reader)
    {
        return new SeasonThemeMappingRow(
            Guid.Parse(reader.GetString(0)), reader.GetString(1), GetNullableString(reader, 2),
            Guid.Parse(reader.GetString(3)), reader.GetString(4), GetNullableString(reader, 5),
            GetNullableInt32(reader, 6), reader.GetString(7), reader.GetString(8), reader.GetInt64(9) != 0,
            GetNullableString(reader, 10), GetNullableInt32(reader, 11), GetNullableString(reader, 12),
            GetNullableString(reader, 13), GetNullableInt32(reader, 14), GetNullableInt32(reader, 15), GetNullableString(reader, 16),
            GetNullableInt32(reader, 17));
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

    private static int GetStoredSchemaVersion(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Value FROM SchemaMetadata WHERE Key = 'SchemaVersion';";
        return command.ExecuteScalar() is string value &&
               int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>
    /// Adds a column to an existing table when it is missing. Migration steps must stay
    /// idempotent because databases created before SchemaMetadata tracking report version 0.
    /// </summary>
    internal static void EnsureColumn(SqliteConnection connection, SqliteTransaction? transaction, string table, string column, string definition)
    {
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "PRAGMA table_info(" + table + ");";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        Execute(connection, transaction, "ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition + ";");
    }

    private static void UpsertMetadata(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
    {
        Execute(connection, transaction, "INSERT INTO SchemaMetadata (Key, Value) VALUES ($key, $value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;", ("$key", key), ("$value", value));
    }
}

#pragma warning restore SA1117

