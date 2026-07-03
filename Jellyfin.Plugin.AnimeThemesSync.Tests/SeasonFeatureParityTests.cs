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
                     "AtsSeasonCollectionLockEnabled",
                     "AtsSeasonCollectionImagesEnabled",
                     "AtsSeasonCollectionBackdropOverlayEnabled",
                     "AtsSeasonCollectionBackdropOverlayOpacity",
                     "AtsSeasonCollectionBackdropOverlayColor",
                     "AtsCollectionImageOptions",
                     "AtsBackdropOverlayOptions",
                     "AtsSeasonCollectionPosterFillMode",
                     "AtsSeasonCollectionLandscapeSourceMode",
                     "AtsSeasonCollectionCanvasColor",
                     "AtsSeasonCollectionCanvasOpacity",
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
        Assert.Contains("normalizeOverlayOpacity", jellyfin, StringComparison.Ordinal);
        Assert.Contains("normalizeOverlayOpacity", embyScript, StringComparison.Ordinal);
        Assert.Contains("normalizeOverlayColor", jellyfin, StringComparison.Ordinal);
        Assert.Contains("normalizeOverlayColor", embyScript, StringComparison.Ordinal);
        Assert.Contains("normalizePosterFillMode", jellyfin, StringComparison.Ordinal);
        Assert.Contains("normalizePosterFillMode", embyScript, StringComparison.Ordinal);
        Assert.Contains("normalizeLandscapeSourceMode", jellyfin, StringComparison.Ordinal);
        Assert.Contains("normalizeLandscapeSourceMode", embyScript, StringComparison.Ordinal);
        Assert.Contains("normalizeCanvasOpacity", jellyfin, StringComparison.Ordinal);
        Assert.Contains("normalizeCanvasOpacity", embyScript, StringComparison.Ordinal);

        foreach (var downloaderMarker in new[]
                 {
                     "FinalizeSeasonCollectionsAsync",
                     "GenerateSeasonCollectionImagesAsync",
                     "IsLocked = ",
                     "SeasonCollectionLockEnabled",
                     "SeasonCollectionImagesEnabled",
                     "UserOwnedImageFingerprint",
                     "ManagedSeasonCollectionAssetState",
                     "SeasonCollectionFinalizeGate",
                     "FinalizeSeasonCollectionsSafelyAsync",
                     "GetCollectionMemberArt",
                     "ComputeLandscapeCanvasLayout",
                     "SelectLandscapeCanvasSources",
                 })
        {
            Assert.Contains(downloaderMarker, jellyfinDownloader, StringComparison.Ordinal);
            Assert.Contains(downloaderMarker, embyDownloader, StringComparison.Ordinal);
        }

        var jellyfinStore = File.ReadAllText(Path.Combine(root, "AnimeThemesSync.Shared", "Services", "SeasonFinderDataStore.cs"));
        var embyStore = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "ScheduledTasks", "EmbySeasonFinderDataStore.cs"));
        foreach (var storeMarker in new[]
                 {
                     "ManagedSeasonCollectionAssets",
                     "GetCollectionAssetStates",
                     "UpsertCollectionAssetState",
                     "DeleteCollectionAssetState",
                 })
        {
            Assert.Contains(storeMarker, jellyfinStore, StringComparison.Ordinal);
            Assert.Contains(storeMarker, embyStore, StringComparison.Ordinal);
        }
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
