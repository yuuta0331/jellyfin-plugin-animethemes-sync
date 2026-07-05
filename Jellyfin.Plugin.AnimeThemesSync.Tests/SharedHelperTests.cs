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
