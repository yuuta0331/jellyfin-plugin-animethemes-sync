using System;
using System.IO;
using System.Linq;
using AnimeThemesSync.Shared;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using Xunit;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public class ThemeFilePlannerTests
{
    [Fact]
    public void BuildPlan_UsesCompactExtrasNameByDefault()
    {
        var anime = CreateAnime();

        var plan = ThemeFilePlanner.BuildPlan(
            anime,
            Path.Combine("Media", "Bakemonogatari"),
            Disabled(),
            Enabled(maxThemes: 2),
            extrasEnabled: true);

        var backdropNames = plan.MediaFiles.Where(f => f.IsVideo).Select(f => Path.GetFileName(f.Path)).ToList();
        var extrasNames = plan.ExtraFiles.Select(f => Path.GetFileName(f.TargetPath)).ToList();

        Assert.Equal(
            new[]
            {
                "01-OP1 - staple stable - Chiwa Saitou - Eps 1-2, 12 - NC BD1080.webm",
                "02-OP2 - Kaerimichi - Emiri Katou - Eps 3-5 - Spoiler WEB720.webm"
            },
            backdropNames);
        Assert.Contains("01. OP1 - staple stable-other.webm", extrasNames);
        Assert.Contains("02. OP2 - Kaerimichi-other.webm", extrasNames);
        Assert.Contains(
            Path.Combine("Media", "Bakemonogatari", "extras", "AnimeThemes - 01 - OP1 - staple stable - Chiwa Saitou - Eps 1-2, 12 - NC BD1080.webm"),
            plan.ExtraFiles[0].LegacyTargetPaths);
        Assert.False(string.IsNullOrWhiteSpace(plan.ExtraFiles[0].Key));
    }

    [Fact]
    public void BuildPlan_UsesCustomExtrasNameFormat()
    {
        var anime = CreateAnime();

        var plan = ThemeFilePlanner.BuildPlan(
            anime,
            Path.Combine("Media", "Bakemonogatari"),
            Disabled(),
            Enabled(maxThemes: 1),
            extrasEnabled: true,
            extrasFileNameFormat: "{Theme} - {Song} - {Artist} - {Episodes} - {Quality}");

        var extrasName = Path.GetFileName(plan.ExtraFiles.Single().TargetPath);

        Assert.Equal("OP1 - staple stable - Chiwa Saitou - Eps 1-2, 12 - BD1080-other.webm", extrasName);
    }

    [Fact]
    public void BuildPlan_AddsSongTitleToAudioThemeName()
    {
        var anime = CreateAnime();

        var plan = ThemeFilePlanner.BuildPlan(
            anime,
            Path.Combine("Media", "Bakemonogatari"),
            Enabled(maxThemes: 1),
            Disabled(),
            extrasEnabled: false);

        var audioName = Path.GetFileName(plan.MediaFiles.Single().Path);

        Assert.Equal("01-OP1 - staple stable - Chiwa Saitou.ogg", audioName);
    }

    [Fact]
    public void BuildPlan_AttachesSharedRowAndDisplayMetadataToThemeOutputs()
    {
        var plan = ThemeFilePlanner.BuildPlan(
            CreateAnime(),
            Path.Combine("Media", "Bakemonogatari"),
            Enabled(maxThemes: 1),
            Enabled(maxThemes: 1),
            extrasEnabled: true);

        var audio = plan.MediaFiles.Single(file => !file.IsVideo);
        var video = plan.MediaFiles.Single(file => file.IsVideo);
        var extra = plan.ExtraFiles.Single();
        Assert.False(string.IsNullOrWhiteSpace(audio.SourceRowId));
        Assert.Equal(audio.SourceRowId, video.SourceRowId);
        Assert.Equal(audio.SourceRowId, extra.Key);
        Assert.Equal("OP1 · staple stable", audio.DisplayTitle);
        Assert.Equal(audio.DisplayTitle, video.DisplayTitle);
        Assert.Equal(audio.DisplayTitle, extra.DisplayTitle);
    }

    [Fact]
    public void BuildPlan_MetadataGroupsOnlyOutputsSelectedByEachMediaSetting()
    {
        var plan = ThemeFilePlanner.BuildPlan(
            CreateAnime(),
            Path.Combine("Media", "Bakemonogatari"),
            Enabled(maxThemes: 1),
            Enabled(maxThemes: 2),
            extrasEnabled: true);
        var outputRows = plan.MediaFiles
            .Select(file => (RowId: file.SourceRowId, Kind: file.IsVideo ? "video" : "audio"))
            .Concat(plan.ExtraFiles.Select(extra => (RowId: extra.Key, Kind: "extra")))
            .GroupBy(output => output.RowId)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Select(output => output.Kind).OrderBy(kind => kind, StringComparer.Ordinal).ToArray())
            .ToList();

        Assert.Equal(2, outputRows.Count);
        Assert.Contains(outputRows, kinds => kinds.SequenceEqual(new[] { "audio", "extra", "video" }));
        Assert.Contains(outputRows, kinds => kinds.SequenceEqual(new[] { "extra", "video" }));
    }

    [Fact]
    public void BuildPlan_FallsBackWhenSequenceIsMissingAndSanitizesNames()
    {
        var anime = new AnimeThemesAnime
        {
            AnimeThemes =
            [
                new AnimeThemesTheme
                {
                    Type = "ED",
                    Slug = "ED:Final",
                    Song = new AnimeThemesSong { Title = "bad/title?" },
                    Entries =
                    [
                        new AnimeThemesEntry
                        {
                            Version = 2,
                            Videos =
                            [
                                new AnimeThemesVideo
                                {
                                    Link = "https://v.animethemes.moe/test.webm",
                                    Audio = new AnimeThemesAudio { Link = "https://a.animethemes.moe/test.ogg" }
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var plan = ThemeFilePlanner.BuildPlan(anime, Path.Combine("Media", "Test"), Enabled(maxThemes: 1), Enabled(maxThemes: 1), extrasEnabled: true);

        Assert.Equal("01-ED Finalv2 - bad title.ogg", Path.GetFileName(plan.MediaFiles.Single(f => !f.IsVideo).Path));
        Assert.Equal("01-ED Finalv2 - bad title.webm", Path.GetFileName(plan.MediaFiles.Single(f => f.IsVideo).Path));
        Assert.Equal("01. ED Finalv2 - bad title-other.webm", Path.GetFileName(plan.ExtraFiles.Single().TargetPath));
    }

    [Fact]
    public void BuildPlan_TruncatesLongExtrasNames()
    {
        var anime = CreateAnime();
        anime.AnimeThemes![0].Song!.Title = new string('A', 220);

        var plan = ThemeFilePlanner.BuildPlan(anime, Path.Combine("Media", "Test"), Disabled(), Enabled(maxThemes: 1), extrasEnabled: true);

        var extrasName = Path.GetFileName(plan.ExtraFiles.Single().TargetPath);

        Assert.True(extrasName.Length <= 180);
        Assert.StartsWith("01. OP1 - ", extrasName, StringComparison.Ordinal);
        Assert.EndsWith(".webm", extrasName, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPlan_DistinguishesMultipleEntriesForSameTheme()
    {
        var anime = CreateAnime();
        anime.AnimeThemes![0].Entries!.Add(new AnimeThemesEntry
        {
            Version = 1,
            Episodes = "13",
            Spoiler = true,
            Nsfw = true,
            Videos =
            [
                new AnimeThemesVideo
                {
                    Link = "https://v.animethemes.moe/op1-spoiler.webm",
                    Source = "BD",
                    Resolution = 1080,
                    Audio = new AnimeThemesAudio { Link = "https://a.animethemes.moe/op1-spoiler.ogg" }
                }
            ]
        });

        var plan = ThemeFilePlanner.BuildPlan(anime, Path.Combine("Media", "Test"), Disabled(), Enabled(maxThemes: 2), extrasEnabled: true);
        var videoNames = plan.MediaFiles.Where(f => f.IsVideo).Select(f => Path.GetFileName(f.Path)).ToList();

        Assert.Equal(2, videoNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("01-OP1 - staple stable - Chiwa Saitou - Eps 1-2, 12 - NC BD1080.webm", videoNames);
        Assert.Contains("02-OP1 - staple stable - Chiwa Saitou - Eps 13 - Spoiler NSFW BD1080.webm", videoNames);
    }

    [Fact]
    public void BuildBrowserRowId_UsesIdsAndFallsBackToStableHash()
    {
        var anime = CreateAnime();
        var theme = anime.AnimeThemes![0];
        var entry = theme.Entries![0];
        var video = entry.Videos![0];
        theme.Id = 10;
        entry.Id = 20;
        video.Id = 30;
        video.Audio!.Id = 40;

        var candidate = ThemeFilePlanner.GetBrowserCandidates(anime.AnimeThemes).First();
        var rowId = ThemeFilePlanner.BuildBrowserRowId(candidate);

        Assert.Equal("t10-e20-v30-a40", rowId);

        candidate.Theme.Id = 0;
        candidate.Entry.Id = 0;
        candidate.Video.Id = 0;

        var fallback = ThemeFilePlanner.BuildBrowserRowId(candidate);
        Assert.StartsWith("h", fallback, StringComparison.Ordinal);
        Assert.Equal(fallback, ThemeFilePlanner.BuildBrowserRowId(candidate));
    }

    [Fact]
    public void BuildSingleCandidatePlan_CreatesOnlySelectedThemeOutputsWithBrowserOrder()
    {
        var anime = CreateAnime();
        var candidates = ThemeFilePlanner.GetBrowserCandidates(anime.AnimeThemes!);
        var selected = candidates[1];

        var plan = ThemeFilePlanner.BuildSingleCandidatePlan(
            anime,
            selected,
            order: 2,
            Path.Combine("Media", "Bakemonogatari"),
            includeAudio: true,
            includeVideo: true,
            includeExtras: true);

        Assert.Equal(2, plan.MediaFiles.Count);
        Assert.Single(plan.ExtraFiles);
        Assert.Equal("02-OP2 - Kaerimichi - Emiri Katou - Eps 3-5 - Spoiler WEB720.webm", Path.GetFileName(plan.MediaFiles.Single(f => f.IsVideo).Path));
        Assert.Equal("02-OP2 - Kaerimichi - Emiri Katou.ogg", Path.GetFileName(plan.MediaFiles.Single(f => !f.IsVideo).Path));
        Assert.Equal("02. OP2 - Kaerimichi-other.webm", Path.GetFileName(plan.ExtraFiles.Single().TargetPath));
    }

    [Fact]
    public void BuildPlans_ApplySeasonPrefixAndOutputTargetToEveryArtifact()
    {
        var anime = CreateAnime();
        var seriesPath = Path.Combine("Media", "Bakemonogatari");
        var target = new ThemeOutputTarget(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            seriesPath,
            ThemeOutputScope.SeriesRoot,
            true);

        var plan = ThemeFilePlanner.BuildPlan(
            anime,
            target.OutputRootPath,
            Enabled(1),
            Enabled(1),
            extrasEnabled: true,
            fileNamePrefix: "Season 01 -",
            outputTarget: target);
        var candidate = ThemeFilePlanner.GetBrowserCandidates(anime.AnimeThemes!).First();
        var single = ThemeFilePlanner.BuildSingleCandidatePlan(
            anime,
            candidate,
            1,
            target.OutputRootPath,
            includeAudio: true,
            includeVideo: true,
            includeExtras: true,
            fileNamePrefix: "Season 01 -",
            outputTarget: target);

        Assert.All(plan.MediaFiles, file =>
        {
            Assert.StartsWith("Season 01 - ", Path.GetFileName(file.Path), StringComparison.Ordinal);
            Assert.Equal(target, file.OutputTarget);
            Assert.True(ThemeFilePlanner.IsPluginOwnedFile(file.Path, anime.AnimeThemes!));
        });
        Assert.All(plan.ExtraFiles, file =>
        {
            Assert.StartsWith("Season 01 - ", Path.GetFileName(file.TargetPath), StringComparison.Ordinal);
            Assert.Equal(target, file.OutputTarget);
            Assert.True(ThemeFilePlanner.IsPluginOwnedFile(file.TargetPath, anime.AnimeThemes!));
        });
        Assert.All(single.MediaFiles, file => Assert.StartsWith("Season 01 - ", Path.GetFileName(file.Path), StringComparison.Ordinal));
        Assert.StartsWith("Season 01 - ", Path.GetFileName(Assert.Single(single.ExtraFiles).TargetPath), StringComparison.Ordinal);
    }

    [Fact]
    public void MergePlans_UnionsCleanupForSharedSeriesRoot()
    {
        var anime = CreateAnime();
        var root = Path.Combine("Media", "Bakemonogatari");
        var seriesPlan = ThemeFilePlanner.BuildPlan(anime, root, Enabled(1), Enabled(1), extrasEnabled: true);
        var seasonPlan = ThemeFilePlanner.BuildPlan(
            anime,
            root,
            Enabled(1),
            Enabled(1),
            extrasEnabled: true,
            fileNamePrefix: "Season 01 -");

        var merged = ThemeFilePlanner.MergePlans([seriesPlan, seasonPlan]);

        Assert.Equal(3, merged.CleanupPlans.Count);
        Assert.All(merged.CleanupPlans, cleanup => Assert.Equal(2, cleanup.DesiredFiles.Count));
    }

    [Fact]
    public void BuildPlan_ExtraOnly_DownloadsDirectlyAndPreservesSourceExtension()
    {
        var anime = CreateAnime();
        var video = anime.AnimeThemes![0].Entries![0].Videos![0];
        video.Filename = "OP1.MP4";
        video.Link = "https://v.animethemes.moe/download?id=1";
        var audio = Disabled();
        var videoConfig = Enabled(maxThemes: 1);
        videoConfig.UseAsTheme = false;

        var plan = ThemeFilePlanner.BuildPlan(
            anime,
            Path.Combine("Media", "Test"),
            audio,
            videoConfig,
            extrasEnabled: true);

        Assert.Empty(plan.MediaFiles);
        var extra = Assert.Single(plan.ExtraFiles);
        Assert.Null(extra.SourcePath);
        Assert.Equal(video.Link, extra.DownloadUrl);
        Assert.Equal("01. OP1 - staple stable-other.mp4", Path.GetFileName(extra.TargetPath));
    }

    [Fact]
    public void BuildPlan_PreservesSeparateAudioExtensionAndMarksVideoFallbackForExtraction()
    {
        var anime = CreateAnime();
        var video = anime.AnimeThemes![0].Entries![0].Videos![0];
        video.Audio!.Filename = "OP1.FLAC";
        video.Audio.Link = "https://a.animethemes.moe/audio?id=1";

        var withAudio = ThemeFilePlanner.BuildPlan(anime, "Media", Enabled(1), Disabled(), extrasEnabled: false);
        var audioPlan = Assert.Single(withAudio.MediaFiles);
        Assert.EndsWith(".flac", audioPlan.Path, StringComparison.Ordinal);
        Assert.False(audioPlan.RequiresTranscoding);

        video.Audio = null;
        var withoutAudio = ThemeFilePlanner.BuildPlan(anime, "Media", Enabled(1), Disabled(), extrasEnabled: false);
        audioPlan = Assert.Single(withoutAudio.MediaFiles);
        Assert.EndsWith(".mp3", audioPlan.Path, StringComparison.Ordinal);
        Assert.True(audioPlan.RequiresTranscoding);
    }

    [Theory]
    [InlineData(ExtrasFileSuffix.None, "01. OP1 - staple stable.webm")]
    [InlineData(ExtrasFileSuffix.Other, "01. OP1 - staple stable-other.webm")]
    [InlineData(ExtrasFileSuffix.Short, "01. OP1 - staple stable-short.webm")]
    [InlineData(ExtrasFileSuffix.Scene, "01. OP1 - staple stable-scene.webm")]
    public void BuildPlan_AppliesConfiguredExtrasSuffix(ExtrasFileSuffix suffix, string expected)
    {
        var plan = ThemeFilePlanner.BuildPlan(
            CreateAnime(),
            "Media",
            Disabled(),
            Enabled(1),
            extrasEnabled: true,
            extrasFileNameFormat: null,
            extrasFileSuffix: suffix);

        Assert.Equal(expected, Path.GetFileName(Assert.Single(plan.ExtraFiles).TargetPath));
    }

    [Theory]
    [InlineData("clip.MKV", null, "https://example.test/video.webm", true, ".mkv")]
    [InlineData(null, "song.OpUs", "https://example.test/audio.ogg", false, ".opus")]
    [InlineData(null, null, "https://example.test/video.mp4?token=abc", true, ".mp4")]
    [InlineData(null, null, "https://example.test/download?token=abc", true, ".webm")]
    public void ResolveMediaExtension_UsesMetadataThenUrl(
        string? filename,
        string? basename,
        string url,
        bool isVideo,
        string expected)
    {
        var extension = ThemeFilePlanner.ResolveMediaExtension(
            filename,
            basename,
            url,
            isVideo ? ".webm" : ".mp3",
            isVideo);

        Assert.Equal(expected, extension);
    }

    [Fact]
    public void ExtrasManifest_RenamesLegacyAndPreviouslyTrackedExtras()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ats-extras-" + Guid.NewGuid().ToString("N"));
        var dbDirectory = Path.Combine(Path.GetTempPath(), "ats-db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var store = new AnimeThemesDataStore(new TestDataPathProvider(dbDirectory), new TestServerIdentityProvider());
        ThemeExtrasManifestService.ConfigureStore(store);

        try
        {
            var legacyPath = Path.Combine(directory, "AnimeThemes - 01 - OP1 - Song.webm");
            var firstTargetPath = Path.Combine(directory, "01. OP1 - Song.webm");
            var secondTargetPath = Path.Combine(directory, "OP1 - Song.webm");
            File.WriteAllText(legacyPath, "video");

            var firstPlan = new ThemeExtraPlan(Path.Combine(directory, "source.webm"), firstTargetPath)
            {
                Key = "theme-key",
                LegacyTargetPaths = new[] { legacyPath }
            };

            var firstResult = ThemeExtrasManifestService.MigrateExtraFile(firstPlan, overwrite: false);

            Assert.Equal("renamed", firstResult.Action);
            Assert.False(File.Exists(legacyPath));
            Assert.True(File.Exists(firstTargetPath));

            var secondPlan = new ThemeExtraPlan(Path.Combine(directory, "source.webm"), secondTargetPath)
            {
                Key = "theme-key"
            };

            var secondResult = ThemeExtrasManifestService.MigrateExtraFile(secondPlan, overwrite: false);

            Assert.Equal("renamed", secondResult.Action);
            Assert.False(File.Exists(firstTargetPath));
            Assert.True(File.Exists(secondTargetPath));
            Assert.False(File.Exists(Path.Combine(directory, ThemeExtrasManifestService.ManifestFileName)));
            Assert.True(File.Exists(store.DatabasePath));
        }
        finally
        {
            ThemeExtrasManifestService.ResetStoreForTests();
            Directory.Delete(directory, recursive: true);
            if (Directory.Exists(dbDirectory))
            {
                Directory.Delete(dbDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ConfigurationPages_DoNotContainRemovedPresetControlsOrFlatActionButtons()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "configPage.html"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "configPage.html"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "configPage.js"),
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js")
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("ConfigurationPreset", content, StringComparison.Ordinal);
            Assert.DoesNotContain("ApplyPresetButton", content, StringComparison.Ordinal);
            Assert.DoesNotContain("applyConfigurationPreset", content, StringComparison.Ordinal);
            Assert.DoesNotContain("button-flat", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PluginVersion_IsSynchronizedAcrossBuildAndRegisteredPages()
    {
        var root = FindRepositoryRoot();
        var assetVersion = Constants.UiAssetVersion;
        var pluginVersion = Constants.PluginVersion;
        var build = File.ReadAllText(Path.Combine(root, "build.yaml"));
        var props = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        var releaseWorkflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "github-release.yaml"));
        var jellyfinConfig = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "configPage.html"));
        var jellyfinBrowser = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        var embyConfig = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "configPage.html"));
        var embyBrowser = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));

        Assert.Contains("configurationpage?name=animethemessyncbrowser" + assetVersion, jellyfinConfig, StringComparison.Ordinal);
        Assert.Contains("#/configurationpage?name=animethemessyncbrowser" + assetVersion, embyConfig, StringComparison.Ordinal);
        Assert.Contains("__plugin/animethemessyncconfigjs" + assetVersion, embyConfig, StringComparison.Ordinal);
        Assert.Contains("__plugin/animethemessyncbrowserjs" + assetVersion, embyBrowser, StringComparison.Ordinal);
        Assert.Contains("version: \"" + pluginVersion + "\"", build, StringComparison.Ordinal);
        Assert.Contains(">" + pluginVersion + "</Version>", props, StringComparison.Ordinal);
        Assert.Contains("./update-version.ps1 -Version $env:VERSION", releaseWorkflow, StringComparison.Ordinal);
        var isReleaseDisplay = jellyfinBrowser.Contains("Version: " + pluginVersion, StringComparison.Ordinal);
        Assert.Equal(isReleaseDisplay, embyBrowser.Contains("Version: " + pluginVersion, StringComparison.Ordinal));
        if (isReleaseDisplay)
        {
            Assert.Equal("v" + pluginVersion.Replace(".", string.Empty, StringComparison.Ordinal), assetVersion);
            Assert.DoesNotContain("UI version:", jellyfinBrowser, StringComparison.Ordinal);
            Assert.DoesNotContain("UI version:", embyBrowser, StringComparison.Ordinal);
        }
        else
        {
            Assert.Matches(@"^\d{8}[a-z]$", assetVersion);
            Assert.Contains("UI version:", jellyfinBrowser, StringComparison.Ordinal);
            Assert.Contains("UI version:", embyBrowser, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HostSpecificSqliteProviders_AreSeparated()
    {
        var root = FindRepositoryRoot();
        var jellyfinProject = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Jellyfin.Plugin.AnimeThemesSync.csproj"));
        var embyProject = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Emby.Plugin.AnimeThemesSync.csproj"));
        var embyStore = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "ScheduledTasks", "EmbySeasonFinderDataStore.cs"));

        Assert.Contains("Microsoft.Data.Sqlite\" Version=\"9.0.11", jellyfinProject, StringComparison.Ordinal);
        Assert.Contains("SQLitePCL.pretty.core\" Version=\"1.2.2\" IncludeAssets=\"compile", embyProject, StringComparison.Ordinal);
        Assert.Contains("Services\\SeasonFinderDataStore.cs", embyProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", embyProject, StringComparison.Ordinal);
        Assert.Contains("SQLite3.Open", embyStore, StringComparison.Ordinal);
        Assert.DoesNotContain("using Microsoft.Data.Sqlite", embyStore, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS SeasonFinderRows", embyStore, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS SeasonThemeMappings", embyStore, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS AnimeSearchCache", embyStore, StringComparison.Ordinal);
        Assert.Contains("LIMIT $limit OFFSET $start", embyStore, StringComparison.Ordinal);
        Assert.Contains("MigrateDocumentStore(connection)", embyStore, StringComparison.Ordinal);
        Assert.Contains("EmbyDocumentStoreMigrated:", embyStore, StringComparison.Ordinal);
        Assert.DoesNotContain("private StoreDocument? _document", embyStore, StringComparison.Ordinal);
        Assert.DoesNotContain("IEnumerable<SeasonFinderRowRecord> filtered", embyStore, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserPages_UseAuthenticatedLocalMediaUrls()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js")
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Assert.Contains("getAccessToken", content, StringComparison.Ordinal);
            Assert.Contains("appendQuery(url, 'api_key', token)", content, StringComparison.Ordinal);
            Assert.Contains("LocalMedia?target=", content, StringComparison.Ordinal);
            Assert.Contains("Play Video", content, StringComparison.Ordinal);
            Assert.Contains("Play Audio", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Play Extra", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BrowserPages_StartDownloadJobsAndPollProgress()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js")
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Assert.Contains("AnimeThemesSync/Jobs/ThemeDownload", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesSync/Jobs/ItemDownload", content, StringComparison.Ordinal);
            Assert.Contains("startDownloadsPolling", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesBrowserProgressBar", content, StringComparison.Ordinal);
            Assert.Contains("AtsDownloadIncludeAudio", content, StringComparison.Ordinal);
            Assert.Contains("AtsDownloadIncludeVideo", content, StringComparison.Ordinal);
            Assert.Contains("AtsDownloadIncludeExtras", content, StringComparison.Ordinal);
            Assert.Contains("&IncludeAudio=", content, StringComparison.Ordinal);
            Assert.Contains("&IncludeVideo=", content, StringComparison.Ordinal);
            Assert.Contains("&IncludeExtras=", content, StringComparison.Ordinal);
            Assert.Contains("&DisplayTitle=", content, StringComparison.Ordinal);
            Assert.Contains("data-row-id", content, StringComparison.Ordinal);
            Assert.Contains("ats-card-download-status", content, StringComparison.Ordinal);
            Assert.DoesNotContain("/Download?force=", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BrowserPages_ExposeVisibleProgressHistoryDismissalRetryAndCorrectDeleteApi()
    {
        var root = FindRepositoryRoot();
        var jellyfinPage = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        var embyPage = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        var embyController = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js"));

        foreach (var page in new[] { jellyfinPage, embyPage })
        {
            Assert.Contains("#AnimeThemesBrowserPage .ats-card-download-bar", page, StringComparison.Ordinal);
            Assert.Contains("display: block;", page, StringComparison.Ordinal);
            Assert.Contains("ats-skeleton-shimmer-only::after", page, StringComparison.Ordinal);
            Assert.Contains("animation: atsShimmer 1.25s", page, StringComparison.Ordinal);
            Assert.Contains("ats-dm-item-dismiss", page, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesDmClearHistory", page, StringComparison.Ordinal);
            Assert.Contains("ats-dm-item-retry", page, StringComparison.Ordinal);
            Assert.Contains("ats-card-download-retry", page, StringComparison.Ordinal);
            Assert.Contains("No active downloads", page, StringComparison.Ordinal);
        }

        foreach (var script in new[] { jellyfinPage, embyController })
        {
            Assert.Contains("function dismissDownloadJob(jobId)", script, StringComparison.Ordinal);
            Assert.Contains("function retryDownloadJob(jobId)", script, StringComparison.Ordinal);
            Assert.Contains("function clearFinishedDownloadHistory()", script, StringComparison.Ordinal);
            Assert.Contains("dmContent.innerHTML = '<div class=\"ats-dm-empty\">No active downloads</div>'", script, StringComparison.Ordinal);
            Assert.Contains("if (emptyState) emptyState.remove();", script, StringComparison.Ordinal);
            Assert.Contains("isTerminalDownloadStatus(job.status)", script, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesSync/Jobs/", script, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesSync/Jobs/History", script, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesSync/Jobs/ItemDownloadBatch", script, StringComparison.Ordinal);
            Assert.Contains("observeBrowserSentinel", script, StringComparison.Ordinal);
            Assert.Contains("preserveCount: true", script, StringComparison.Ordinal);
            Assert.Contains("function loadPlayerAttempt", script, StringComparison.Ordinal);
            Assert.Contains("createButton('Retry', true)", script, StringComparison.Ordinal);
            Assert.Contains("ThemeFiles/DeleteFile?ItemId=", script, StringComparison.Ordinal);
            Assert.Contains("markThemeTargetDeleted", script, StringComparison.Ordinal);
            Assert.DoesNotContain("/Delete/Video", script, StringComparison.Ordinal);
            Assert.DoesNotContain("/Delete/Audio", script, StringComparison.Ordinal);
            Assert.DoesNotContain("/Delete/Extra", script, StringComparison.Ordinal);
            Assert.DoesNotContain("addOpenButton(actions, 'AnimeThemes', value(row, 'AnimeThemesUrl'", script, StringComparison.Ordinal);
        }

        var embyService = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Api", "AnimeThemesSyncService.cs"));
        Assert.Contains("SupportsRangeRequests = true", embyService, StringComparison.Ordinal);
        Assert.Contains("GetStaticFileResult", embyService, StringComparison.Ordinal);
        Assert.Contains("RetryAnimeThemesDownloadJob", embyService, StringComparison.Ordinal);
        Assert.Contains("RemoveAnimeThemesFinishedDownloadHistory", embyService, StringComparison.Ordinal);

        var jellyfinController = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Api", "AnimeThemesSyncController.cs"));
        Assert.Contains("Jobs/ItemDownloadBatch", jellyfinController, StringComparison.Ordinal);
        Assert.Contains("Jobs/{jobId}/Retry", jellyfinController, StringComparison.Ordinal);
        Assert.Contains("Jobs/History", jellyfinController, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserPages_ExposeSegmentedSettingsAndUniqueDownloadManagerIds()
    {
        var root = FindRepositoryRoot();
        var jellyfinHtml = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        var embyHtml = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        var embyController = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js"));

        foreach (var content in new[] { jellyfinHtml, embyHtml })
        {
            Assert.Contains("AtsSegmentedDownloadEnabled", content, StringComparison.Ordinal);
            Assert.Contains("AtsSegmentedDownloadSegments", content, StringComparison.Ordinal);
            Assert.Equal(1, content.Split("id=\"AnimeThemesDownloadManager\"", StringSplitOptions.None).Length - 1);
            Assert.DoesNotContain("server theme Efor", content, StringComparison.Ordinal);
        }

        Assert.Contains("SegmentedDownloadEnabled", jellyfinHtml, StringComparison.Ordinal);
        Assert.Contains("SegmentedDownloadSegments", jellyfinHtml, StringComparison.Ordinal);
        Assert.Contains("SegmentedDownloadEnabled", embyController, StringComparison.Ordinal);
        Assert.Contains("SegmentedDownloadSegments", embyController, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserPages_UseDedicatedAccessibleDownloadDialog()
    {
        var root = FindRepositoryRoot();
        var jellyfinHtml = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        var embyHtml = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        var embyController = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js"));

        foreach (var content in new[] { jellyfinHtml, embyHtml })
        {
            var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.Contains("id=\"AnimeThemesDownloadDialog\"", content, StringComparison.Ordinal);
            Assert.Contains("role=\"dialog\"", content, StringComparison.Ordinal);
            Assert.Contains("aria-modal=\"true\"", content, StringComparison.Ordinal);
            Assert.Contains("ats-download-option", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesDownloadDialogCancel", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesDownloadDialogConfirm", content, StringComparison.Ordinal);
            Assert.Contains("Existing files for unselected outputs will not be deleted", content, StringComparison.Ordinal);
            Assert.Contains("#AnimeThemesBrowserPage {", content, StringComparison.Ordinal);
            Assert.Contains("class=\"emby-checkbox-label ats-download-checkbox\"", content, StringComparison.Ordinal);
            Assert.Contains("aria-label=\"Include theme video\" />\n                                <span>Include</span>", normalized, StringComparison.Ordinal);
            Assert.Contains("--jf-palette-background-paper", content, StringComparison.Ordinal);
            Assert.Contains("--background-hue", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Canvas", content, StringComparison.Ordinal);
        }

        Assert.Contains("material-icons ats-download-option-icon", jellyfinHtml, StringComparison.Ordinal);
        Assert.Contains("md-icon ats-download-option-icon", embyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("material-icons", embyHtml, StringComparison.Ordinal);
        Assert.Contains("openDownloadDialog", jellyfinHtml, StringComparison.Ordinal);
        Assert.Contains("confirmDownloadSelection", jellyfinHtml, StringComparison.Ordinal);
        Assert.Contains("openDownloadDialog", embyController, StringComparison.Ordinal);
        Assert.Contains("confirmDownloadSelection", embyController, StringComparison.Ordinal);
        Assert.Contains("control.click()", jellyfinHtml, StringComparison.Ordinal);
        Assert.Contains("control.click()", embyController, StringComparison.Ordinal);
        Assert.DoesNotContain("AtsDownloadSelectionConfirm", jellyfinHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("AtsDownloadSelectionConfirm", embyController, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserPagesExposeIntegratedSettingsAndSeasonFinder()
    {
        var root = FindRepositoryRoot();
        var browserPages = new[]
        {
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html")
        };

        foreach (var file in browserPages)
        {
            var content = File.ReadAllText(file);
            Assert.Contains("data-ats-tab=\"settings\"", content, StringComparison.Ordinal);
            Assert.Contains("AtsSeasonThemeDownloadsEnabled", content, StringComparison.Ordinal);
            Assert.Contains("https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync", content, StringComparison.Ordinal);
            Assert.Contains("Help / GitHub", content, StringComparison.Ordinal);
            Assert.Contains("AtsExtrasOptions", content, StringComparison.Ordinal);
            Assert.Contains("AtsExtrasFileSuffix", content, StringComparison.Ordinal);
            Assert.Contains("AtsTagOptions", content, StringComparison.Ordinal);
            Assert.Contains("Interface Customization", content, StringComparison.Ordinal);
            Assert.Contains("AtsExtrasFormatPreview", content, StringComparison.Ordinal);
            Assert.Contains("AtsTagFormatPreview", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesBrowserLibraryTypeFilter", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesBrowserLibrarySavedFilter", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesBrowserLibrarySort", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesBrowserShowDetails", content, StringComparison.Ordinal);
            Assert.Contains("value=\"itemAdded\"", content, StringComparison.Ordinal);
            Assert.Contains("value=\"latestEpisodeAdded\"", content, StringComparison.Ordinal);
            Assert.Contains("AtsResetDefaults", content, StringComparison.Ordinal);
            Assert.Contains("id=\"AtsSettingsSave\" disabled", content, StringComparison.Ordinal);
            Assert.Contains("top: .75rem;", content, StringComparison.Ordinal);
            Assert.Contains("width: fit-content;", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Manual Linking", content, StringComparison.Ordinal);
            Assert.DoesNotContain("AnimeThemesBrowserForce", content, StringComparison.Ordinal);
            Assert.Contains("Create browseable extras", content, StringComparison.Ordinal);
            Assert.Contains("Add season/year tags", content, StringComparison.Ordinal);
            Assert.Contains("AtsSeriesAudioVolumeSlider", content, StringComparison.Ordinal);
            Assert.Contains("AtsSeriesVideoMute", content, StringComparison.Ordinal);
            Assert.Contains("AtsMovieAudioVolumeSlider", content, StringComparison.Ordinal);
            Assert.Contains("AtsMovieVideoMute", content, StringComparison.Ordinal);
            Assert.Contains("Season Finder", content, StringComparison.Ordinal);
            Assert.Contains("Settings", content, StringComparison.Ordinal);
            Assert.DoesNotContain("AtsOnDemandItemId", content, StringComparison.Ordinal);
            Assert.DoesNotContain("AtsRunOnDemand", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Run Item Download", content, StringComparison.Ordinal);

            var actionsCssStart = content.IndexOf("#AnimeThemesBrowserPage .ats-settings-actions-bar {", StringComparison.Ordinal);
            var actionsCssEnd = content.IndexOf("#AnimeThemesBrowserPage .ats-settings-actions-bar .fieldDescription", StringComparison.Ordinal);
            Assert.True(actionsCssStart >= 0 && actionsCssEnd > actionsCssStart);
            var actionsCss = content[actionsCssStart..actionsCssEnd];
            Assert.DoesNotContain("bottom:", actionsCss, StringComparison.Ordinal);
        }

        var jellyfinPage = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        Assert.Contains("SeasonMappings", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("loadSettings", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("ConfigurationVersion", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("Ats' + profileName + mediaName + 'VolumeSlider", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("Ats' + profileName + mediaName + 'Mute", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("Ats' + profileName + mediaName + 'UseAsTheme", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("syncConditionalSettings", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("syncSettingsDirty", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("resetSettingsDefaults", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("function readSettingsForm()", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("return serializeSettings(readSettingsForm());", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("if (!settingsDirty())", jellyfinPage, StringComparison.Ordinal);
        Assert.DoesNotContain("collectSettingsFromForm(cloneSettings", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("itemLinkStatus", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("LatestEpisodeDateCreated", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("copyCustomCss", jellyfinPage, StringComparison.Ordinal);
        Assert.DoesNotContain("runOnDemandDownload", jellyfinPage, StringComparison.Ordinal);

        var embyController = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js"));
        Assert.Contains("SeasonMappings", embyController, StringComparison.Ordinal);
        Assert.Contains("loadSettings", embyController, StringComparison.Ordinal);
        Assert.Contains("ConfigurationVersion", embyController, StringComparison.Ordinal);
        Assert.Contains("Ats' + profileName + mediaName + 'VolumeSlider", embyController, StringComparison.Ordinal);
        Assert.Contains("Ats' + profileName + mediaName + 'Mute", embyController, StringComparison.Ordinal);
        Assert.Contains("Ats' + profileName + mediaName + 'UseAsTheme", embyController, StringComparison.Ordinal);
        Assert.Contains("syncConditionalSettings", embyController, StringComparison.Ordinal);
        Assert.Contains("syncSettingsDirty", embyController, StringComparison.Ordinal);
        Assert.Contains("resetSettingsDefaults", embyController, StringComparison.Ordinal);
        Assert.Contains("function readSettingsForm()", embyController, StringComparison.Ordinal);
        Assert.Contains("return serializeSettings(readSettingsForm());", embyController, StringComparison.Ordinal);
        Assert.Contains("if (!settingsDirty())", embyController, StringComparison.Ordinal);
        Assert.DoesNotContain("collectSettingsFromForm(cloneSettings", embyController, StringComparison.Ordinal);
        Assert.Contains("itemLinkStatus", embyController, StringComparison.Ordinal);
        Assert.Contains("LatestEpisodeDateCreated", embyController, StringComparison.Ordinal);
        Assert.Contains("copyCustomCss", embyController, StringComparison.Ordinal);
        Assert.DoesNotContain("runOnDemandDownload", embyController, StringComparison.Ordinal);

        var embyPage = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        Assert.Contains("md-icon ats-icon", embyPage, StringComparison.Ordinal);
        Assert.Contains("&#xE8B6;", embyPage, StringComparison.Ordinal);
        Assert.Contains("&#xE5C4;", embyPage, StringComparison.Ordinal);
        Assert.DoesNotContain("material-icons", embyPage, StringComparison.Ordinal);
        Assert.DoesNotContain(">save<", embyPage, StringComparison.Ordinal);
        Assert.Contains("md-icon ats-icon", embyController, StringComparison.Ordinal);
        Assert.DoesNotContain("material-icons", embyController, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseMetadataPublishesPluginImageFromGitHubPages()
    {
        var root = FindRepositoryRoot();
        var buildYaml = File.ReadAllText(Path.Combine(root, "build.yaml"));
        var updateRepoWorkflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "update-repo.yaml"));

        Assert.Contains("imageUrl: https://cassiscloud.github.io/jellyfin-plugin-animethemes-sync/images/jellyfin-plugin-animethemes-sync.jpeg", buildYaml, StringComparison.Ordinal);
        Assert.Contains("cp -R resource/images public/images", updateRepoWorkflow, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserPages_ShowLibraryGridViewModesSummaryAndDeleteActions()
    {
        var root = FindRepositoryRoot();
        var htmlFiles = new[]
        {
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html")
        };

        foreach (var file in htmlFiles)
        {
            var content = File.ReadAllText(file);
            Assert.Contains("AnimeThemesBrowserItemGrid", content, StringComparison.Ordinal);
            Assert.Contains("data-view-mode=\"poster\"", content, StringComparison.Ordinal);
            Assert.Contains("data-view-mode=\"list\"", content, StringComparison.Ordinal);
            Assert.Contains("data-view-mode=\"thumb\"", content, StringComparison.Ordinal);
            Assert.Contains("data-delete-scope=\"audio\"", content, StringComparison.Ordinal);
            Assert.Contains("data-delete-scope=\"video\"", content, StringComparison.Ordinal);
            Assert.Contains("data-delete-scope=\"all\"", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesSeasonFinderView", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesFinderSearchInput", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesBrowserSeasonGroups", content, StringComparison.Ordinal);
            Assert.Contains("ats-clear-input", content, StringComparison.Ordinal);
            Assert.Contains("data-season-filter=\"unmatched\"", content, StringComparison.Ordinal);
            Assert.Contains("data-ats-tab=\"finder\"", content, StringComparison.Ordinal);
            Assert.Contains("ats-finder-topbar", content, StringComparison.Ordinal);
            Assert.Contains("ats-hero-immersive", content, StringComparison.Ordinal);
            Assert.Contains("ats-hero-meta-chip", content, StringComparison.Ordinal);
            Assert.Contains("ats-skeleton", content, StringComparison.Ordinal);
            Assert.Contains("prefers-reduced-motion", content, StringComparison.Ordinal);
            Assert.Contains("Overwrite existing files", content, StringComparison.Ordinal);
            Assert.Contains("Settings Import / Export", content, StringComparison.Ordinal);
            Assert.Contains("Mappings Import / Export", content, StringComparison.Ordinal);
            Assert.Contains("Library Snapshot", content, StringComparison.Ordinal);
            Assert.Contains("Data Model", content, StringComparison.Ordinal);
            Assert.Contains("Mapping Explorer", content, StringComparison.Ordinal);
            Assert.Contains("AtsImportJson", content, StringComparison.Ordinal);
            Assert.Contains("AtsMappingsJson", content, StringComparison.Ordinal);
            Assert.Contains("AtsLibrarySnapshotExport", content, StringComparison.Ordinal);
            Assert.Contains("AtsExplorerTable", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesSummaryUnmatchedSeasons", content, StringComparison.Ordinal);
            Assert.Contains("ats-overview-grid", content, StringComparison.Ordinal);
            Assert.Contains("ats-overview-section", content, StringComparison.Ordinal);
            Assert.Contains("Library</h3>", content, StringComparison.Ordinal);
            Assert.Contains("Matching</h3>", content, StringComparison.Ordinal);
            Assert.Contains("Local Files</h3>", content, StringComparison.Ordinal);
            Assert.Contains("Storage & Cache</h3>", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesCacheBytes", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesBrowserRebuildCache", content, StringComparison.Ordinal);
            Assert.Contains("max-height: 28rem;", content, StringComparison.Ordinal);
            Assert.Contains("overflow: auto;", content, StringComparison.Ordinal);
            Assert.Contains("position: sticky;", content, StringComparison.Ordinal);
            Assert.Contains("background: #202124;", content, StringComparison.Ordinal);
            Assert.Contains("ats-empty-actions", content, StringComparison.Ordinal);
        }

        var jellyfinPage = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"));
        Assert.Contains("AnimeThemesSync/Summary", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesSync/ThemeFiles/Delete", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesSync/SeasonMappings", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesSync/Search?query=", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("setViewMode", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("saveSeasonMapping", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("Save & Download", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("MatchedTitle", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("renderSeasonGroups", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("renderDetailLoading", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("renderDetailError", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("renderLibrarySkeleton", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("renderFinderSkeleton", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("renderSummarySkeleton", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("selectDefaultGroup", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("isSpecialGroup", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("openFinderForSeasonGroup", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesBrowserMatchInFinder", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("Match in Season Finder", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("resolveFinderTargetGroup", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("SeasonNumber', 'seasonNumber')) === 1", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("syncMatchInFinderButton", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("isFinderMatchActionAvailable", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("appendEmptyMatchAction", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("createButton('Match in Season Finder', false, 'link')", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("if (!allRows.length)", jellyfinPage, StringComparison.Ordinal);
        Assert.DoesNotContain("ats-season-pill-wrap", jellyfinPage, StringComparison.Ordinal);
        Assert.DoesNotContain("ats-season-match-button", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("SeasonItemId", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("scheduleAnimeThemesSearch(250)", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("canonicalizeFullSettings", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("PluginConfiguration JSON must be an object", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("delete full.SeasonThemeMappings", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("mappingImportRowsFromRaw", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("Mappings JSON must be an array or an object with a Mappings array", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesSync/SeasonMappings/Import", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("exportMappings", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("exportLibrarySnapshot", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("Array.isArray(parsed)", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("renderMappingExplorer", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("explorerTable.appendChild(table)", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("addHeroMetaChip", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("addRemotePreviewButton(actions, row, 'video')", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("addRemotePreviewButton(actions, row, 'audio')", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("detailToken", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("EmptyMessage", jellyfinPage, StringComparison.Ordinal);
        Assert.DoesNotContain("+ ' - Themes'", jellyfinPage, StringComparison.Ordinal);
        Assert.DoesNotContain("state.currentResult = {}", jellyfinPage, StringComparison.Ordinal);
        Assert.DoesNotContain("window.history", jellyfinPage, StringComparison.Ordinal);
        Assert.DoesNotContain("popstate", jellyfinPage, StringComparison.Ordinal);
        Assert.DoesNotContain("scheduleAnimeThemesSearch(350)", jellyfinPage, StringComparison.Ordinal);

        var embyController = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js"));
        Assert.Contains("AnimeThemesSync/Summary", embyController, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesSync/ThemeFiles/Delete", embyController, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesSync/SeasonMappings", embyController, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesSync/Search?query=", embyController, StringComparison.Ordinal);
        Assert.Contains("setViewMode", embyController, StringComparison.Ordinal);
        Assert.Contains("saveSeasonMapping", embyController, StringComparison.Ordinal);
        Assert.Contains("Save & Download", embyController, StringComparison.Ordinal);
        Assert.Contains("MatchedTitle", embyController, StringComparison.Ordinal);
        Assert.Contains("renderSeasonGroups", embyController, StringComparison.Ordinal);
        Assert.Contains("renderDetailLoading", embyController, StringComparison.Ordinal);
        Assert.Contains("renderDetailError", embyController, StringComparison.Ordinal);
        Assert.Contains("renderLibrarySkeleton", embyController, StringComparison.Ordinal);
        Assert.Contains("renderFinderSkeleton", embyController, StringComparison.Ordinal);
        Assert.Contains("renderSummarySkeleton", embyController, StringComparison.Ordinal);
        Assert.Contains("selectDefaultGroup", embyController, StringComparison.Ordinal);
        Assert.Contains("isSpecialGroup", embyController, StringComparison.Ordinal);
        Assert.Contains("openFinderForSeasonGroup", embyController, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesBrowserMatchInFinder", File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html")), StringComparison.Ordinal);
        Assert.Contains("Match in Season Finder", embyController, StringComparison.Ordinal);
        Assert.Contains("resolveFinderTargetGroup", embyController, StringComparison.Ordinal);
        Assert.Contains("SeasonNumber', 'seasonNumber')) === 1", embyController, StringComparison.Ordinal);
        Assert.Contains("syncMatchInFinderButton", embyController, StringComparison.Ordinal);
        Assert.Contains("isFinderMatchActionAvailable", embyController, StringComparison.Ordinal);
        Assert.Contains("appendEmptyMatchAction", embyController, StringComparison.Ordinal);
        Assert.Contains("createButton('Match in Season Finder', false, 'link')", embyController, StringComparison.Ordinal);
        Assert.Contains("if (!allRows.length)", embyController, StringComparison.Ordinal);
        Assert.DoesNotContain("ats-season-pill-wrap", File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html")), StringComparison.Ordinal);
        Assert.DoesNotContain("ats-season-match-button", embyController, StringComparison.Ordinal);
        Assert.Contains("SeasonItemId", embyController, StringComparison.Ordinal);
        Assert.Contains("scheduleAnimeThemesSearch(250)", embyController, StringComparison.Ordinal);
        Assert.Contains("canonicalizeFullSettings", embyController, StringComparison.Ordinal);
        Assert.Contains("PluginConfiguration JSON must be an object", embyController, StringComparison.Ordinal);
        Assert.Contains("delete full.SeasonThemeMappings", embyController, StringComparison.Ordinal);
        Assert.Contains("mappingImportRowsFromRaw", embyController, StringComparison.Ordinal);
        Assert.Contains("Mappings JSON must be an array or an object with a Mappings array", embyController, StringComparison.Ordinal);
        Assert.Contains("AnimeThemesSync/SeasonMappings/Import", embyController, StringComparison.Ordinal);
        Assert.Contains("exportMappings", embyController, StringComparison.Ordinal);
        Assert.Contains("exportLibrarySnapshot", embyController, StringComparison.Ordinal);
        Assert.Contains("Array.isArray(parsed)", embyController, StringComparison.Ordinal);
        Assert.Contains("renderMappingExplorer", embyController, StringComparison.Ordinal);
        Assert.Contains("explorerTable.appendChild(table)", embyController, StringComparison.Ordinal);
        Assert.Contains("addHeroMetaChip", embyController, StringComparison.Ordinal);
        Assert.Contains("addRemotePreviewButton(actions, row, 'video')", embyController, StringComparison.Ordinal);
        Assert.Contains("addRemotePreviewButton(actions, row, 'audio')", embyController, StringComparison.Ordinal);
        Assert.Contains("detailToken", embyController, StringComparison.Ordinal);
        Assert.Contains("EmptyMessage", embyController, StringComparison.Ordinal);
        Assert.DoesNotContain("+ ' - Themes'", embyController, StringComparison.Ordinal);
        Assert.DoesNotContain("state.currentResult = {}", embyController, StringComparison.Ordinal);
        Assert.DoesNotContain("window.history", embyController, StringComparison.Ordinal);
        Assert.DoesNotContain("popstate", embyController, StringComparison.Ordinal);
        Assert.DoesNotContain("scheduleAnimeThemesSearch(350)", embyController, StringComparison.Ordinal);

        var dtoFile = File.ReadAllText(Path.Combine(root, "AnimeThemesSync.Shared", "Models", "ThemeBrowserDtos.cs"));
        Assert.Contains("ThemeBrowserThemeGroup", dtoFile, StringComparison.Ordinal);
        Assert.Contains("List<ThemeBrowserThemeGroup>? Groups", dtoFile, StringComparison.Ordinal);
        Assert.Contains("Guid? SeriesItemId", dtoFile, StringComparison.Ordinal);
        Assert.Contains("Guid? SeasonItemId", dtoFile, StringComparison.Ordinal);
        Assert.Contains("int SeriesItems", dtoFile, StringComparison.Ordinal);
        Assert.Contains("int UnmatchedSeasons", dtoFile, StringComparison.Ordinal);
        Assert.Contains("SeasonThemeMappingImportResult", dtoFile, StringComparison.Ordinal);
        Assert.Contains("ImportSeasonThemeMappingsRequest", dtoFile, StringComparison.Ordinal);
        Assert.Contains("ImportSeasonThemeMappingRow", dtoFile, StringComparison.Ordinal);
        Assert.Contains("int Imported", dtoFile, StringComparison.Ordinal);
        Assert.Contains("int Skipped", dtoFile, StringComparison.Ordinal);
        Assert.Contains("List<string> Errors", dtoFile, StringComparison.Ordinal);

        var jellyfinDownloader = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"));
        var embyDownloader = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"));
        var jellyfinController = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Api", "AnimeThemesSyncController.cs"));
        var embyService = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Api", "AnimeThemesSyncService.cs"));
        Assert.Contains("Uses series-level themes", jellyfinDownloader, StringComparison.Ordinal);
        Assert.Contains("Uses series-level themes", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("SeasonThemeDownloadsDisabled", jellyfinDownloader, StringComparison.Ordinal);
        Assert.Contains("SeasonThemeDownloadsDisabled", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("GetBrowserSummary()", jellyfinDownloader, StringComparison.Ordinal);
        Assert.Contains("GetBrowserSummary()", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("ThemeBrowserSummary", dtoFile, StringComparison.Ordinal);
        Assert.True(jellyfinDownloader.Split("new BrowserAnimeResolution(seriesAnime, \"Series\", \"SeriesLevel\", false)").Length >= 3);
        Assert.True(embyDownloader.Split("new BrowserAnimeResolution(seriesAnime, \"Series\", \"SeriesLevel\", false)").Length >= 3);
        Assert.Contains("ImportSeasonThemeMappingsAsync", jellyfinDownloader, StringComparison.Ordinal);
        Assert.Contains("ImportSeasonThemeMappingsAsync", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("SeasonMappings/Import", jellyfinController, StringComparison.Ordinal);
        Assert.Contains("SeasonMappings/Import", embyService, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeFinderSearch_UsesAnimeThemesAnimeIndexWithoutAniListFallback()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs")
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var start = content.IndexOf("public async Task<IReadOnlyList<ThemeFinderSearchResult>> SearchThemeFinderAnimeAsync", StringComparison.Ordinal);
            var end = content.IndexOf("public async Task<ThemeBrowserItemResult> GetAnimeThemePreviewAsync", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);

            var method = content[start..end];
            Assert.Contains("SearchAnimeByTitle(query, year", method, StringComparison.Ordinal);
            Assert.Contains("Take(15)", method, StringComparison.Ordinal);
            Assert.Contains("GetAnimePrimaryImageUrl", method, StringComparison.Ordinal);
            Assert.DoesNotContain("_aniListService.SearchAnime", method, StringComparison.Ordinal);
            Assert.DoesNotContain("ResolveAnimePrimaryImageUrlAsync", method, StringComparison.Ordinal);
            Assert.DoesNotContain("OrderByDescending", method, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SeasonFinderMappings_LoadWithoutExternalResolutionAndHideSpecialSeasons()
    {
        var root = FindRepositoryRoot();
        var downloaderFiles = new[]
        {
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs")
        };

        foreach (var file in downloaderFiles)
        {
            var content = File.ReadAllText(file);
            Assert.Contains("public Task<IReadOnlyList<SeasonThemeMappingRow>> GetSeasonThemeMappingsAsync", content, StringComparison.Ordinal);
            Assert.Contains("private SeasonThemeMappingRow BuildSeasonMappingRow(", content, StringComparison.Ordinal);
            Assert.Contains("private SeasonThemeMatchState BuildSeasonThemeMatchState(", content, StringComparison.Ordinal);
            Assert.Contains("_seasonFinderStore.GetAllRows()", content, StringComparison.Ordinal);
            Assert.Contains("public SeasonFinderItemsPage GetSeasonFinderItems(", content, StringComparison.Ordinal);

            var start = content.IndexOf("public Task<IReadOnlyList<SeasonThemeMappingRow>> GetSeasonThemeMappingsAsync", StringComparison.Ordinal);
            var end = content.IndexOf("public async Task<IReadOnlyList<ThemeFinderSearchResult>> SearchThemeFinderAnimeAsync", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);

            var mappingsMethod = content[start..end];
            Assert.Contains("_seasonFinderStore.GetAllRows()", mappingsMethod, StringComparison.Ordinal);
            Assert.Contains("_seasonFinderStore.QueryRows", mappingsMethod, StringComparison.Ordinal);
            Assert.DoesNotContain("GetEnabledLibraryItems", mappingsMethod, StringComparison.Ordinal);
            Assert.DoesNotContain("ResolveAnime(series", mappingsMethod, StringComparison.Ordinal);
            Assert.DoesNotContain("BuildAutomaticSeasonAnimeMapAsync", mappingsMethod, StringComparison.Ordinal);

            Assert.DoesNotContain("GetSeasonThemeMappingAsync", content, StringComparison.Ordinal);
            Assert.DoesNotContain("BuildResolvedSeasonMappingRowAsync", content, StringComparison.Ordinal);
            Assert.Contains("SaveAutomaticSeasonThemeMapping(series, season", content, StringComparison.Ordinal);
            Assert.Contains("Locked = false", content, StringComparison.Ordinal);
            Assert.Contains("existing?.Locked == true", content, StringComparison.Ordinal);
            Assert.Contains("var status = mapping.Locked ? \"Manual\" : \"Auto\"", content, StringComparison.Ordinal);
            Assert.Contains("ResolveSeasonBrowserAnimeAsync(series, season", content, StringComparison.Ordinal);
            Assert.Contains("ResolveAnimeByIdentityAsync", content, StringComparison.Ordinal);
            Assert.Contains("GetSeasonMappingMatchRank", content, StringComparison.Ordinal);
            Assert.Contains("OrderByDescending(candidate => candidate.Mapping.Locked)", content, StringComparison.Ordinal);
            Assert.Contains("MatchesId(mapping.SeriesItemId", content, StringComparison.Ordinal);
            Assert.Contains("season.IndexNumber == 0", content, StringComparison.Ordinal);
            Assert.Contains("IndexOf(\"special\", StringComparison.OrdinalIgnoreCase)", content, StringComparison.Ordinal);
            Assert.Contains(".Where(s => IsSeasonEligibleForThemeMatching(s) && s.IndexNumber.HasValue && s.IndexNumber.Value > 1)", content, StringComparison.Ordinal);
        }

        var browserFiles = new[]
        {
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js")
        };

        foreach (var file in browserFiles)
        {
            var content = File.ReadAllText(file);
            Assert.Contains("return !isSpecialGroup(group);", content, StringComparison.Ordinal);
            Assert.Contains("function seasonMappingHasMatch(row)", content, StringComparison.Ordinal);
            Assert.Contains("status === 'auto'", content, StringComparison.Ordinal);
            Assert.Contains("status === 'direct'", content, StringComparison.Ordinal);
            Assert.Contains("status === 'series'", content, StringComparison.Ordinal);
            Assert.Contains("value(row, 'AniListId', 'aniListId')", content, StringComparison.Ordinal);
            Assert.Contains("value(row, 'MyAnimeListId', 'myAnimeListId')", content, StringComparison.Ordinal);
            Assert.Contains("if (!hasMatch) addChip(chips, 'Needs match', 'missing');", content, StringComparison.Ordinal);
            Assert.Contains("IntersectionObserver", content, StringComparison.Ordinal);
            Assert.Contains("rootMargin: '300px 0px'", content, StringComparison.Ordinal);
            Assert.DoesNotContain("AnimeThemesBrowserLimit", content, StringComparison.Ordinal);
            Assert.DoesNotContain("libraryLimit", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesSync/SeasonFinder?", content, StringComparison.Ordinal);
            Assert.Contains("finderRequestToken", content, StringComparison.Ordinal);
            Assert.Contains("finderCacheVersion", content, StringComparison.Ordinal);
            Assert.Contains("reloadSeasonMappingsPreservingState", content, StringComparison.Ordinal);
            Assert.Contains("Math.max(previousCount, state.finderLimit)", content, StringComparison.Ordinal);
            Assert.Contains("accumulated.length < targetCount", content, StringComparison.Ordinal);
            Assert.DoesNotContain("function resolveSeasonMappings(rows)", content, StringComparison.Ordinal);
            Assert.DoesNotContain("seasonMappingResolveToken", content, StringComparison.Ordinal);
            Assert.DoesNotContain("groupHasContent", content, StringComparison.Ordinal);
            Assert.DoesNotContain("if (!value(row, 'AnimeThemesSlug', 'animeThemesSlug')) addChip(chips, 'Needs match', 'missing');", content, StringComparison.Ordinal);
        }

        foreach (var file in downloaderFiles)
        {
            var content = File.ReadAllText(file);
            Assert.Contains("ApplySeasonThemeMappingChanges", content, StringComparison.Ordinal);
            Assert.DoesNotContain("_seasonFinderStore.ReplaceSeasonThemeMappings", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HostDownloaders_NormalizeSeasonOneOutputAndUseLogicalActionItem()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs")
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Assert.Contains("private ThemeOutputTarget? ResolveThemeOutputTarget", content, StringComparison.Ordinal);
            Assert.Contains("return !season.IndexNumber.HasValue || season.IndexNumber.Value == 1;", content, StringComparison.Ordinal);
            Assert.Contains("new ThemeOutputTarget(season.Id, series.Id, series.Path, ThemeOutputScope.SeriesRoot, true)", content, StringComparison.Ordinal);
            Assert.Contains("var representativeSeason = seasonsWithPath.FirstOrDefault(IsSeriesRootSeason);", content, StringComparison.Ordinal);
            Assert.Contains("fileNamePrefix = outputTarget.IsRedirected ? \"Season 01 -\" : null", content, StringComparison.Ordinal);
            Assert.DoesNotContain("ShouldOutputMappedSeasonToSeriesRoot", content, StringComparison.Ordinal);
            Assert.DoesNotContain("BuildPlan(anime, item.Path", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ThemeExtrasFileService_CopyOnlyCreatesBrowseableExtra()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AnimeThemesSyncTests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(directory, "backdrops", "01-OP1.webm");
        var target = Path.Combine(directory, "extras", "AnimeThemes - 01 - OP1.webm");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, "video");

            var action = ThemeExtrasFileService.EnsureExtraFile(source, target, ExtrasLinkMode.CopyOnly, overwrite: false);

            Assert.Equal("copied", action);
            Assert.True(File.Exists(target));
            Assert.Equal("video", File.ReadAllText(target));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ThemeExtrasFileService_HardLinkOnlyCreatesSharedFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AnimeThemesSyncTests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(directory, "backdrops", "01-OP1.webm");
        var target = Path.Combine(directory, "extras", "AnimeThemes - 01 - OP1.webm");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, "video");

            var action = ThemeExtrasFileService.EnsureExtraFile(source, target, ExtrasLinkMode.HardLinkOnly, overwrite: false);

            Assert.Equal("hard-linked", action);
            Assert.True(File.Exists(target));
            File.AppendAllText(target, "-extra");
            Assert.Equal("video-extra", File.ReadAllText(source));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ThemeExtrasFileService_HardLinkOnlyReplacesExistingCopy()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AnimeThemesSyncTests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(directory, "backdrops", "01-OP1.webm");
        var target = Path.Combine(directory, "extras", "AnimeThemes - 01 - OP1.webm");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(source, "source");
            File.WriteAllText(target, "old-copy");

            var result = ThemeExtrasFileService.EnsureExtraFileDetailed(source, target, ExtrasLinkMode.HardLinkOnly, overwrite: false);

            Assert.Equal("hard-linked", result.Action);
            Assert.True(result.HardLinkVerified is null or true);
            File.AppendAllText(target, "-extra");
            Assert.Equal("source-extra", File.ReadAllText(source));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void BrowserPages_SynchronizeMutationsAndReleaseMediaBeforeDelete()
    {
        var root = FindRepositoryRoot();
        var scripts = new[]
        {
            File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html")),
            File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.js"))
        };

        foreach (var script in scripts)
        {
            Assert.Contains("function scheduleUiRefresh(options)", script, StringComparison.Ordinal);
            Assert.Contains("if (terminalTransition) scheduleUiRefresh();", script, StringComparison.Ordinal);
            Assert.Contains("token !== state.browserRequestToken", script, StringComparison.Ordinal);
            Assert.Contains("loadItems(false, { silent: true, preserveCount: true })", script, StringComparison.Ordinal);
            Assert.Contains("normalizeApiError", script, StringComparison.Ordinal);
            Assert.Contains("response.text()", script, StringComparison.Ordinal);
            Assert.Contains("function releasePlayerMedia()", script, StringComparison.Ordinal);
            Assert.Contains("media.removeAttribute('src')", script, StringComparison.Ordinal);
            Assert.Contains("restoreThemeTarget(row, outcome.target", script, StringComparison.Ordinal);
            Assert.DoesNotContain("dismissBtn.textContent = '\\u00d7'", script, StringComparison.Ordinal);
        }

        var pages = new[]
        {
            File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "browserPage.html")),
            File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "browserPage.html"))
        };
        foreach (var page in pages)
        {
            Assert.Contains(".ats-dm-item-dismiss::before", page, StringComparison.Ordinal);
            Assert.Contains("translate(-50%, -50%) rotate(45deg)", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HostDownloaders_RefreshParentSeriesAndRetryLockedDeletes()
    {
        var root = FindRepositoryRoot();
        var downloaderFiles = new[]
        {
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs")
        };

        foreach (var file in downloaderFiles)
        {
            var content = File.ReadAllText(file);
            Assert.Contains("item = FindSeriesForSeason(season) ?? item;", content, StringComparison.Ordinal);
            Assert.Contains("RefreshBrowserCacheForItem(series);", content, StringComparison.Ordinal);
            Assert.Contains("FileDeleteRetryService.DeleteAsync", content, StringComparison.Ordinal);
        }

        var retryService = File.ReadAllText(Path.Combine(root, "AnimeThemesSync.Shared", "Services", "FileDeleteRetryService.cs"));
        Assert.Contains("[250, 500, 1000, 2000]", retryService, StringComparison.Ordinal);
        Assert.Contains("catch (IOException ex)", retryService, StringComparison.Ordinal);
        Assert.Contains("throw new InvalidOperationException", retryService, StringComparison.Ordinal);
    }

    private static ThemeConfig Enabled(int maxThemes)
    {
        return new ThemeConfig
        {
            MaxThemes = maxThemes,
            IgnoreEd = false,
            IgnoreOp = false
        };
    }

    private static ThemeConfig Disabled()
    {
        return new ThemeConfig { MaxThemes = 0 };
    }

    private static AnimeThemesAnime CreateAnime()
    {
        return new AnimeThemesAnime
        {
            AnimeThemes =
            [
                new AnimeThemesTheme
                {
                    Type = "OP",
                    Sequence = 1,
                    Slug = "OP1",
                    Song = new AnimeThemesSong
                    {
                        Title = "staple stable",
                        Performances =
                        [
                            new AnimeThemesPerformance
                            {
                                Relevance = 1,
                                Artist = new AnimeThemesArtist { Name = "Chiwa Saitou" }
                            }
                        ]
                    },
                    Entries =
                    [
                        new AnimeThemesEntry
                        {
                            Version = 1,
                            Episodes = "1-2, 12",
                            Spoiler = false,
                            Videos =
                            [
                                new AnimeThemesVideo
                                {
                                    Link = "https://v.animethemes.moe/op1.webm",
                                    Nc = true,
                                    Source = "BD",
                                    Resolution = 1080,
                                    Audio = new AnimeThemesAudio { Link = "https://a.animethemes.moe/op1.ogg" }
                                }
                            ]
                        }
                    ]
                },
                new AnimeThemesTheme
                {
                    Type = "OP",
                    Sequence = 2,
                    Slug = "OP2",
                    Song = new AnimeThemesSong
                    {
                        Title = "Kaerimichi",
                        Artists = [new AnimeThemesArtist { Name = "Emiri Katou" }]
                    },
                    Entries =
                    [
                        new AnimeThemesEntry
                        {
                            Version = 1,
                            Episodes = "3-5",
                            Spoiler = true,
                            Videos =
                            [
                                new AnimeThemesVideo
                                {
                                    Link = "https://v.animethemes.moe/op2.webm",
                                    Source = "WEB",
                                    Resolution = 720,
                                    Tags = "TV",
                                    Audio = new AnimeThemesAudio { Link = "https://a.animethemes.moe/op2.ogg" }
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Jellyfin.Plugin.AnimeThemesSync.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
