using System;
using System.IO;
using System.Linq;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using Xunit;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public class ThemeFilePlannerTests
{
    [Fact]
    public void BuildPlan_UsesRichExtrasNameFromAnimeThemesMetadata()
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
        Assert.Contains("AnimeThemes - 01 - OP1 - staple stable - Chiwa Saitou - Eps 1-2, 12 - NC BD1080.webm", extrasNames);
        Assert.Contains("AnimeThemes - 02 - OP2 - Kaerimichi - Emiri Katou - Eps 3-5 - Spoiler WEB720.webm", extrasNames);
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

        Assert.Equal("01-OP1 - staple stable - Chiwa Saitou.mp3", audioName);
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

        Assert.Equal("01-ED Finalv2 - bad title.mp3", Path.GetFileName(plan.MediaFiles.Single(f => !f.IsVideo).Path));
        Assert.Equal("01-ED Finalv2 - bad title.webm", Path.GetFileName(plan.MediaFiles.Single(f => f.IsVideo).Path));
        Assert.Equal("AnimeThemes - 01 - ED Finalv2 - bad title.webm", Path.GetFileName(plan.ExtraFiles.Single().TargetPath));
    }

    [Fact]
    public void BuildPlan_TruncatesLongExtrasNames()
    {
        var anime = CreateAnime();
        anime.AnimeThemes![0].Song!.Title = new string('A', 220);

        var plan = ThemeFilePlanner.BuildPlan(anime, Path.Combine("Media", "Test"), Disabled(), Enabled(maxThemes: 1), extrasEnabled: true);

        var extrasName = Path.GetFileName(plan.ExtraFiles.Single().TargetPath);

        Assert.True(extrasName.Length <= 180);
        Assert.StartsWith("AnimeThemes - 01 - OP1 - ", extrasName, StringComparison.Ordinal);
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
        Assert.Equal("02-OP2 - Kaerimichi - Emiri Katou.mp3", Path.GetFileName(plan.MediaFiles.Single(f => !f.IsVideo).Path));
        Assert.Equal("AnimeThemes - 02 - OP2 - Kaerimichi - Emiri Katou - Eps 3-5 - Spoiler WEB720.webm", Path.GetFileName(plan.ExtraFiles.Single().TargetPath));
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
            Assert.Contains("pollDownloadJob", content, StringComparison.Ordinal);
            Assert.Contains("AnimeThemesBrowserProgressBar", content, StringComparison.Ordinal);
            Assert.DoesNotContain("/Download?force=", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ConfigurationPagesExposeSeasonThemeMappings()
    {
        var root = FindRepositoryRoot();
        var configPages = new[]
        {
            Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "configPage.html"),
            Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "configPage.html")
        };

        foreach (var file in configPages)
        {
            var content = File.ReadAllText(file);
            Assert.Contains("SeasonThemeDownloadsEnabled", content, StringComparison.Ordinal);
            Assert.Contains("Enable Season Theme Downloads", content, StringComparison.Ordinal);
            Assert.Contains("SeasonThemeMappingsJson", content, StringComparison.Ordinal);
            Assert.Contains("Season Theme Mapping", content, StringComparison.Ordinal);
            Assert.Contains("Season Mappings JSON", content, StringComparison.Ordinal);
        }

        var jellyfinPage = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "Configuration", "configPage.html"));
        Assert.Contains("SeasonThemeMappings", jellyfinPage, StringComparison.Ordinal);
        Assert.Contains("parseSeasonMappings", jellyfinPage, StringComparison.Ordinal);

        var embyController = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "Configuration", "configPage.js"));
        Assert.Contains("SeasonThemeDownloadsEnabled", embyController, StringComparison.Ordinal);
        Assert.Contains("SeasonThemeMappings", embyController, StringComparison.Ordinal);
        Assert.Contains("parseSeasonMappings", embyController, StringComparison.Ordinal);
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

        var jellyfinDownloader = File.ReadAllText(Path.Combine(root, "Jellyfin.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"));
        var embyDownloader = File.ReadAllText(Path.Combine(root, "Emby.Plugin.AnimeThemesSync", "ScheduledTasks", "ThemeDownloader.cs"));
        Assert.Contains("Uses series-level themes", jellyfinDownloader, StringComparison.Ordinal);
        Assert.Contains("Uses series-level themes", embyDownloader, StringComparison.Ordinal);
        Assert.Contains("SeasonThemeDownloadsDisabled", jellyfinDownloader, StringComparison.Ordinal);
        Assert.Contains("SeasonThemeDownloadsDisabled", embyDownloader, StringComparison.Ordinal);
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
    public void SeasonFinderMappings_UseBrowserResolutionAndHideSpecialSeasons()
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
            Assert.Contains("public async Task<IReadOnlyList<SeasonThemeMappingRow>> GetSeasonThemeMappingsAsync", content, StringComparison.Ordinal);
            Assert.DoesNotContain("private SeasonThemeMappingRow BuildSeasonMappingRow(", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Task.FromResult(BuildSeasonMappingRow", content, StringComparison.Ordinal);

            var start = content.IndexOf("public async Task<IReadOnlyList<SeasonThemeMappingRow>> GetSeasonThemeMappingsAsync", StringComparison.Ordinal);
            var end = content.IndexOf("public async Task<IReadOnlyList<ThemeFinderSearchResult>> SearchThemeFinderAnimeAsync", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);

            var mappingsMethod = content[start..end];
            Assert.Contains("ResolveAnime(series, cancellationToken, logMissingIds: false)", mappingsMethod, StringComparison.Ordinal);
            Assert.Contains("BuildAutomaticSeasonAnimeMapAsync(series, seasons, seriesAnime, cancellationToken)", mappingsMethod, StringComparison.Ordinal);
            Assert.Contains("BuildSeasonMappingRowAsync(series, season, seriesAnime, automaticSeasonAnime, cancellationToken)", mappingsMethod, StringComparison.Ordinal);
            Assert.Contains(".Where(IsSeasonEligibleForThemeMatching)", mappingsMethod, StringComparison.Ordinal);

            Assert.Contains("BuildResolvedSeasonMappingRowAsync(series, season, cancellationToken)", content, StringComparison.Ordinal);
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
            Assert.Contains("if (!hasMatch) addChip(chips, 'Needs match', 'missing');", content, StringComparison.Ordinal);
            Assert.DoesNotContain("groupHasContent", content, StringComparison.Ordinal);
            Assert.DoesNotContain("if (!value(row, 'AnimeThemesSlug', 'animeThemesSlug')) addChip(chips, 'Needs match', 'missing');", content, StringComparison.Ordinal);
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
