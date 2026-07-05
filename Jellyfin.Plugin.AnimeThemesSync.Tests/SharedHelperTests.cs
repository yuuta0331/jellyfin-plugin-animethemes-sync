using System;
using System.Collections.Generic;
using System.IO;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class SharedHelperTests
{
    [Fact]
    public void GetSeasonMappingMatchRank_PrefersSeasonIdThenPathThenSeriesWithNumber()
    {
        var seasonId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        var compactSeasonId = seasonId.Replace("-", string.Empty, StringComparison.Ordinal);
        var seriesId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
        var compactSeriesId = seriesId.Replace("-", string.Empty, StringComparison.Ordinal);
        var seasonPath = Path.Combine("D:", "Anime", "Example", "Season 02");
        var seriesPath = Path.Combine("D:", "Anime", "Example");

        int Rank(SeasonThemeMapping mapping) => SeasonMappingMatchHelper.GetSeasonMappingMatchRank(
            mapping, seasonId, compactSeasonId, seasonPath, seriesId, compactSeriesId, seriesPath, seriesPath, 2);

        Assert.Equal(4, Rank(new SeasonThemeMapping { SeasonItemId = compactSeasonId }));
        Assert.Equal(3, Rank(new SeasonThemeMapping { SeasonPath = seasonPath + Path.DirectorySeparatorChar }));
        Assert.Equal(2, Rank(new SeasonThemeMapping { SeriesItemId = seriesId, SeasonNumber = 2 }));
        Assert.Equal(1, Rank(new SeasonThemeMapping { SeriesPath = seriesPath, SeasonNumber = 2 }));
        Assert.Equal(0, Rank(new SeasonThemeMapping { SeriesPath = seriesPath, SeasonNumber = 3 }));
        Assert.Equal(0, Rank(new SeasonThemeMapping()));
    }

    [Fact]
    public void HasThemeIdentity_RequiresSlugOrProviderId()
    {
        Assert.True(SeasonMappingMatchHelper.HasThemeIdentity(new SeasonThemeMapping { AnimeThemesSlug = "slug" }));
        Assert.True(SeasonMappingMatchHelper.HasThemeIdentity(new SeasonThemeMapping { AniListId = 1 }));
        Assert.True(SeasonMappingMatchHelper.HasThemeIdentity(new SeasonThemeMapping { MyAnimeListId = 1 }));
        Assert.False(SeasonMappingMatchHelper.HasThemeIdentity(new SeasonThemeMapping { SeasonPath = "path" }));
    }

    [Fact]
    public void IsWithinCleanupRoots_RejectsSiblingsAndPrefixCollisions()
    {
        var root = Path.Combine("D:", "Anime", "Example", "theme-music");
        var roots = new[] { root };

        Assert.True(LocalMediaPathHelper.IsWithinCleanupRoots(Path.Combine(root, "OP1.mp3"), roots));
        Assert.False(LocalMediaPathHelper.IsWithinCleanupRoots(root, roots));
        Assert.False(LocalMediaPathHelper.IsWithinCleanupRoots(Path.Combine("D:", "Anime", "Example", "theme-music-extra", "x.mp3"), roots));
        Assert.False(LocalMediaPathHelper.IsWithinCleanupRoots(Path.Combine("D:", "Anime", "Other", "file.mp3"), roots));
    }

    [Fact]
    public void ValidateLocalMediaPath_RejectsEscapesAndUnmanagedFolders()
    {
        var itemPath = Path.Combine(Path.GetTempPath(), "ats-item");
        var managed = Path.Combine(itemPath, "theme-music", "OP1.mp3");
        LocalMediaPathHelper.ValidateLocalMediaPath(itemPath, managed);

        Assert.Throws<InvalidOperationException>(() =>
            LocalMediaPathHelper.ValidateLocalMediaPath(itemPath, Path.Combine(itemPath, "..", "outside", "OP1.mp3")));
        Assert.Throws<InvalidOperationException>(() =>
            LocalMediaPathHelper.ValidateLocalMediaPath(itemPath, Path.Combine(itemPath, "Season 01", "episode.mkv")));
        Assert.Throws<InvalidOperationException>(() =>
            LocalMediaPathHelper.ValidateLocalMediaPath(itemPath, Path.Combine(itemPath, "theme-music", "notes.txt")));
    }

    [Fact]
    public void CleanupDirectories_MapKindsAndRegisterManagedFolders()
    {
        Assert.Equal("Audio", LocalMediaPathHelper.CleanupFileKind(Path.Combine("root", "theme-music") + Path.DirectorySeparatorChar));
        Assert.Equal("Extra", LocalMediaPathHelper.CleanupFileKind(Path.Combine("root", "extras")));
        Assert.Equal("Video", LocalMediaPathHelper.CleanupFileKind(Path.Combine("root", "backdrops")));

        var directories = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        LocalMediaPathHelper.AddCleanupDirectories(null, directories);
        Assert.Empty(directories);
        LocalMediaPathHelper.AddCleanupDirectories(Path.Combine("D:", "Anime", "Example"), directories);
        Assert.Equal(3, directories.Count);
    }

    [Fact]
    public void ThemeBrowserRowBuilder_BuildsRowsWithSavedFlagsFromFileSystem()
    {
        var anime = new AnimeThemesAnime
        {
            Name = "Example Show",
            Slug = "example_show",
            AnimeThemes =
            [
                new AnimeThemesTheme
                {
                    Type = "OP",
                    Sequence = 1,
                    Slug = "OP1",
                    Song = new AnimeThemesSong { Title = "Opening Song" },
                    Entries =
                    [
                        new AnimeThemesEntry
                        {
                            Version = 1,
                            Videos =
                            [
                                new AnimeThemesVideo
                                {
                                    Link = "https://v.animethemes.moe/op1.webm",
                                    Resolution = 1080,
                                    Audio = new AnimeThemesAudio { Link = "https://a.animethemes.moe/op1.ogg" },
                                },
                            ],
                        },
                    ],
                },
            ],
        };
        var outputTarget = new ThemeOutputTarget(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Path.Combine(Path.GetTempPath(), "ats-series"),
            ThemeOutputScope.SeriesRoot,
            false);
        var fileSystem = new FakeThemeMediaFileSystem
        {
            ExistingFiles = { Path.Combine(outputTarget.OutputRootPath, "backdrops", "theme.webm") },
        };

        var rows = ThemeBrowserRowBuilder.BuildRowsForPath(
            outputTarget,
            anime,
            includeExtras: false,
            extrasFileNameFormat: ThemeFilePlanner.DefaultExtrasFileNameFormat,
            extrasFileSuffix: ExtrasFileSuffix.Other,
            fileNamePrefix: null,
            fileSystem);

        var row = Assert.Single(rows);
        Assert.Equal(1, row.Order);
        Assert.Equal("OP", row.Type);
        Assert.Equal("Opening Song", row.SongTitle);
        Assert.Equal("https://animethemes.moe/anime/example_show", row.AnimeThemesUrl);
        Assert.NotNull(row.BackdropPath);
        Assert.Equal(fileSystem.ExistingFiles.Contains(row.BackdropPath), row.BackdropExists);
        Assert.False(row.ThemeMusicExists);
        Assert.Null(row.ExtraPath);
    }

    private sealed class FakeThemeMediaFileSystem : global::AnimeThemesSync.Shared.Interfaces.IThemeMediaFileSystem
    {
        public HashSet<string> ExistingFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool FileExists(string path) => ExistingFiles.Contains(path);

        public bool DirectoryExists(string path) => false;

        public IEnumerable<string> GetFilePaths(string path) => Array.Empty<string>();

        public void DeleteFile(string path) => ExistingFiles.Remove(path);
    }

    [Fact]
    public void DownloadFailureHelper_ClassifiesPermanentTransientAndUnknownFailures()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ats-failures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new AnimeThemesDataStore(new TestDataPathProvider(directory), new TestServerIdentityProvider());

            DownloadFailureHelper.RecordScheduledFailure(store, "https://v/1", "D:\\a\\1.webm", new MediaDownloadException("gone", System.Net.HttpStatusCode.Gone, isTransient: false));
            var permanent = store.GetDownloadFailure("https://v/1", "D:\\a\\1.webm");
            Assert.Equal(DownloadFailureStatuses.PermanentFailed, permanent!.Status);
            Assert.True(permanent.NextRetryUtc > DateTimeOffset.UtcNow.AddDays(29));

            DownloadFailureHelper.RecordScheduledFailure(store, "https://v/2", "D:\\a\\2.webm", new MediaDownloadException("busy", System.Net.HttpStatusCode.TooManyRequests, isTransient: true, retryAfter: TimeSpan.FromMinutes(42)));
            var transientWithHint = store.GetDownloadFailure("https://v/2", "D:\\a\\2.webm");
            Assert.Equal(DownloadFailureStatuses.TransientFailed, transientWithHint!.Status);
            Assert.True(transientWithHint.NextRetryUtc > DateTimeOffset.UtcNow.AddMinutes(41));

            DownloadFailureHelper.RecordScheduledFailure(store, "https://v/3", "D:\\a\\3.webm", new IOException("disk hiccup"));
            var transientIo = store.GetDownloadFailure("https://v/3", "D:\\a\\3.webm");
            Assert.Equal(DownloadFailureStatuses.TransientFailed, transientIo!.Status);

            // Non-transient provider errors without a permanent status and unrelated
            // exceptions are not recorded, so the next run retries immediately.
            DownloadFailureHelper.RecordScheduledFailure(store, "https://v/4", "D:\\a\\4.webm", new MediaDownloadException("forbidden", System.Net.HttpStatusCode.Forbidden, isTransient: false));
            Assert.Null(store.GetDownloadFailure("https://v/4", "D:\\a\\4.webm"));
            DownloadFailureHelper.RecordScheduledFailure(store, "https://v/5", "D:\\a\\5.webm", new InvalidOperationException("bug"));
            Assert.Null(store.GetDownloadFailure("https://v/5", "D:\\a\\5.webm"));
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
    public void BuildLegacySeasonMetadataState_ProjectsTagsCollectionsAndBroadcastSeasons()
    {
        var automation = new SeasonAutomationState
        {
            SeriesItemId = "series-1",
            Rules =
            [
                new SeasonAutomationRuleRecord
                {
                    RuleKey = "rule-1", SeriesItemId = "series-1", SeasonItemId = "season-1", SeasonName = "Season 1",
                    AnimeYear = 2024, AnimeSeason = "spring", BroadcastSeasonKey = "2024-spring",
                    BroadcastSeasonLabel = "Spring 2024", Source = "Auto", UpdatedAtUtc = "2026-01-01T00:00:00Z",
                },
            ],
            Tags =
            [
                new SeasonAutomationTagRecord
                {
                    RuleKey = "rule-1", TargetItemId = "series-1", TargetItemType = "Series",
                    TagName = "Spring 2024", Source = "Auto", AddedByPlugin = true, UpdatedAtUtc = "2026-01-01T00:00:00Z",
                },
            ],
        };

        var state = SeasonAutomationStateHelper.BuildLegacySeasonMetadataState(automation, null);

        Assert.Equal("series-1", state.SeriesItemId);
        Assert.Equal("Spring 2024", Assert.Single(state.ManagedTags["series-1"]));
        Assert.Equal("2024-spring", Assert.Single(state.BroadcastSeasons).Key);
    }
}
