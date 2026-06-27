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

            var first = store.QueryRows(null, 0, 2, null, "all", "seriesName", "asc");
            Assert.True(first.CacheReady);
            Assert.Equal(3, first.TotalRecordCount);
            Assert.Equal(2, first.Items.Count);
            Assert.Equal("Alpha", first.Items[0].SeriesName);

            var automatic = store.QueryRows(null, 0, 80, null, "auto", "seriesName", "asc");
            Assert.Single(automatic.Items);
            Assert.Equal("Direct", automatic.Items[0].Status);

            var search = store.QueryRows(null, 0, 80, "chosen", "all", "seriesName", "asc");
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
    public void SearchCache_PersistsExpiresAndClearsWithoutDeletingMappings()
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
                command.CommandText = "UPDATE AnimeSearchCache SET ExpiresAtUtc = '2000-01-01T00:00:00Z';";
                command.ExecuteNonQuery();
            }

            Assert.False(reopened.TryGetSearch("example", 2024, out _));
            reopened.ClearCache();
            Assert.Single(reopened.GetSeasonThemeMappings());
            Assert.False(reopened.IsCacheReady());
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
            var before = store.QueryRows(null, 0, 80, null, "all", "seriesName", "asc").CacheVersion;
            Thread.Sleep(2);

            store.UpsertRow(CreateRow(firstId, "Alpha", 1, "Manual", "alpha"));

            var after = store.QueryRows(null, 0, 80, null, "all", "seriesName", "asc");
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

