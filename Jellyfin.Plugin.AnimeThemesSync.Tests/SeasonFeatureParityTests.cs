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
        var jellyfinRenderer = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Services", "SkiaCollectionImageRenderer.cs"));
        var embyRenderer = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Helpers", "SkiaCollectionImageRenderer.cs"));

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
                     "AtsSeasonCollectionPosterFillLandscapeType",
                     "AtsSeasonCollectionCanvasLandscapeType",
                     "AnimeThemesCollectionDisableDialog",
                     "AnimeThemesCollectionDisableDialogKeep",
                     "AnimeThemesCollectionDisableDialogRemove",
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
        Assert.Contains("normalizeLandscapeArtType", jellyfin, StringComparison.Ordinal);
        Assert.Contains("normalizeLandscapeArtType", embyScript, StringComparison.Ordinal);
        Assert.Contains("requestCollectionDisableChoice", jellyfin, StringComparison.Ordinal);
        Assert.Contains("requestCollectionDisableChoice", embyScript, StringComparison.Ordinal);
        Assert.Contains("atsCollectionDialogCancelled", jellyfin, StringComparison.Ordinal);
        Assert.Contains("atsCollectionDialogCancelled", embyScript, StringComparison.Ordinal);
        Assert.DoesNotContain("Season Collections were turned off. Remove only collection memberships", jellyfin, StringComparison.Ordinal);
        Assert.DoesNotContain("Season Collections were turned off. Remove only collection memberships", embyScript, StringComparison.Ordinal);
        Assert.Contains("SeasonCollectionLandscapeSourceMode: 2", jellyfin, StringComparison.Ordinal);
        Assert.Contains("SeasonCollectionLandscapeSourceMode: 2", embyScript, StringComparison.Ordinal);
        Assert.Contains("SeasonCollectionBackdropOverlayEnabled: false", jellyfin, StringComparison.Ordinal);
        Assert.Contains("SeasonCollectionBackdropOverlayEnabled: false", embyScript, StringComparison.Ordinal);

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
                     "SelectPosterCanvasSources",
                     "SeasonCollectionPosterFillLandscapeType",
                     "SeasonCollectionCanvasLandscapeType",
                     "CollectionImageLayoutSource",
                     "thumbSources",
                     "backdropSources",
                     "FindTrackedCollectionImage",
                     "FindWrittenCollectionImage",
                     "DeleteCollectionAssetState",
                     "DeleteFileLocation = true",
                 })
        {
            Assert.Contains(downloaderMarker, jellyfinDownloader, StringComparison.Ordinal);
            Assert.Contains(downloaderMarker, embyDownloader, StringComparison.Ordinal);
        }

        Assert.Contains("DeleteImageAsync(imageType, trackedImage.Index)", jellyfinDownloader, StringComparison.Ordinal);
        Assert.Contains("DeleteImage(imageType, image.Index)", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("GetLinkedChildren().Count == 0", jellyfinDownloader, StringComparison.Ordinal);
        Assert.Contains("GetCollectionMembers(collection).Count == 0", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("GetItemList(new InternalItemsQuery", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("GetItemIdList(new InternalItemsQuery", embyDownloader, StringComparison.Ordinal);
        Assert.DoesNotContain("GetChildrenIds(new InternalItemsQuery", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("emby://playlistcollage", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("auto_poster", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("collection.GetInternalMetadataPath()", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("slotImages.Any(image => !IsEmbyDynamicCollectionImage(collection, image))", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("Array.Empty<long>()", embyDownloader, StringComparison.Ordinal);
        Assert.DoesNotContain("long[] memberIds", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("EnsureManagedSeasonCollection", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("collection.ProviderIds[BroadcastSeasonProviderKey] = broadcastSeason.Key", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("stateByCollectionItemId", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("stateByItemId?.CollectionKey", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("Collection image plan for {0}", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("Replacing {0} Emby-generated {1} image(s)", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("GetCollectionMemberArtAsync", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("ConvertImageToLocal", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("Could not localize the {0} image", embyDownloader, StringComparison.Ordinal);

        foreach (var rendererMarker in new[]
                 {
                     "CollectionImageLayoutSource",
                     "bitmap.Width",
                     "bitmap.Height",
                     "layoutForSources(layoutSources)",
                 })
        {
            Assert.Contains(rendererMarker, jellyfinRenderer, StringComparison.Ordinal);
            Assert.Contains(rendererMarker, embyRenderer, StringComparison.Ordinal);
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
    public void EmbySynchronizer_UsesBoxSetItemQueriesAndDefendsRootSeriesParents()
    {
        var root = FindRepositoryRoot();
        var downloader = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"));

        Assert.Contains("GetItemList(new InternalItemsQuery", downloader, StringComparison.Ordinal);
        Assert.Contains("GetItemIdList(new InternalItemsQuery", downloader, StringComparison.Ordinal);
        Assert.DoesNotContain("GetChildrenIds(new InternalItemsQuery", downloader, StringComparison.Ordinal);
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
