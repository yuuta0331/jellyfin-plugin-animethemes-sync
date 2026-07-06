using System;
using System.IO;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using Microsoft.Data.Sqlite;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class SeasonFinderDataStoreTests
{
    [Fact]
    public void Rows_QueryUsesPersistentPagingSearchStatusAndSort()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.ReplaceRows(new[]
            {
                CreateRow("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Zeta", 2, "Manual", "chosen-anime"),
                CreateRow("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Alpha", 1, "Direct", "alpha-anime"),
                CreateRow("cccccccc-cccc-cccc-cccc-cccccccccccc", "Beta", 3, "Unmatched", null),
            });

            var first = store.QueryRows(null, 0, 2, null, "all", null, "seriesName", "asc");
            Assert.True(first.CacheReady);
            Assert.Equal(3, first.TotalRecordCount);
            Assert.Equal(2, first.Items.Count);
            Assert.Equal("Alpha", first.Items[0].SeriesName);
            Assert.Equal(new[] { 1, 2, 3 }, first.SeasonNumbers);

            var seasonTwo = store.QueryRows(null, 0, 80, null, "all", 2, "seriesName", "asc");
            Assert.Single(seasonTwo.Items);
            Assert.Equal(2, seasonTwo.Items[0].SeasonNumber);

            var automatic = store.QueryRows(null, 0, 80, null, "auto", null, "seriesName", "asc");
            Assert.Single(automatic.Items);
            Assert.Equal("Direct", automatic.Items[0].Status);

            var search = store.QueryRows(null, 0, 80, "chosen", "all", null, "seriesName", "asc");
            Assert.Single(search.Items);
            Assert.Equal("Zeta", search.Items[0].SeriesName);

            var reopened = CreateStore(directory);
            Assert.Equal(3, reopened.GetAllRows().Count);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void LegacyMappings_MigrateOnlyOnceAndPreserveLockedState()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.MigrateLegacyMappings(new[] { CreateMapping("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "first", true) });
            store.MigrateLegacyMappings(new[] { CreateMapping("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "second", false) });

            var mappings = store.GetSeasonThemeMappings();
            var mapping = Assert.Single(mappings);
            Assert.Equal("first", mapping.AnimeThemesSlug);
            Assert.True(mapping.Locked);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void SearchCache_PersistsAndOnlyProviderClearRemovesIt()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.MigrateLegacyMappings(new[] { CreateMapping("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "mapped", true) });
            store.SetSearch("  Example ", 2024, "[1]");

            var reopened = CreateStore(directory);
            Assert.True(reopened.TryGetSearch("example", 2024, out var json));
            Assert.Equal("[1]", json);

            using (var connection = new SqliteConnection("Data Source=" + store.DatabasePath + ";Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE AnimeSearchCache SET CreatedAtUtc = '2000-01-01T00:00:00Z';";
                command.ExecuteNonQuery();
            }

            Assert.False(reopened.TryGetSearch("example", 2024, out _));
            reopened.SetSearch("example", 2024, "[2]");
            reopened.ClearCache();
            Assert.Single(reopened.GetSeasonThemeMappings());
            Assert.False(reopened.IsCacheReady());
            Assert.True(reopened.TryGetSearch("example", 2024, out var preservedJson));
            Assert.Equal("[2]", preservedJson);

            reopened.ClearProviderCache();
            Assert.False(reopened.TryGetSearch("example", 2024, out _));
            Assert.Single(reopened.GetSeasonThemeMappings());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ServerKinds_AreIsolatedInSharedDatabase()
    {
        var directory = CreateTempDirectory();
        try
        {
            var jellyfin = CreateStore(directory, "Jellyfin");
            var emby = CreateStore(directory, "Emby");
            jellyfin.ReplaceRows(new[] { CreateRow("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Jellyfin Series", 1, "Manual", "jf") });
            emby.ReplaceRows(new[] { CreateRow("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Emby Series", 1, "Manual", "emby") });

            Assert.Equal("Jellyfin Series", Assert.Single(jellyfin.GetAllRows()).SeriesName);
            Assert.Equal("Emby Series", Assert.Single(emby.GetAllRows()).SeriesName);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void SearchCache_PrunesToConfiguredLimit()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            for (var index = 0; index < 205; index++)
            {
                store.SetSearch("query-" + index, null, "[]");
            }

            using var connection = new SqliteConnection("Data Source=" + store.DatabasePath + ";Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM AnimeSearchCache WHERE ServerKind = 'Test';";
            Assert.Equal(200L, Convert.ToInt64(command.ExecuteScalar()));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void AutomationState_PersistsNormalizedRulesTagsAndCollectionMembers()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var now = DateTimeOffset.UtcNow.ToString("O");
            store.SaveSeasonAutomationState(new SeasonAutomationState
            {
                SeriesItemId = "series-1",
                Rules =
                [
                    new SeasonAutomationRuleRecord
                    {
                        RuleKey = "id:season-2", SeriesItemId = "series-1", SeasonItemId = "season-2",
                        SeasonName = "Season 2", SeasonNumber = 2, AnimeYear = 2024, AnimeSeason = "summer",
                        BroadcastSeasonKey = "2024-summer", BroadcastSeasonLabel = "Summer 2024",
                        Source = "SeasonThemeMappings", UpdatedAtUtc = now,
                    },
                ],
                Tags =
                [
                    new SeasonAutomationTagRecord
                    {
                        RuleKey = "id:season-2", TargetItemId = "series-1", TargetItemType = "Series",
                        TagName = "Summer 2024", AddedByPlugin = true, UpdatedAtUtc = now,
                    },
                ],
                Collections =
                [
                    new ManagedSeasonCollectionRecord
                    {
                        CollectionKey = "2024-summer", CollectionName = "Summer 2024",
                        CollectionItemId = "collection-1", UpdatedAtUtc = now,
                    },
                ],
                CollectionMembers =
                [
                    new ManagedSeasonCollectionMemberRecord
                    {
                        CollectionKey = "2024-summer", RuleKey = "id:season-2", TargetItemId = "season-2",
                        TargetItemType = "Season", AddedByPlugin = true, UpdatedAtUtc = now,
                    },
                ],
            });

            var reopened = CreateStore(directory).GetSeasonAutomationState("series-1");
            Assert.Single(reopened.Rules);
            Assert.Single(reopened.Tags);
            Assert.Single(reopened.Collections);
            Assert.Single(reopened.CollectionMembers);
            var summary = Assert.Single(CreateStore(directory).GetSeasonSummaries("series-1"));
            Assert.Equal("Summer 2024", summary.BroadcastSeasonLabel);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void LegacyMappings_DeduplicateWithLockedMappingPriority()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var locked = CreateMapping("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "locked", true);
            var laterUnlocked = CreateMapping("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "unlocked", false);

            store.MigrateLegacyMappings(new[] { locked, laterUnlocked });

            var mapping = Assert.Single(store.GetSeasonThemeMappings());
            Assert.True(mapping.Locked);
            Assert.Equal("locked", mapping.AnimeThemesSlug);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void MappingChanges_UpdateOnlyTargetAndRemoveAlternateKeys()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var targetId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            store.ReplaceSeasonThemeMappings(new[]
            {
                CreateMapping(targetId, "by-id", false),
                new SeasonThemeMapping { SeasonPath = "/series/Season 1", AnimeThemesSlug = "by-path", Enabled = true },
                CreateMapping("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "untouched", true),
            }, "Test");
            var replacement = CreateMapping(targetId, "replacement", true);
            var target = new SeasonThemeMappingTarget(
                "dddddddd-dddd-dddd-dddd-dddddddddddd", "/series", targetId, "/series/Season 1", "/series", 1);

            store.ApplySeasonThemeMappingChanges([new SeasonThemeMappingChange(target, replacement, "Manual")]);

            var mappings = store.GetSeasonThemeMappings();
            Assert.Equal(2, mappings.Count);
            Assert.Contains(mappings, mapping => mapping.AnimeThemesSlug == "replacement");
            Assert.Contains(mappings, mapping => mapping.AnimeThemesSlug == "untouched");
            Assert.DoesNotContain(mappings, mapping => mapping.AnimeThemesSlug is "by-id" or "by-path");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void RowUpsert_BumpsCacheVersionWithoutReplacingOtherRows()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var firstId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            store.ReplaceRows(new[]
            {
                CreateRow(firstId, "Alpha", 1, "Unmatched", null),
                CreateRow("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Beta", 2, "Unmatched", null),
            });
            var before = store.QueryRows(null, 0, 80, null, "all", null, "seriesName", "asc").CacheVersion;
            Thread.Sleep(2);

            store.UpsertRow(CreateRow(firstId, "Alpha", 1, "Manual", "alpha"));

            var after = store.QueryRows(null, 0, 80, null, "all", null, "seriesName", "asc");
            Assert.Equal(2, after.TotalRecordCount);
            Assert.NotEqual(before, after.CacheVersion);
            Assert.Equal("Manual", after.Items[0].Status);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ReplaceRows_RollsBackWhenNewRowsCannotBeWritten()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.ReplaceRows(new[] { CreateRow("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Original", 1, "Unmatched", null) });
            var invalid = new SeasonFinderRowRecord { Row = null! };

            Assert.ThrowsAny<Exception>(() => store.ReplaceRows(new[] { CreateRow("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "New", 1, "Manual", "new"), invalid }));

            Assert.Equal("Original", Assert.Single(store.GetAllRows()).SeriesName);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void SearchCache_SeparatesYearsForNormalizedQuery()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.SetSearch(" Example ", 2023, "[2023]");
            store.SetSearch("example", 2024, "[2024]");

            Assert.True(store.TryGetSearch("EXAMPLE", 2023, out var first));
            Assert.True(store.TryGetSearch("example", 2024, out var second));
            Assert.Equal("[2023]", first);
            Assert.Equal("[2024]", second);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CollectionAssets_UpsertReadDeleteAndSurviveClearCache()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.UpsertCollectionAssetState(new ManagedSeasonCollectionAssetState
            {
                CollectionKey = "2024-winter",
                CollectionItemId = "collection-1",
                LockAppliedByPlugin = true,
                PrimaryFingerprint = "fp-primary",
                PrimaryWrittenFileIdentity = "100:200",
                UpdatedAtUtc = "2026-07-03T00:00:00.0000000+00:00",
            });
            store.UpsertCollectionAssetState(new ManagedSeasonCollectionAssetState
            {
                CollectionKey = "2024-winter",
                CollectionItemId = "collection-1",
                LockAppliedByPlugin = true,
                PrimaryFingerprint = "fp-primary-2",
                PrimaryWrittenFileIdentity = "101:201",
                ThumbFingerprint = "fp-thumb",
                UpdatedAtUtc = "2026-07-03T01:00:00.0000000+00:00",
            });

            store.ClearCache();

            var reopened = CreateStore(directory);
            var state = Assert.Single(reopened.GetCollectionAssetStates());
            Assert.Equal("2024-winter", state.CollectionKey);
            Assert.True(state.LockAppliedByPlugin);
            Assert.Equal("fp-primary-2", state.PrimaryFingerprint);
            Assert.Equal("101:201", state.PrimaryWrittenFileIdentity);
            Assert.Equal("fp-thumb", state.ThumbFingerprint);
            Assert.Null(state.BackdropFingerprint);

            reopened.DeleteCollectionAssetState("2024-winter");
            Assert.Empty(reopened.GetCollectionAssetStates());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CollectionAssets_AreScopedByServerKind()
    {
        var directory = CreateTempDirectory();
        try
        {
            var jellyfinStore = CreateStore(directory, "Jellyfin");
            var embyStore = CreateStore(directory, "Emby");
            jellyfinStore.UpsertCollectionAssetState(new ManagedSeasonCollectionAssetState
            {
                CollectionKey = "2024-winter",
                UpdatedAtUtc = "2026-07-03T00:00:00.0000000+00:00",
            });

            Assert.Single(jellyfinStore.GetCollectionAssetStates());
            Assert.Empty(embyStore.GetCollectionAssetStates());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void SeasonMetadataAndApiFetchCache_PersistAndSurviveBrowserCacheClear()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.SaveSeasonMetadataSnapshot(new SeasonMetadataSnapshot
            {
                SeriesItemId = "series-1",
                SeriesName = "Example",
                InputFingerprint = "fingerprint-1",
                ResolvedAtUtc = "2026-07-04T00:00:00.0000000+00:00",
                ExpiresAtUtc = "2026-08-03T00:00:00.0000000+00:00",
                Seasons =
                [
                    new SeasonMetadataRow
                    {
                        SeasonItemId = "season-1", SeasonName = "Season 1", SeasonNumber = 1,
                        Status = "Direct", Source = "SeasonProviderIds", SameAsSeries = false,
                        AnimeThemesSlug = "example", AniListId = 100, MyAnimeListId = 200,
                        AnimeYear = 2024, AnimeSeason = "SPRING",
                    },
                ],
            });
            store.UpsertApiFetchCache(new ApiFetchCacheEntry
            {
                CacheKey = "animethemes:slug:example",
                Provider = "AnimeThemes",
                PayloadJson = "{\"slug\":\"example\"}",
                CreatedAtUtc = "2026-07-04T00:00:00.0000000+00:00",
                ExpiresAtUtc = "2026-08-03T00:00:00.0000000+00:00",
            });

            store.ClearCache();

            var reopened = CreateStore(directory);
            var snapshot = reopened.GetSeasonMetadataSnapshot("series-1");
            Assert.NotNull(snapshot);
            Assert.Equal("fingerprint-1", snapshot.InputFingerprint);
            var season = Assert.Single(snapshot.Seasons);
            Assert.Equal("example", season.AnimeThemesSlug);
            Assert.Equal(2024, season.AnimeYear);
            var api = reopened.GetApiFetchCache("animethemes:slug:example");
            Assert.NotNull(api);
            Assert.Equal("AnimeThemes", api.Provider);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CacheMaintenanceStatus_AppliesCurrentTtlToExistingRows()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var created = DateTimeOffset.UtcNow.AddDays(-10);
            store.SaveSeasonMetadataSnapshot(new SeasonMetadataSnapshot
            {
                SeriesItemId = "series-1",
                InputFingerprint = "fingerprint-1",
                ResolvedAtUtc = created.ToString("O"),
                ExpiresAtUtc = created.AddDays(30).ToString("O"),
                LastError = "Most recent resolver error",
                Seasons = [new SeasonMetadataRow { SeasonItemId = "season-1", SeasonNumber = 1 }],
            });
            store.UpsertApiFetchCache(new ApiFetchCacheEntry
            {
                CacheKey = "animethemes:slug:example",
                Provider = "AnimeThemes",
                PayloadJson = "{}",
                CreatedAtUtc = created.ToString("O"),
                ExpiresAtUtc = created.AddDays(30).ToString("O"),
            });

            var extended = store.GetCacheMaintenanceStatus(30, 30);
            Assert.Equal(1, extended.FreshSeasonSeriesCount);
            Assert.Equal(1, extended.FreshProviderEntryCount);
            Assert.Equal("Most recent resolver error", extended.LastSeasonError);

            var shortened = store.GetCacheMaintenanceStatus(5, 5);
            Assert.Equal(1, shortened.ExpiredSeasonSeriesCount);
            Assert.Equal(1, shortened.StaleProviderEntryCount);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CacheMaintenanceStatus_IsReadOnlyAndPruneRemovesRetiredRows()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.EnsureInitialized();
            var retired = DateTimeOffset.UtcNow.AddDays(-400).ToString("O");
            using (var connection = new SqliteConnection("Data Source=" + store.DatabasePath + ";Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO ApiFetchCache (ServerKind, CacheKey, Provider, PayloadJson, CreatedAtUtc, ExpiresAtUtc)
                    VALUES ('Test', 'animethemes:slug:retired', 'AnimeThemes', '{}', $created, $created);
                    INSERT INTO AnimeSearchCache (ServerKind, QueryKey, Query, Year, ResultJson, CreatedAtUtc, ExpiresAtUtc)
                    VALUES ('Test', 'retired|', 'retired', NULL, '[]', $created, $created);
                    """;
                command.Parameters.AddWithValue("$created", retired);
                command.ExecuteNonQuery();
            }

            _ = store.GetCacheMaintenanceStatus(30, 30);
            Assert.Equal(2, CountProviderCacheRows(store.DatabasePath));

            store.PruneProviderCaches(30);
            Assert.Equal(0, CountProviderCacheRows(store.DatabasePath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static int CountProviderCacheRows(string databasePath)
    {
        using var connection = new SqliteConnection("Data Source=" + databasePath + ";Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT (SELECT COUNT(*) FROM ApiFetchCache) + (SELECT COUNT(*) FROM AnimeSearchCache);";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void ClearProviderCache_PreservesSeasonMetadataAndBrowserProjection()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.ReplaceRows([CreateRow("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Example", 1, "Direct", "example")]);
            store.SaveSeasonMetadataSnapshot(new SeasonMetadataSnapshot
            {
                SeriesItemId = "series-1",
                InputFingerprint = "fingerprint-1",
                ResolvedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30).ToString("O"),
            });
            store.SetSearch("example", 2024, "[]");
            store.UpsertApiFetchCache(new ApiFetchCacheEntry
            {
                CacheKey = "anilist:relations:1",
                Provider = "AniList",
                PayloadJson = "{}",
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30).ToString("O"),
            });

            store.ClearProviderCache();

            Assert.NotNull(store.GetSeasonMetadataSnapshot("series-1"));
            Assert.Single(store.GetAllRows());
            Assert.False(store.TryGetSearch("example", 2024, out _));
            Assert.Null(store.GetApiFetchCache("anilist:relations:1"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void SeasonMetadataAndApiFetchCache_AreScopedByServerKind()
    {
        var directory = CreateTempDirectory();
        try
        {
            var jellyfin = CreateStore(directory, "Jellyfin");
            var emby = CreateStore(directory, "Emby");
            jellyfin.SaveSeasonMetadataSnapshot(new SeasonMetadataSnapshot
            {
                SeriesItemId = "series-1", InputFingerprint = "jf", ResolvedAtUtc = "now", ExpiresAtUtc = "later",
            });
            jellyfin.UpsertApiFetchCache(new ApiFetchCacheEntry
            {
                CacheKey = "key", Provider = "provider", PayloadJson = "{}", CreatedAtUtc = "2026-07-04T00:00:00Z", ExpiresAtUtc = "2026-08-03T00:00:00Z",
            });

            Assert.NotNull(jellyfin.GetSeasonMetadataSnapshot("series-1"));
            Assert.Null(emby.GetSeasonMetadataSnapshot("series-1"));
            Assert.NotNull(jellyfin.GetApiFetchCache("key"));
            Assert.Null(emby.GetApiFetchCache("key"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ApiFetchCache_PrunesToConfiguredLimit()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.EnsureInitialized();
            using (var connection = new SqliteConnection("Data Source=" + store.DatabasePath + ";Pooling=False"))
            {
                connection.Open();
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO ApiFetchCache (ServerKind, CacheKey, Provider, PayloadJson, CreatedAtUtc, ExpiresAtUtc)
                    VALUES ('Test', $key, 'Test', '{}', $created, $expires);
                    """;
                var key = command.Parameters.Add("$key", Microsoft.Data.Sqlite.SqliteType.Text);
                var created = command.Parameters.Add("$created", Microsoft.Data.Sqlite.SqliteType.Text);
                var expires = command.Parameters.Add("$expires", Microsoft.Data.Sqlite.SqliteType.Text);
                var now = DateTimeOffset.UtcNow;
                for (var index = 0; index < 2001; index++)
                {
                    key.Value = "key-" + index;
                    created.Value = now.AddSeconds(index).ToString("O");
                    expires.Value = now.AddDays(30).ToString("O");
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }

            store.UpsertApiFetchCache(new ApiFetchCacheEntry
            {
                CacheKey = "newest", Provider = "Test", PayloadJson = "{}",
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(31).ToString("O"),
            });

            using var verify = new SqliteConnection("Data Source=" + store.DatabasePath + ";Pooling=False");
            verify.Open();
            using var count = verify.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM ApiFetchCache WHERE ServerKind = 'Test';";
            Assert.Equal(2000L, Convert.ToInt64(count.ExecuteScalar()));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ProviderCaches_RetainEntriesUntilCurrentTtlExpiryPlus180Days()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.EnsureInitialized();
            var now = DateTimeOffset.UtcNow;
            using (var connection = new SqliteConnection("Data Source=" + store.DatabasePath + ";Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO ApiFetchCache (ServerKind, CacheKey, Provider, PayloadJson, CreatedAtUtc, ExpiresAtUtc)
                    VALUES ('Test', $apiKey, 'AnimeThemes', '{}', $apiCreated, '2000-01-01T00:00:00Z');
                    INSERT INTO AnimeSearchCache (ServerKind, QueryKey, Query, Year, ResultJson, CreatedAtUtc, ExpiresAtUtc)
                    VALUES ('Test', $searchKey, $searchKey, NULL, '[]', $searchCreated, '2000-01-01T00:00:00Z');
                    """;
                var apiKey = command.Parameters.Add("$apiKey", Microsoft.Data.Sqlite.SqliteType.Text);
                var apiCreated = command.Parameters.Add("$apiCreated", Microsoft.Data.Sqlite.SqliteType.Text);
                var searchKey = command.Parameters.Add("$searchKey", Microsoft.Data.Sqlite.SqliteType.Text);
                var searchCreated = command.Parameters.Add("$searchCreated", Microsoft.Data.Sqlite.SqliteType.Text);
                foreach (var age in new[] { 200, 220 })
                {
                    apiKey.Value = "api-age-" + age;
                    apiCreated.Value = now.AddDays(-age).ToString("O");
                    searchKey.Value = "search-age-" + age;
                    searchCreated.Value = now.AddDays(-age).ToString("O");
                    command.ExecuteNonQuery();
                }
            }

            store.UpsertApiFetchCache(new ApiFetchCacheEntry
            {
                CacheKey = "trigger",
                Provider = "AnimeThemes",
                PayloadJson = "{}",
                CreatedAtUtc = now.ToString("O"),
                ExpiresAtUtc = now.AddDays(30).ToString("O"),
            }, 30);
            store.SetSearch("trigger", null, "[]", 30);

            using var verify = new SqliteConnection("Data Source=" + store.DatabasePath + ";Pooling=False");
            verify.Open();
            using var count = verify.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM ApiFetchCache WHERE ServerKind = 'Test' AND CacheKey = 'api-age-200';";
            Assert.Equal(1L, Convert.ToInt64(count.ExecuteScalar()));
            count.CommandText = "SELECT COUNT(*) FROM ApiFetchCache WHERE ServerKind = 'Test' AND CacheKey = 'api-age-220';";
            Assert.Equal(0L, Convert.ToInt64(count.ExecuteScalar()));
            count.CommandText = "SELECT COUNT(*) FROM AnimeSearchCache WHERE ServerKind = 'Test' AND QueryKey = 'search-age-200';";
            Assert.Equal(1L, Convert.ToInt64(count.ExecuteScalar()));
            count.CommandText = "SELECT COUNT(*) FROM AnimeSearchCache WHERE ServerKind = 'Test' AND QueryKey = 'search-age-220';";
            Assert.Equal(0L, Convert.ToInt64(count.ExecuteScalar()));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void EnsureInitialized_UpgradesStoredSchemaVersionWithoutTouchingData()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.MigrateLegacyMappings(new[] { CreateMapping("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "kept-slug", true) });

            // Simulate a database written by an older plugin version.
            using (var connection = new SqliteConnection("Data Source=" + store.DatabasePath + ";Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE SchemaMetadata SET Value = '1' WHERE Key = 'SchemaVersion';";
                command.ExecuteNonQuery();
            }

            var reopened = CreateStore(directory);
            reopened.EnsureInitialized();

            using (var connection = new SqliteConnection("Data Source=" + store.DatabasePath + ";Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT Value FROM SchemaMetadata WHERE Key = 'SchemaVersion';";
                Assert.Equal("5", command.ExecuteScalar());
            }

            var mapping = Assert.Single(reopened.GetSeasonThemeMappings());
            Assert.Equal("kept-slug", mapping.AnimeThemesSlug);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void EnsureColumn_AddsMissingColumnOnceAndIsIdempotent()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.EnsureInitialized();

            using var connection = new SqliteConnection("Data Source=" + store.DatabasePath + ";Pooling=False");
            connection.Open();
            SeasonFinderDataStore.EnsureColumn(connection, null, "SeasonFinderRows", "MigrationProbe", "TEXT NULL");
            SeasonFinderDataStore.EnsureColumn(connection, null, "SeasonFinderRows", "MigrationProbe", "TEXT NULL");

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(SeasonFinderRows);";
            using var reader = command.ExecuteReader();
            var found = 0;
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "MigrationProbe", StringComparison.OrdinalIgnoreCase))
                {
                    found++;
                }
            }

            Assert.Equal(1, found);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static SeasonFinderDataStore CreateStore(string directory, string serverKind = "Test")
    {
        return new SeasonFinderDataStore(new TestPathProvider(directory), new TestIdentityProvider(serverKind));
    }

    private static SeasonFinderRowRecord CreateRow(string seasonId, string seriesName, int seasonNumber, string status, string? slug)
    {
        return new SeasonFinderRowRecord
        {
            LibraryId = "11111111-1111-1111-1111-111111111111",
            Row = new SeasonThemeMappingRow(
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                seriesName,
                "/library/" + seriesName,
                Guid.Parse(seasonId),
                "Season " + seasonNumber,
                "/library/" + seriesName + "/Season " + seasonNumber,
                seasonNumber,
                status,
                status == "Unmatched" ? "None" : "Test",
                false,
                slug,
                null,
                slug,
                null,
                null,
                null,
                null),
        };
    }

    private static SeasonThemeMapping CreateMapping(string seasonId, string slug, bool locked)
    {
        return new SeasonThemeMapping
        {
            SeasonItemId = seasonId,
            AnimeThemesSlug = slug,
            Locked = locked,
            Enabled = true,
        };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ats-season-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TestPathProvider(string path) : IAnimeThemesDataPathProvider
    {
        public string GetPluginDataDirectory() => path;
    }

    private sealed class TestIdentityProvider(string serverKind) : IAnimeThemesServerIdentityProvider
    {
        public string ServerKind => serverKind;
    }
}

