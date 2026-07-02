using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Services;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class SeasonMetadataPlannerTests
{
    [Theory]
    [InlineData(SeasonTagTarget.Series, true, false)]
    [InlineData(SeasonTagTarget.Season, false, true)]
    [InlineData(SeasonTagTarget.Both, true, true)]
    public void TagTarget_SelectsExpectedItems(SeasonTagTarget target, bool series, bool season)
    {
        Assert.Equal(series, SeasonMetadataPlanner.AppliesToSeries(target));
        Assert.Equal(season, SeasonMetadataPlanner.AppliesToSeason(target));
    }

    [Fact]
    public void CreateBroadcastSeason_UsesStableKeyAndLocalizedLabel()
    {
        var value = SeasonMetadataPlanner.CreateBroadcastSeason(
            2024,
            "autumn",
            "{Year} / {Season}",
            "Spring",
            "Summer",
            "Autumn",
            "Winter");

        Assert.NotNull(value);
        Assert.Equal("2024-fall", value.Key);
        Assert.Equal("2024 / Autumn", value.Label);
        Assert.Equal(new[] { "2024", "2024 / Autumn" }, SeasonMetadataPlanner.BuildTags(value));
    }

    [Theory]
    [InlineData(null, "winter")]
    [InlineData(2024, null)]
    [InlineData(2024, "")]
    public void CreateBroadcastSeason_RequiresYearAndSeason(int? year, string? season)
    {
        Assert.Null(SeasonMetadataPlanner.CreateBroadcastSeason(year, season, "{Season} {Year}", "Spring", "Summer", "Fall", "Winter"));
    }

    [Theory]
    [InlineData(1, true, true)]
    [InlineData(1, false, false)]
    [InlineData(2, true, false)]
    [InlineData(null, true, false)]
    public void UsesSeriesForCollection_OnlyReplacesSeasonOne(int? seasonNumber, bool enabled, bool expected)
    {
        Assert.Equal(expected, SeasonMetadataPlanner.UsesSeriesForCollection(seasonNumber, enabled));
    }

    [Fact]
    public void BuildTags_RemovesDuplicateYearLabel()
    {
        var value = SeasonMetadataPlanner.CreateBroadcastSeason(2024, "winter", "{Year}", "Spring", "Summer", "Fall", "Winter")!;

        Assert.Equal(new[] { "2024" }, SeasonMetadataPlanner.BuildTags(value));
    }
}
