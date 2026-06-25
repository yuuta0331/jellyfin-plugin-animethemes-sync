using System;
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
        Assert.Contains("(itemPager || itemGrid).appendChild(more)", embyJs, StringComparison.Ordinal);
        Assert.Contains("(itemPager || itemGrid).appendChild(more)", jellyfinHtml, StringComparison.Ordinal);
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
