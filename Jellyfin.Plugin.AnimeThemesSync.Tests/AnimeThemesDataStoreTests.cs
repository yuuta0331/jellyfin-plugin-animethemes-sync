using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class AnimeThemesDataStoreTests
{
    [Fact]
    public void EnsureInitialized_CreatesAndReopensDatabase()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.EnsureInitialized();

            Assert.True(File.Exists(store.DatabasePath));

            var reopened = CreateStore(directory);
            reopened.EnsureInitialized();

            Assert.Equal(store.DatabasePath, reopened.DatabasePath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void BrowserItems_QueryUsesPagingSearchSortAndFilters()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.ReplaceBrowserItems(
                new[]
                {
                    CreateBrowserItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Alpha", "Series", videos: 1, songs: 0, extras: 0, bytes: 10),
                    CreateBrowserItem("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Beta", "Movie", videos: 0, songs: 1, extras: 1, bytes: 20),
                    CreateBrowserItem("cccccccc-cccc-cccc-cccc-cccccccccccc", "Gamma", "Series", videos: 0, songs: 0, extras: 0, bytes: 0),
                },
                new (string LibraryId, string? LibraryName, int ItemCount)[] { ("11111111-1111-1111-1111-111111111111", "Anime", 3) });

            var page = store.QueryBrowserItems(null, 0, 2, "SortName", "Ascending", null, "all", "all", "all");
            Assert.Equal(3, page.TotalRecordCount);
            Assert.Equal(2, page.Items.Count);
            Assert.Equal("Alpha", page.Items[0].Name);

            var search = store.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", "bet", "all", "all", "all");
            Assert.Single(search.Items);
            Assert.Equal("Beta", search.Items[0].Name);

            var saved = store.QueryBrowserItems(null, 0, 80, "ThemeBytes", "Descending", null, "all", "all", "saved");
            Assert.Equal(2, saved.TotalRecordCount);
            Assert.Equal("Beta", saved.Items[0].Name);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void BrowserItems_QueryFiltersAndReturnsBroadcastSeasons()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var winter = CreateBrowserItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Winter Show", "Series", 0, 0, 0, 0);
            winter.BroadcastSeasons.Add(new BroadcastSeasonValue("2024-winter", "Winter 2024", 2024, "winter"));
            var spring = CreateBrowserItem("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Spring Show", "Series", 0, 0, 0, 0);
            spring.BroadcastSeasons.Add(new BroadcastSeasonValue("2025-spring", "Spring 2025", 2025, "spring"));
            store.ReplaceBrowserItems(new[] { winter, spring }, Array.Empty<(string, string?, int)>());

            var page = store.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all", "2024-winter");

            Assert.Single(page.Items);
            Assert.Equal("Winter Show", page.Items[0].Name);
            Assert.Equal(new[] { "2024-winter" }, page.Items[0].BroadcastSeasonKeys);
            Assert.Equal(new[] { "2025-spring", "2024-winter" }, page.BroadcastSeasons!.Select(i => i.Key));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void SeasonMetadataState_PersistsAcrossCacheClear()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.SaveSeasonMetadataState(new SeasonMetadataState
            {
                SeriesItemId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                ManagedTags = new Dictionary<string, List<string>>
                {
                    ["aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"] = new() { "Winter 2024" },
                },
                BroadcastSeasons = new() { new BroadcastSeasonValue("2024-winter", "Winter 2024", 2024, "winter") },
            });

            store.ClearBrowserCache();
            store.ResetSharedStateForTests();
            var reopened = CreateStore(directory);
            reopened.EnsureInitialized();
            var state = reopened.GetSeasonMetadataState("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

            Assert.NotNull(state);
            Assert.Equal("Winter 2024", state.ManagedTags.Values.Single().Single());
            Assert.Equal("2024-winter", state.BroadcastSeasons.Single().Key);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void SeasonMetadataState_ReopensNullCollectionsAsEmpty()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.SaveSeasonMetadataState(new SeasonMetadataState
            {
                SeriesItemId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                ManagedTags = null!,
                CollectionMemberships = null!,
                BroadcastSeasons = null!,
            });

            store.ResetSharedStateForTests();
            var reopened = CreateStore(directory);
            reopened.EnsureInitialized();
            var state = reopened.GetSeasonMetadataState("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

            Assert.NotNull(state);
            Assert.Empty(state.ManagedTags);
            Assert.Empty(state.CollectionMemberships);
            Assert.Empty(state.BroadcastSeasons);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void BrowserItems_EmptyRebuildIsCacheReady()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.ReplaceBrowserItems(Array.Empty<BrowserItemRecord>(), Array.Empty<(string LibraryId, string? LibraryName, int ItemCount)>());

            var page = store.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all");
            var status = store.GetStorageStatus(false);

            Assert.True(page.CacheReady);
            Assert.True(status.CacheReady);
            Assert.Equal(0, status.BrowserItemCount);
            Assert.False(string.IsNullOrWhiteSpace(status.LastFullScanUtc));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ClearBrowserCache_MarksCacheNotReady()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.ReplaceBrowserItems(
                new[] { CreateBrowserItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Alpha", "Series", videos: 1, songs: 0, extras: 0, bytes: 10) },
                new (string LibraryId, string? LibraryName, int ItemCount)[] { ("11111111-1111-1111-1111-111111111111", "Anime", 1) });

            store.ClearBrowserCache();

            var page = store.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all");
            var status = store.GetStorageStatus(false);

            Assert.False(page.CacheReady);
            Assert.False(status.CacheReady);
            Assert.Equal(0, status.BrowserItemCount);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void EnsureInitialized_QuarantinesCorruptCache()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            Directory.CreateDirectory(directory);
            File.WriteAllText(store.DatabasePath, "{not json");

            store.EnsureInitialized();

            Assert.True(File.Exists(store.DatabasePath));
            Assert.Contains(Directory.GetFiles(directory), path => path.Contains(".corrupt-", StringComparison.Ordinal));
            Assert.False(store.GetStorageStatus(false).CacheReady);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void BrowserUi_KeepsHostSpecificStructure()
    {
        var embyHtml = ReadRepoFile("Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html");
        var embyJs = ReadRepoFile("Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js");
        var jellyfinHtml = ReadRepoFile("Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html");

        var embyToolbar = ExtractBrowserToolbar(embyHtml);
        Assert.DoesNotContain("AnimeThemesBrowserRebuildCache", embyToolbar, StringComparison.Ordinal);
        Assert.DoesNotContain("AnimeThemesBrowserClearCache", embyToolbar, StringComparison.Ordinal);
        Assert.DoesNotContain("AnimeThemesImportLegacyManifests", embyToolbar, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesBrowserRebuildCache", embyHtml, StringComparison.Ordinal);
        Assert.Contains("<i class=\"md-icon ats-icon\"", embyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("material-icons", embyHtml, StringComparison.Ordinal);

        var jellyfinToolbar = ExtractBrowserToolbar(jellyfinHtml);
        Assert.DoesNotContain("AnimeThemesBrowserRebuildCache", jellyfinToolbar, StringComparison.Ordinal);
        Assert.DoesNotContain("AnimeThemesBrowserClearCache", jellyfinToolbar, StringComparison.Ordinal);
        Assert.DoesNotContain("AnimeThemesImportLegacyManifests", jellyfinToolbar, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesBrowserRebuildCache", jellyfinHtml, StringComparison.Ordinal);
        Assert.Contains("material-icons ats-icon", jellyfinHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("md-icon", jellyfinHtml, StringComparison.Ordinal);

        Assert.DoesNotContain("Browser cache is empty", embyJs, StringComparison.Ordinal);
        Assert.DoesNotContain("Browser cache is empty", jellyfinHtml, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesBrowserPager", embyHtml, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesBrowserPager", jellyfinHtml, StringComparison.Ordinal);
        Assert.Contains("itemGrid.appendChild(loader)", embyJs, StringComparison.Ordinal);
        Assert.Contains("itemGrid.appendChild(loader)", jellyfinHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyExtrasManifest_IsImportedReadOnly()
    {
        var directory = CreateTempDirectory();
        var extrasDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(extrasDirectory, "old.webm"), "video");
            var manifestPath = Path.Combine(extrasDirectory, ThemeExtrasManifestService.ManifestFileName);
            File.WriteAllText(manifestPath, "{\"Files\":{\"theme-key\":\"old.webm\"}}");

            var store = CreateStore(directory);
            var result = store.ImportLegacyExtrasManifest(extrasDirectory);

            Assert.Equal(1, result.ManifestsImported);
            Assert.Equal(1, result.FilesImported);
            Assert.True(File.Exists(manifestPath));
            Assert.Equal(Path.Combine(extrasDirectory, "old.webm"), store.FindPreviousExtraPath(new ThemeExtraPlan(string.Empty, "unused.webm") { Key = "theme-key" }));
        }
        finally
        {
            DeleteDirectory(directory);
            DeleteDirectory(extrasDirectory);
        }
    }

    [Fact]
    public void ThemeFiles_SchemaV2MigratesLogicalIdAndStoresOutputRoot()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                store.DatabasePath,
                "{\"SchemaVersion\":1,\"ThemeFiles\":[{\"ServerKind\":\"Test\",\"ItemId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"ThemeKey\":\"OP1\",\"FileKind\":\"video\",\"Path\":\"old.webm\"}]}");

            store.EnsureInitialized();
            var target = new ThemeOutputTarget(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Path.Combine(directory, "Series"),
                ThemeOutputScope.SeriesRoot,
                true);
            store.UpsertThemeFile(target, "ED1", "audio", Path.Combine(target.OutputRootPath, "theme-music", "ED1.mp3"));
            store.UpdateExtraFile(new ThemeExtraPlan(
                string.Empty,
                Path.Combine(target.OutputRootPath, "extras", "ED1-other.webm"))
            {
                Key = "extra-key",
                OutputTarget = target
            });

            var json = File.ReadAllText(store.DatabasePath);
            Assert.Contains("\"SchemaVersion\":7", json, StringComparison.Ordinal);
            Assert.Contains("\"LogicalItemId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"", json, StringComparison.Ordinal);
            Assert.Contains("\"LogicalItemId\":\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\"", json, StringComparison.Ordinal);
            Assert.Contains("\"OutputRootItemId\":\"cccccccc-cccc-cccc-cccc-cccccccccccc\"", json, StringComparison.Ordinal);
            Assert.Contains("\"OutputScope\":\"SeriesRoot\"", json, StringComparison.Ordinal);
            Assert.Contains("\"Source\":\"TrackedUnknown\"", json, StringComparison.Ordinal);
            Assert.Contains("\"Key\":\"extra-key\",\"LogicalItemId\":\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\"", json, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ClearBrowserCache_PreservesThemeFileOwnershipRegistry()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var target = new ThemeOutputTarget(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Path.Combine(directory, "Series"),
                ThemeOutputScope.SeriesRoot,
                false);
            var path = Path.Combine(target.OutputRootPath, "theme-music", "theme.mp3");
            store.UpsertThemeFile(target, "OP1", "audio", path, "BrowserManual");

            store.ClearBrowserCache();

            var file = Assert.Single(store.GetThemeFiles());
            Assert.Equal(path, file.Path);
            Assert.Equal("BrowserManual", file.Source);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ExtrasTracking_DoesNotMigrateLegacyFileAcrossOutputRoots()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var legacyPath = Path.Combine(directory, "Series", "Season 01", "extras", "legacy.webm");
            store.UpdateExtraFile(new ThemeExtraPlan(string.Empty, legacyPath) { Key = "theme-key" });

            var target = new ThemeOutputTarget(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Path.Combine(directory, "Series"),
                ThemeOutputScope.SeriesRoot,
                true);
            var newPlan = new ThemeExtraPlan(
                string.Empty,
                Path.Combine(target.OutputRootPath, "extras", "Season 01 - new.webm"))
            {
                Key = "theme-key",
                OutputTarget = target
            };

            Assert.Null(store.FindPreviousExtraPath(newPlan));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void BrowserItems_BroadcastSeasonAggregationRefreshesAfterWrites()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var winter = CreateBrowserItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Winter Show", "Series", 0, 0, 0, 0);
            winter.BroadcastSeasons.Add(new BroadcastSeasonValue("2024-winter", "Winter 2024", 2024, "winter"));
            store.ReplaceBrowserItems(new[] { winter }, Array.Empty<(string, string?, int)>());

            // Query twice so the second response is served from the memo.
            _ = store.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all");
            var memoized = store.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all");
            Assert.Equal(new[] { "2024-winter" }, memoized.BroadcastSeasons!.Select(i => i.Key));

            var spring = CreateBrowserItem("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Spring Show", "Series", 0, 0, 0, 0);
            spring.BroadcastSeasons.Add(new BroadcastSeasonValue("2025-spring", "Spring 2025", 2025, "spring"));
            store.UpsertBrowserItem(spring);

            var refreshed = store.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all");
            Assert.Equal(new[] { "2025-spring", "2024-winter" }, refreshed.BroadcastSeasons!.Select(i => i.Key));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void LoadDocument_RecoversFromInterruptedSaveWithValidTempFile()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            File.WriteAllText(
                store.DatabasePath + ".tmp",
                "{\"SchemaVersion\":5,\"BrowserItems\":[{\"ServerKind\":\"Test\",\"ItemId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"Name\":\"Recovered\",\"ItemType\":\"Series\"}]}");

            store.EnsureInitialized();

            Assert.True(File.Exists(store.DatabasePath));
            Assert.False(File.Exists(store.DatabasePath + ".tmp"));
            var page = store.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all");
            Assert.Equal("Recovered", Assert.Single(page.Items).Name);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void LoadDocument_QuarantinesCorruptTempLeftover()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            File.WriteAllText(store.DatabasePath + ".tmp", "{partial write");

            store.EnsureInitialized();

            Assert.True(File.Exists(store.DatabasePath));
            Assert.False(File.Exists(store.DatabasePath + ".tmp"));
            Assert.Contains(Directory.GetFiles(directory), path => path.Contains(".tmp.corrupt-", StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void LoadDocument_KeepsMainFileWhenStaleTempExists()
    {
        var directory = CreateTempDirectory();
        try
        {
            var seed = CreateStore(directory);
            seed.ReplaceBrowserItems(
                new[] { CreateBrowserItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Kept", "Series", videos: 0, songs: 0, extras: 0, bytes: 0) },
                Array.Empty<(string, string?, int)>());
            File.WriteAllText(seed.DatabasePath + ".tmp", "{partial write");

            seed.ResetSharedStateForTests();
            var reopened = CreateStore(directory);
            reopened.EnsureInitialized();

            Assert.False(File.Exists(reopened.DatabasePath + ".tmp"));
            var page = reopened.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all");
            Assert.Equal("Kept", Assert.Single(page.Items).Name);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void QueryBrowserItems_SkipsRowsWithInvalidItemIds()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                store.DatabasePath,
                "{\"SchemaVersion\":5,\"BrowserItems\":[" +
                "{\"ServerKind\":\"Test\",\"ItemId\":\"not-a-guid\",\"Name\":\"Broken\",\"ItemType\":\"Series\"}," +
                "{\"ServerKind\":\"Test\",\"ItemId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"Name\":\"Valid\",\"ItemType\":\"Series\"}]}");

            var page = store.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all");

            Assert.Equal(1, page.TotalRecordCount);
            Assert.Equal("Valid", Assert.Single(page.Items).Name);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void UpsertBrowserItems_ReplacesExistingRowsAndLastDuplicateWins()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            store.ReplaceBrowserItems(
                new[] { CreateBrowserItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Original", "Series", videos: 0, songs: 0, extras: 0, bytes: 0) },
                Array.Empty<(string, string?, int)>());

            // Prime the query memo so the write below must invalidate it.
            _ = store.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all");

            store.UpsertBrowserItems(new[]
            {
                CreateBrowserItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "First Update", "Series", videos: 1, songs: 0, extras: 0, bytes: 1),
                CreateBrowserItem("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Added", "Movie", videos: 0, songs: 0, extras: 0, bytes: 0),
                CreateBrowserItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Second Update", "Series", videos: 2, songs: 0, extras: 0, bytes: 2),
            });

            var page = store.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all");
            Assert.Equal(2, page.TotalRecordCount);
            var updated = page.Items.Single(item => item.Name == "Second Update");
            Assert.Equal(2, updated.ThemeVideos);
            Assert.DoesNotContain(page.Items, item => item.Name is "Original" or "First Update");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ConcurrentInstances_ShareOneDocumentAndDoNotLoseWrites()
    {
        // Emby constructs a store per API request while the scheduled task holds its
        // own instance; both point at the same cache file within one process.
        var directory = CreateTempDirectory();
        try
        {
            var taskInstance = CreateStore(directory);
            taskInstance.EnsureInitialized();

            var requestInstance = CreateStore(directory);
            requestInstance.UpsertBrowserItem(CreateBrowserItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "FromRequest", "Series", videos: 0, songs: 0, extras: 0, bytes: 0));
            taskInstance.UpsertBrowserItem(CreateBrowserItem("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "FromTask", "Series", videos: 0, songs: 0, extras: 0, bytes: 0));

            taskInstance.ResetSharedStateForTests();
            var reloaded = CreateStore(directory);
            var page = reloaded.QueryBrowserItems(null, 0, 80, "SortName", "Ascending", null, "all", "all", "all");

            Assert.Equal(2, page.TotalRecordCount);
            Assert.Contains(page.Items, item => item.Name == "FromRequest");
            Assert.Contains(page.Items, item => item.Name == "FromTask");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void DownloadFailures_ArePersistedDeferredAndRemovedAfterSuccess()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var now = DateTimeOffset.UtcNow;
            const string url = "https://v.animethemes.moe/test.webm";
            var path = Path.Combine(directory, "test.webm");

            store.RecordDownloadFailure(
                url,
                path,
                DownloadFailureStatuses.TransientFailed,
                "503",
                503,
                now.AddMinutes(10));
            store.RecordDownloadFailure(
                url,
                path,
                DownloadFailureStatuses.TransientFailed,
                "503 again",
                503,
                now.AddMinutes(20));

            Assert.True(store.ShouldDeferDownload(url, path, now, out var failure));
            Assert.NotNull(failure);
            Assert.Equal(2, failure.AttemptCount);
            Assert.Equal(503, failure.LastStatusCode);

            store.ClearBrowserCache();
            Assert.True(store.ShouldDeferDownload(url, path, now, out _));

            store.RemoveDownloadFailure(url, path);
            Assert.False(store.ShouldDeferDownload(url, path, now, out _));
            Assert.Contains("\"SchemaVersion\":7", File.ReadAllText(store.DatabasePath), StringComparison.Ordinal);
            var issuePage = store.QueryManagerIssues(0, 80, "Resolved", "Download", null, null);
            var issue = Assert.Single(issuePage.Items);
            Assert.Equal(ManagerIssueStates.Resolved, issue.State);
            Assert.Equal(ManagerIssueCategories.Download, issue.Category);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ManagerIssueReads_DoNotRewriteCacheWhenNothingChanged()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var now = DateTimeOffset.UtcNow;
            store.RecordDownloadFailure(
                "https://v.animethemes.moe/steady.webm",
                Path.Combine(directory, "steady.webm"),
                DownloadFailureStatuses.PermanentFailed,
                "404",
                404,
                now.AddDays(30));

            // Prime the projection (this may legitimately write once).
            _ = store.QueryManagerIssues(0, 80, null, null, null, null);
            var snapshot = File.ReadAllBytes(store.DatabasePath);

            // Repeated reads over unchanged state must not rewrite the document.
            for (var i = 0; i < 3; i++)
            {
                _ = store.QueryManagerIssues(0, 80, null, null, null, null);
                _ = store.GetManagerIssueSummary();
            }

            Assert.Equal(snapshot, File.ReadAllBytes(store.DatabasePath));

            // A genuine state change still persists.
            var issueId = store.QueryManagerIssues(0, 80, null, "Download", null, null).Items.Single().Id;
            Assert.True(store.SetManagerIssueState(issueId, ManagerIssueStates.Ignored));
            Assert.NotEqual(snapshot, File.ReadAllBytes(store.DatabasePath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void DownloadFailures_PermanentEntryBecomesEligibleAtNextRetry()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var now = DateTimeOffset.UtcNow;
            const string url = "https://v.animethemes.moe/missing.webm";
            var path = Path.Combine(directory, "missing.webm");
            store.RecordDownloadFailure(
                url,
                path,
                DownloadFailureStatuses.PermanentFailed,
                "404",
                404,
                now.AddDays(30));

            Assert.True(store.ShouldDeferDownload(url, path, now.AddDays(29), out var failure));
            Assert.Equal(DownloadFailureStatuses.PermanentFailed, failure?.Status);
            Assert.False(store.ShouldDeferDownload(url, path, now.AddDays(31), out _));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static BrowserItemRecord CreateBrowserItem(string id, string name, string itemType, int videos, int songs, int extras, long bytes)
    {
        return new BrowserItemRecord
        {
            ItemId = id,
            LibraryId = "11111111-1111-1111-1111-111111111111",
            ItemType = itemType,
            Name = name,
            SortName = name,
            LinkStatus = "Unlinked",
            ThemeVideoCount = videos,
            ThemeSongCount = songs,
            ThemeExtraCount = extras,
            ThemeBytes = bytes,
            HasLocalThemes = videos + songs + extras > 0,
            DateCreatedUtc = DateTimeOffset.UtcNow,
            LastRefreshedUtc = DateTimeOffset.UtcNow
        };
    }

    private static AnimeThemesDataStore CreateStore(string directory)
    {
        return new AnimeThemesDataStore(new TestDataPathProvider(directory), new TestServerIdentityProvider());
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ats-store-" + Guid.NewGuid().ToString("N"));
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

    private static string ReadRepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find repository file.", Path.Combine(parts));
    }

    private static string ExtractBrowserToolbar(string html)
    {
        const string startToken = "<div class=\"ats-browser-toolbar\">";
        const string endToken = "<div class=\"ats-display-controls\">";
        var start = html.IndexOf(startToken, StringComparison.Ordinal);
        var end = html.IndexOf(endToken, start, StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.True(end > start);
        return html[start..end];
    }
}

internal sealed class TestDataPathProvider : IAnimeThemesDataPathProvider
{
    private readonly string _directory;

    public TestDataPathProvider(string directory)
    {
        _directory = directory;
    }

    public string GetPluginDataDirectory()
    {
        return _directory;
    }
}

internal sealed class TestServerIdentityProvider : IAnimeThemesServerIdentityProvider
{
    public string ServerKind => "Test";
}
