using System;
using System.IO;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class SeasonFeatureParityTests
{
    [Fact]
    public void BrowserPages_ExposeSeasonSettingsAndBroadcastFilterForBothHosts()
    {
        var root = FindRepositoryRoot();
        var jellyfin = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        var embyHtml = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        var embyScript = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js"));
        var jellyfinDownloader = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"));
        var embyDownloader = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"));

        foreach (var marker in new[]
                 {
                     "AnimeThemesBrowserLibrarySeasonFilter",
                     "AtsSeasonTagTarget",
                     "AtsSeasonCollectionsEnabled",
                     "AtsSeasonOneCollectionUseSeries",
                     "AtsSeasonCollectionFormat",
                     "AtsCollectionOptions",
                     "AtsSeasonLabelOptions",
                     "AnimeThemesSeasonNumberFilter",
                 })
        {
            Assert.Contains(marker, jellyfin, StringComparison.Ordinal);
            Assert.Contains(marker, embyHtml, StringComparison.Ordinal);
        }

        Assert.Contains("broadcastSeason", jellyfin, StringComparison.Ordinal);
        Assert.Contains("broadcastSeason", embyScript, StringComparison.Ordinal);
        Assert.Contains("SeasonMetadata/Sync", jellyfin, StringComparison.Ordinal);
        Assert.Contains("SeasonMetadata/Sync", embyScript, StringComparison.Ordinal);
        Assert.Contains("normalizeSeasonTagTarget", jellyfin, StringComparison.Ordinal);
        Assert.Contains("normalizeSeasonTagTarget", embyScript, StringComparison.Ordinal);
        Assert.Contains("CompletedWithErrors", jellyfin, StringComparison.Ordinal);
        Assert.Contains("CompletedWithErrors", embyScript, StringComparison.Ordinal);
        Assert.Contains("config.SeasonCollectionFormat", jellyfinDownloader, StringComparison.Ordinal);
        Assert.Contains("config.SeasonCollectionFormat", embyDownloader, StringComparison.Ordinal);
    }

    [Fact]
    public void EmbySynchronizer_DefendsEmptyCollectionsAndRootSeriesParents()
    {
        var root = FindRepositoryRoot();
        var downloader = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"));

        Assert.Contains("GetChildrenIds(new InternalItemsQuery()) ?? Array.Empty<long>()", downloader, StringComparison.Ordinal);
        Assert.Contains("?? _libraryManager.RootFolder", downloader, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "build.yaml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
