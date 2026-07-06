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
    private const int CurrentSchemaVersion = 5;
    private const int DefaultLimit = 80;
    private const int MaxLimit = 100;
    private const int SearchCacheLimit = 200;
    private const int ApiFetchCacheLimit = 2000;
    private static readonly TimeSpan ApiFetchCacheRetention = TimeSpan.FromDays(180);
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
                    """.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    connection.Execute(schemaStatement + ";");
                }
                var storedSchemaVersion = GetStoredSchemaVersion(connection);

                // Ensure AnimeYear column exists unconditionally for safety (handles cases where schema version is 5 but column is missing)
                EnsureColumn(connection, "SeasonFinderRows", "AnimeYear", "INTEGER NULL");

                if (storedSchemaVersion < CurrentSchemaVersion)
                {
                    UpsertMetadata(connection, "SchemaVersion", CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture));
                }

                MigrateDocumentStore(connection);
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

    public SeasonFinderItemsPage QueryRows(string? libraryId, int? startIndex, int? limit, string? searchTerm, string? status, int? seasonNumber, string? sortBy, string? sortOrder)
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
                       AnimeThemesSlug, AnimeThemesUrl, AniListId, MyAnimeListId, PrimaryImageUrl,
                       AnimeYear
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
            using var seasonStatement = Prepare(connection, "SELECT DISTINCT SeasonNumber FROM SeasonFinderRows WHERE ServerKind = $serverKind AND SeasonNumber IS NOT NULL ORDER BY SeasonNumber;", ("$serverKind", ServerKind));
            var seasonNumbers = new List<int>();
            while (seasonStatement.MoveNext())
            {
                seasonNumbers.Add(seasonStatement.Current.GetInt(0));
            }

            return new SeasonFinderItemsPage(items, count, normalizedStart, normalizedLimit, cacheState.Version, cacheState.Ready, seasonNumbers);
        }
    }

    public SeasonAutomationState GetSeasonAutomationState(string seriesItemId)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            var state = new SeasonAutomationState { SeriesItemId = seriesItemId };
            using (var statement = Prepare(connection, """
                SELECT RuleKey, SeriesItemId, SeasonItemId, SeasonName, SeasonNumber, AnimeThemesSlug,
                       AniListId, MyAnimeListId, AnimeYear, AnimeSeason, BroadcastSeasonKey,
                       BroadcastSeasonLabel, Source, ResolvedAtUtc, LastError, UpdatedAtUtc
                FROM SeasonAutomationRules WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId
                ORDER BY SeasonNumber, SeasonName;
                """, ("$serverKind", ServerKind), ("$seriesId", seriesItemId)))
            {
                while (statement.MoveNext())
                {
                    var row = statement.Current;
                    state.Rules.Add(new SeasonAutomationRuleRecord
                    {
                        RuleKey = row.GetString(0), SeriesItemId = row.GetString(1), SeasonItemId = row.GetString(2),
                        SeasonName = row.GetString(3), SeasonNumber = GetNullableInt32(row, 4), AnimeThemesSlug = GetNullableString(row, 5),
                        AniListId = GetNullableInt32(row, 6), MyAnimeListId = GetNullableInt32(row, 7), AnimeYear = GetNullableInt32(row, 8),
                        AnimeSeason = GetNullableString(row, 9), BroadcastSeasonKey = GetNullableString(row, 10),
                        BroadcastSeasonLabel = GetNullableString(row, 11), Source = row.GetString(12),
                        ResolvedAtUtc = GetNullableString(row, 13), LastError = GetNullableString(row, 14), UpdatedAtUtc = row.GetString(15),
                    });
                }
            }

            if (state.Rules.Count == 0)
            {
                return state;
            }

            using (var statement = Prepare(connection, """
                SELECT t.RuleKey, t.TargetItemId, t.TargetItemType, t.TagName, t.Source,
                       t.AddedByPlugin, t.LastAppliedAtUtc, t.LastError, t.UpdatedAtUtc
                FROM SeasonAutomationTags t
                INNER JOIN SeasonAutomationRules r ON r.ServerKind = t.ServerKind AND r.RuleKey = t.RuleKey
                WHERE t.ServerKind = $serverKind AND r.SeriesItemId = $seriesId;
                """, ("$serverKind", ServerKind), ("$seriesId", seriesItemId)))
            {
                while (statement.MoveNext())
                {
                    var row = statement.Current;
                    state.Tags.Add(new SeasonAutomationTagRecord
                    {
                        RuleKey = row.GetString(0), TargetItemId = row.GetString(1), TargetItemType = row.GetString(2),
                        TagName = row.GetString(3), Source = row.GetString(4), AddedByPlugin = row.GetInt64(5) != 0,
                        LastAppliedAtUtc = GetNullableString(row, 6), LastError = GetNullableString(row, 7), UpdatedAtUtc = row.GetString(8),
                    });
                }
            }

            using (var statement = Prepare(connection, """
                SELECT m.CollectionKey, m.RuleKey, m.TargetItemId, m.TargetItemType, m.AddedByPlugin,
                       m.LastAppliedAtUtc, m.LastError, m.UpdatedAtUtc,
                       c.CollectionName, c.CollectionItemId, c.IsPluginCreated, c.LastError, c.UpdatedAtUtc
                FROM ManagedSeasonCollectionMembers m
                INNER JOIN SeasonAutomationRules r ON r.ServerKind = m.ServerKind AND r.RuleKey = m.RuleKey
                LEFT JOIN ManagedSeasonCollections c ON c.ServerKind = m.ServerKind AND c.CollectionKey = m.CollectionKey
                WHERE m.ServerKind = $serverKind AND r.SeriesItemId = $seriesId;
                """, ("$serverKind", ServerKind), ("$seriesId", seriesItemId)))
            {
                var collectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (statement.MoveNext())
                {
                    var row = statement.Current;
                    state.CollectionMembers.Add(new ManagedSeasonCollectionMemberRecord
                    {
                        CollectionKey = row.GetString(0), RuleKey = row.GetString(1), TargetItemId = row.GetString(2),
                        TargetItemType = row.GetString(3), AddedByPlugin = row.GetInt64(4) != 0,
                        LastAppliedAtUtc = GetNullableString(row, 5), LastError = GetNullableString(row, 6), UpdatedAtUtc = row.GetString(7),
                    });
                    if (!row.IsDBNull(8) && collectionKeys.Add(row.GetString(0)))
                    {
                        state.Collections.Add(new ManagedSeasonCollectionRecord
                        {
                            CollectionKey = row.GetString(0), CollectionName = row.GetString(8), CollectionItemId = GetNullableString(row, 9),
                            IsPluginCreated = row.GetInt64(10) != 0, LastError = GetNullableString(row, 11), UpdatedAtUtc = row.GetString(12),
                        });
                    }
                }
            }

            return state;
        }
    }

    public void SaveSeasonAutomationState(SeasonAutomationState state)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                Execute(connection, """
                    DELETE FROM SeasonAutomationTags WHERE ServerKind = $serverKind AND RuleKey IN
                        (SELECT RuleKey FROM SeasonAutomationRules WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId);
                    """, ("$serverKind", ServerKind), ("$seriesId", state.SeriesItemId));
                Execute(connection, """
                    DELETE FROM ManagedSeasonCollectionMembers WHERE ServerKind = $serverKind AND RuleKey IN
                        (SELECT RuleKey FROM SeasonAutomationRules WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId);
                    """, ("$serverKind", ServerKind), ("$seriesId", state.SeriesItemId));
                Execute(connection, "DELETE FROM SeasonAutomationRules WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId;",
                    ("$serverKind", ServerKind), ("$seriesId", state.SeriesItemId));

                foreach (var rule in state.Rules)
                {
                    Execute(connection, """
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
                        ("$broadcastLabel", rule.BroadcastSeasonLabel), ("$source", rule.Source), ("$resolved", rule.ResolvedAtUtc),
                        ("$error", rule.LastError), ("$updated", rule.UpdatedAtUtc));
                }

                foreach (var tag in state.Tags)
                {
                    Execute(connection, """
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
                    Execute(connection, """
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
                    Execute(connection, """
                        INSERT INTO ManagedSeasonCollectionMembers (ServerKind, CollectionKey, RuleKey, TargetItemId,
                            TargetItemType, AddedByPlugin, LastAppliedAtUtc, LastError, UpdatedAtUtc)
                        VALUES ($serverKind, $key, $ruleKey, $targetId, $targetType, $added, $applied, $error, $updated);
                        """, ("$serverKind", ServerKind), ("$key", member.CollectionKey), ("$ruleKey", member.RuleKey),
                        ("$targetId", member.TargetItemId), ("$targetType", member.TargetItemType),
                        ("$added", member.AddedByPlugin ? 1 : 0), ("$applied", member.LastAppliedAtUtc),
                        ("$error", member.LastError), ("$updated", member.UpdatedAtUtc));
                }
            });
        }
    }

    public SeasonMetadataSnapshot? GetSeasonMetadataSnapshot(string seriesItemId)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            SeasonMetadataSnapshot snapshot;
            using (var statement = Prepare(connection, """
                SELECT SeriesItemId, SeriesName, InputFingerprint, ResolvedAtUtc, ExpiresAtUtc, LastError
                FROM SeasonMetadataSnapshots
                WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId;
                """, ("$serverKind", ServerKind), ("$seriesId", seriesItemId)))
            {
                if (!statement.MoveNext())
                {
                    return null;
                }

                var row = statement.Current;
                snapshot = new SeasonMetadataSnapshot
                {
                    SeriesItemId = row.GetString(0), SeriesName = GetNullableString(row, 1), InputFingerprint = row.GetString(2),
                    ResolvedAtUtc = row.GetString(3), ExpiresAtUtc = row.GetString(4), LastError = GetNullableString(row, 5),
                };
            }

            using (var statement = Prepare(connection, """
                SELECT SeasonItemId, SeasonName, SeasonNumber, Status, Source, SameAsSeries,
                       AnimeThemesSlug, AniListId, MyAnimeListId, AnimeYear, AnimeSeason
                FROM SeasonMetadataRows
                WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId
                ORDER BY SeasonNumber, SeasonName;
                """, ("$serverKind", ServerKind), ("$seriesId", seriesItemId)))
            {
                while (statement.MoveNext())
                {
                    var row = statement.Current;
                    snapshot.Seasons.Add(new SeasonMetadataRow
                    {
                        SeasonItemId = row.GetString(0), SeasonName = row.GetString(1), SeasonNumber = GetNullableInt32(row, 2),
                        Status = row.GetString(3), Source = row.GetString(4), SameAsSeries = row.GetInt64(5) != 0,
                        AnimeThemesSlug = GetNullableString(row, 6), AniListId = GetNullableInt32(row, 7),
                        MyAnimeListId = GetNullableInt32(row, 8), AnimeYear = GetNullableInt32(row, 9),
                        AnimeSeason = GetNullableString(row, 10),
                    });
                }
            }

            return snapshot;
        }
    }

    public void SaveSeasonMetadataSnapshot(SeasonMetadataSnapshot snapshot)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                Execute(connection, """
                    INSERT INTO SeasonMetadataSnapshots (ServerKind, SeriesItemId, SeriesName, InputFingerprint,
                        ResolvedAtUtc, ExpiresAtUtc, LastError)
                    VALUES ($serverKind, $seriesId, $seriesName, $fingerprint, $resolved, $expires, $error)
                    ON CONFLICT(ServerKind, SeriesItemId) DO UPDATE SET SeriesName = excluded.SeriesName,
                        InputFingerprint = excluded.InputFingerprint, ResolvedAtUtc = excluded.ResolvedAtUtc,
                        ExpiresAtUtc = excluded.ExpiresAtUtc, LastError = excluded.LastError;
                    """, ("$serverKind", ServerKind), ("$seriesId", snapshot.SeriesItemId), ("$seriesName", snapshot.SeriesName),
                    ("$fingerprint", snapshot.InputFingerprint), ("$resolved", snapshot.ResolvedAtUtc),
                    ("$expires", snapshot.ExpiresAtUtc), ("$error", snapshot.LastError));
                Execute(connection, "DELETE FROM SeasonMetadataRows WHERE ServerKind = $serverKind AND SeriesItemId = $seriesId;",
                    ("$serverKind", ServerKind), ("$seriesId", snapshot.SeriesItemId));
                foreach (var row in snapshot.Seasons)
                {
                    Execute(connection, """
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
            });
        }
    }

    public ApiFetchCacheEntry? GetApiFetchCache(string cacheKey)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var statement = Prepare(connection, """
                SELECT CacheKey, Provider, PayloadJson, CreatedAtUtc, ExpiresAtUtc
                FROM ApiFetchCache WHERE ServerKind = $serverKind AND CacheKey = $key;
                """, ("$serverKind", ServerKind), ("$key", cacheKey));
            if (!statement.MoveNext())
            {
                return null;
            }

            var row = statement.Current;
            return new ApiFetchCacheEntry
            {
                CacheKey = row.GetString(0), Provider = row.GetString(1), PayloadJson = row.GetString(2),
                CreatedAtUtc = row.GetString(3), ExpiresAtUtc = row.GetString(4),
            };
        }
    }

    public void UpsertApiFetchCache(ApiFetchCacheEntry entry, int ttlDays = 30)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                Execute(connection, """
                    INSERT INTO ApiFetchCache (ServerKind, CacheKey, Provider, PayloadJson, CreatedAtUtc, ExpiresAtUtc)
                    VALUES ($serverKind, $key, $provider, $json, $created, $expires)
                    ON CONFLICT(ServerKind, CacheKey) DO UPDATE SET Provider = excluded.Provider,
                        PayloadJson = excluded.PayloadJson, CreatedAtUtc = excluded.CreatedAtUtc,
                        ExpiresAtUtc = excluded.ExpiresAtUtc;
                    """, ("$serverKind", ServerKind), ("$key", entry.CacheKey), ("$provider", entry.Provider),
                    ("$json", entry.PayloadJson), ("$created", entry.CreatedAtUtc), ("$expires", entry.ExpiresAtUtc));
                ttlDays = Math.Clamp(ttlDays, 1, 365);
                Execute(connection, "DELETE FROM ApiFetchCache WHERE ServerKind = $serverKind AND CreatedAtUtc < $retention;",
                    ("$serverKind", ServerKind), ("$retention", FormatDate(DateTimeOffset.UtcNow.AddDays(-ttlDays).Subtract(ApiFetchCacheRetention))));
                Execute(connection, """
                    DELETE FROM ApiFetchCache WHERE ServerKind = $serverKind AND CacheKey IN (
                        SELECT CacheKey FROM ApiFetchCache WHERE ServerKind = $serverKind
                        ORDER BY CreatedAtUtc DESC LIMIT -1 OFFSET $limit
                    );
                    """, ("$serverKind", ServerKind), ("$limit", ApiFetchCacheLimit));
            });
        }
    }

    public IReadOnlyList<ManagedSeasonCollectionAssetState> GetCollectionAssetStates()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            using var statement = Prepare(connection, """
                SELECT CollectionKey, CollectionItemId, LockAppliedByPlugin, PrimaryFingerprint, ThumbFingerprint,
                       BackdropFingerprint, PrimaryWrittenFileIdentity, ThumbWrittenFileIdentity,
                       BackdropWrittenFileIdentity, LastGeneratedAtUtc, LastError, UpdatedAtUtc
                FROM ManagedSeasonCollectionAssets WHERE ServerKind = $serverKind;
                """, ("$serverKind", ServerKind));
            var states = new List<ManagedSeasonCollectionAssetState>();
            while (statement.MoveNext())
            {
                var row = statement.Current;
                states.Add(new ManagedSeasonCollectionAssetState
                {
                    CollectionKey = row.GetString(0),
                    CollectionItemId = GetNullableString(row, 1),
                    LockAppliedByPlugin = row.GetInt64(2) != 0,
                    PrimaryFingerprint = GetNullableString(row, 3),
                    ThumbFingerprint = GetNullableString(row, 4),
                    BackdropFingerprint = GetNullableString(row, 5),
                    PrimaryWrittenFileIdentity = GetNullableString(row, 6),
                    ThumbWrittenFileIdentity = GetNullableString(row, 7),
                    BackdropWrittenFileIdentity = GetNullableString(row, 8),
                    LastGeneratedAtUtc = GetNullableString(row, 9),
                    LastError = GetNullableString(row, 10),
                    UpdatedAtUtc = row.GetString(11),
                });
            }

            return states;
        }
    }

    public void UpsertCollectionAssetState(ManagedSeasonCollectionAssetState state)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            Execute(connection, """
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

    public void DeleteCollectionAssetState(string collectionKey)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            Execute(connection, "DELETE FROM ManagedSeasonCollectionAssets WHERE ServerKind = $serverKind AND CollectionKey = $key;",
                ("$serverKind", ServerKind), ("$key", collectionKey));
        }
    }

    public IReadOnlyList<SeasonSummary> GetSeasonSummaries(string seriesItemId)
    {
        return GetSeasonAutomationState(seriesItemId).Rules.Select(rule => new SeasonSummary(
            rule.SeasonItemId, rule.SeasonNumber, rule.SeasonName, rule.BroadcastSeasonKey,
            rule.BroadcastSeasonLabel, string.IsNullOrWhiteSpace(rule.LastError) ? "Resolved" : "Warning")).ToList();
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
                       AnimeThemesSlug, AnimeThemesUrl, AniListId, MyAnimeListId, PrimaryImageUrl,
                       AnimeYear
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
            InTransaction(connection, () =>
            {
                Execute(connection, "DELETE FROM ApiFetchCache WHERE ServerKind = $serverKind AND CreatedAtUtc < $retention;", ("$serverKind", ServerKind), ("$retention", retentionCutoff));
                Execute(connection, "DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind AND CreatedAtUtc < $retention;", ("$serverKind", ServerKind), ("$retention", retentionCutoff));
            });
        }
    }

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
            using (var statement = Prepare(connection, "SELECT ResolvedAtUtc, LastError FROM SeasonMetadataSnapshots WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind)))
            {
                while (statement.MoveNext())
                {
                    var row = statement.Current;
                    if (DateTimeOffset.TryParse(row.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var resolved))
                    {
                        seasonResolved.Add(resolved);
                    }

                    if (!row.IsDBNull(1) && !string.IsNullOrWhiteSpace(row.GetString(1)))
                    {
                        seasonErrors++;
                        if (DateTimeOffset.TryParse(row.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var errorAt) &&
                            (!lastSeasonErrorAt.HasValue || errorAt > lastSeasonErrorAt.Value))
                        {
                            lastSeasonErrorAt = errorAt;
                            lastSeasonError = row.GetString(1);
                        }
                    }
                }
            }

            var providerCreated = new List<DateTimeOffset>();
            var animeThemesEntries = 0;
            var aniListEntries = 0;
            using (var statement = Prepare(connection, "SELECT Provider, CreatedAtUtc FROM ApiFetchCache WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind)))
            {
                while (statement.MoveNext())
                {
                    var row = statement.Current;
                    if (string.Equals(row.GetString(0), "AniList", StringComparison.OrdinalIgnoreCase))
                    {
                        aniListEntries++;
                    }
                    else
                    {
                        animeThemesEntries++;
                    }

                    if (DateTimeOffset.TryParse(row.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created))
                    {
                        providerCreated.Add(created);
                    }
                }
            }

            using (var statement = Prepare(connection, "SELECT CreatedAtUtc FROM AnimeSearchCache WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind)))
            {
                while (statement.MoveNext())
                {
                    if (DateTimeOffset.TryParse(statement.Current.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created))
                    {
                        providerCreated.Add(created);
                    }
                }
            }

            var freshSeason = seasonResolved.Count(i => i > seasonCutoff);
            var freshProvider = providerCreated.Count(i => i > providerCutoff);
            // ApiFetchCache rows with an unparseable CreatedAtUtc are counted in the
            // provider buckets but not in providerCreated, so clamp the derived value.
            var searchEntries = Math.Max(0, providerCreated.Count - animeThemesEntries - aniListEntries);
            return new CacheMaintenanceStatus(
                seasonResolved.Count,
                ScalarInt(connection, "SELECT COUNT(*) FROM SeasonMetadataRows WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind)),
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
                Execute(connection, "UPDATE SeasonFinderCacheState SET Ready = 0, CacheVersion = '', LastFullScanUtc = NULL, LastError = NULL, UpdatedAtUtc = $now WHERE ServerKind = $serverKind;",
                    ("$serverKind", ServerKind), ("$now", FormatDate(DateTimeOffset.UtcNow)));
            });
        }
    }

    public void ClearProviderCache()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                Execute(connection, "DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
                Execute(connection, "DELETE FROM ApiFetchCache WHERE ServerKind = $serverKind;", ("$serverKind", ServerKind));
            });
        }
    }

    public bool TryGetSearch(string query, int? year, out string json, int ttlDays = 30)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            var key = BuildQueryKey(query, year);
            using var statement = Prepare(connection,
                "SELECT ResultJson, CreatedAtUtc FROM AnimeSearchCache WHERE ServerKind = $serverKind AND QueryKey = $key;",
                ("$serverKind", ServerKind), ("$key", key));
            ttlDays = Math.Clamp(ttlDays, 1, 365);
            if (statement.MoveNext() && DateTimeOffset.TryParse(statement.Current.GetString(1), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var created) && created.AddDays(ttlDays) > DateTimeOffset.UtcNow)
            {
                json = statement.Current.GetString(0);
                return true;
            }

            json = string.Empty;
            return false;
        }
    }

    public void SetSearch(string query, int? year, string json, int ttlDays = 30)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            using var connection = OpenConnection();
            InTransaction(connection, () =>
            {
                var now = DateTimeOffset.UtcNow;
                ttlDays = Math.Clamp(ttlDays, 1, 365);
                UpsertSearch(connection, BuildQueryKey(query, year), query.Trim(), year, json, FormatDate(now), FormatDate(now.AddDays(ttlDays)));
                Execute(connection, "DELETE FROM AnimeSearchCache WHERE ServerKind = $serverKind AND CreatedAtUtc < $retention;",
                    ("$serverKind", ServerKind), ("$retention", FormatDate(now.AddDays(-ttlDays).Subtract(ApiFetchCacheRetention))));
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

    private static string? NextExpiry(IReadOnlyList<DateTimeOffset> timestamps, DateTimeOffset cutoff, int ttlDays)
    {
        var next = timestamps.Where(i => i > cutoff).Select(i => i.AddDays(ttlDays)).OrderBy(i => i).FirstOrDefault();
        return next == default ? null : FormatDate(next);
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
                OutputRootPath, OutputScope, SearchText, UpdatedAtUtc, AnimeYear)
            VALUES ($serverKind, $libraryId, $seriesId, $seriesName, $seriesPath, $seasonId, $seasonName, $seasonPath,
                $seasonNumber, $status, $source, $sameAsSeries, $animeName, $animeThemesId, $slug, $url,
                $aniListId, $malId, $image, $outputId, $outputPath, $outputScope, $search, $updated, $year)
            ON CONFLICT(ServerKind, SeasonItemId) DO UPDATE SET LibraryId = excluded.LibraryId,
                SeriesItemId = excluded.SeriesItemId, SeriesName = excluded.SeriesName, SeriesPath = excluded.SeriesPath,
                SeasonName = excluded.SeasonName, SeasonPath = excluded.SeasonPath, SeasonNumber = excluded.SeasonNumber,
                Status = excluded.Status, Source = excluded.Source, SameAsSeries = excluded.SameAsSeries,
                AnimeName = excluded.AnimeName, AnimeThemesId = excluded.AnimeThemesId,
                AnimeThemesSlug = excluded.AnimeThemesSlug, AnimeThemesUrl = excluded.AnimeThemesUrl,
                AniListId = excluded.AniListId, MyAnimeListId = excluded.MyAnimeListId,
                PrimaryImageUrl = excluded.PrimaryImageUrl, OutputRootItemId = excluded.OutputRootItemId,
                OutputRootPath = excluded.OutputRootPath, OutputScope = excluded.OutputScope,
                SearchText = excluded.SearchText, UpdatedAtUtc = excluded.UpdatedAtUtc,
                AnimeYear = excluded.AnimeYear;
            """, ("$serverKind", ServerKind), ("$libraryId", record.LibraryId), ("$seriesId", row.SeriesItemId.ToString("D")),
            ("$seriesName", row.SeriesName), ("$seriesPath", row.SeriesPath), ("$seasonId", row.SeasonItemId.ToString("D")),
            ("$seasonName", row.SeasonName), ("$seasonPath", row.SeasonPath), ("$seasonNumber", row.SeasonNumber),
            ("$status", row.Status), ("$source", row.Source), ("$sameAsSeries", row.SameAsSeries ? 1 : 0),
            ("$animeName", row.AnimeName), ("$animeThemesId", row.AnimeThemesId), ("$slug", row.AnimeThemesSlug),
            ("$url", row.AnimeThemesUrl), ("$aniListId", row.AniListId), ("$malId", row.MyAnimeListId),
            ("$image", row.PrimaryImageUrl), ("$outputId", record.OutputRootItemId), ("$outputPath", record.OutputRootPath),
            ("$outputScope", record.OutputScope), ("$search", searchText),
            ("$updated", record.UpdatedAtUtc ?? FormatDate(DateTimeOffset.UtcNow)), ("$year", row.AnimeYear));
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
        GetNullableString(row, 13), GetNullableInt32(row, 14), GetNullableInt32(row, 15), GetNullableString(row, 16),
        GetNullableInt32(row, 17));

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

    private static int GetStoredSchemaVersion(IDatabaseConnection connection) =>
        GetMetadata(connection, "SchemaVersion") is string value &&
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    /// <summary>
    /// Adds a column to an existing table when it is missing. Migration steps must stay
    /// idempotent because databases created before SchemaMetadata tracking report version 0.
    /// </summary>
    internal static void EnsureColumn(IDatabaseConnection connection, string table, string column, string definition)
    {
        using (var statement = connection.PrepareStatement("PRAGMA table_info(" + table + ");"))
        {
            while (statement.MoveNext())
            {
                if (string.Equals(statement.Current.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        connection.Execute("ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition + ";");
    }

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
