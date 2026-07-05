using System.IO;
using System.Xml.Serialization;
using AnimeThemesSync.Shared.Configuration;
using Jellyfin.Plugin.AnimeThemesSync.Configuration;

#pragma warning disable CS0618

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class PluginConfigurationMigrationTests
{
    [Fact]
    public void Constructor_CreatesCurrentDefaults()
    {
        var config = new PluginConfiguration();

        Assert.Equal(PluginConfiguration.CurrentConfigurationVersion, config.ConfigurationVersion);
        Assert.Equal(1, config.Series.Audio.MaxThemes);
        Assert.Equal(100, config.Series.Audio.Volume);
        Assert.True(config.Series.Audio.IgnoreEd);
        Assert.Equal(1, config.Movie.Video.MaxThemes);
        Assert.Equal(100, config.Movie.Video.Volume);
        Assert.True(config.Movie.Video.IgnoreEd);
        Assert.True(config.Series.Audio.UseAsTheme);
        Assert.True(config.Series.Video.UseAsTheme);
        Assert.Equal(ExtrasFileSuffix.Other, config.ExtrasFileSuffix);
        Assert.False(config.SegmentedDownloadEnabled);
        Assert.Equal(2, config.SegmentedDownloadSegments);
        Assert.Equal(25, config.MinimumSegmentedDownloadSizeMiB);
        Assert.Equal(2, config.MaximumConcurrentRangeRequests);
        Assert.Equal(string.Empty, config.DownloadStagingDirectory);
        Assert.Equal(SeasonTagTarget.Series, config.SeasonTagTarget);
        Assert.False(config.SeasonCollectionsEnabled);
        Assert.True(config.SeasonOneCollectionUseSeries);
        Assert.Equal("{Season} {Year}", config.SeasonCollectionFormat);
        Assert.True(config.SeasonCollectionLockEnabled);
        Assert.True(config.SeasonCollectionImagesEnabled);
        Assert.False(config.SeasonCollectionBackdropOverlayEnabled);
        Assert.Equal(35, config.SeasonCollectionBackdropOverlayOpacity);
        Assert.Equal("#000000", config.SeasonCollectionBackdropOverlayColor);
        Assert.Equal(SeasonCollectionPosterFillMode.ArtworkFill, config.SeasonCollectionPosterFillMode);
        Assert.Equal(SeasonCollectionLandscapeSourceMode.LandscapeOnly, config.SeasonCollectionLandscapeSourceMode);
        Assert.Equal(SeasonCollectionLandscapeArtType.Thumb, config.SeasonCollectionPosterFillLandscapeType);
        Assert.Equal(SeasonCollectionLandscapeArtType.Thumb, config.SeasonCollectionCanvasLandscapeType);
        Assert.Equal("#000000", config.SeasonCollectionCanvasColor);
        Assert.Equal(100, config.SeasonCollectionCanvasOpacity);
        Assert.Equal(30, config.SeasonMetadataCacheTtlDays);
        Assert.Equal(30, config.ProviderResponseCacheTtlDays);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(30, 30)]
    [InlineData(365, 365)]
    [InlineData(366, 365)]
    public void CacheTtlDays_AreClamped(int value, int expected)
    {
        var config = new PluginConfiguration
        {
            SeasonMetadataCacheTtlDays = value,
            ProviderResponseCacheTtlDays = value,
        };

        Assert.Equal(expected, config.SeasonMetadataCacheTtlDays);
        Assert.Equal(expected, config.ProviderResponseCacheTtlDays);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 5)]
    [InlineData(20, 10)]
    public void MaxConcurrentDownloads_IsClamped(int value, int expected)
    {
        var config = new PluginConfiguration { MaxConcurrentDownloads = value };

        Assert.Equal(expected, config.MaxConcurrentDownloads);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(150, 100)]
    public void SeasonCollectionCanvasOpacity_IsClamped(int value, int expected)
    {
        var config = new PluginConfiguration { SeasonCollectionCanvasOpacity = value };

        Assert.Equal(expected, config.SeasonCollectionCanvasOpacity);
    }

    [Theory]
    [InlineData("#FFFFFF", "#FFFFFF")]
    [InlineData("blue", "#000000")]
    [InlineData("", "#000000")]
    public void SeasonCollectionCanvasColor_FallsBackToDefaultWhenInvalid(string value, string expected)
    {
        var config = new PluginConfiguration { SeasonCollectionCanvasColor = value };

        Assert.Equal(expected, config.SeasonCollectionCanvasColor);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(35, 35)]
    [InlineData(90, 90)]
    [InlineData(120, 90)]
    public void SeasonCollectionBackdropOverlayOpacity_IsClamped(int value, int expected)
    {
        var config = new PluginConfiguration { SeasonCollectionBackdropOverlayOpacity = value };

        Assert.Equal(expected, config.SeasonCollectionBackdropOverlayOpacity);
    }

    [Theory]
    [InlineData("#FFFFFF", "#FFFFFF")]
    [InlineData("#abc", "#abc")]
    [InlineData("red", "#000000")]
    [InlineData("", "#000000")]
    [InlineData("#12345G", "#000000")]
    public void SeasonCollectionBackdropOverlayColor_FallsBackToDefaultWhenInvalid(string value, string expected)
    {
        var config = new PluginConfiguration { SeasonCollectionBackdropOverlayColor = value };

        Assert.Equal(expected, config.SeasonCollectionBackdropOverlayColor);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(10, 8)]
    public void SegmentedDownloadSegments_IsClamped(int value, int expected)
    {
        var config = new PluginConfiguration
        {
            SegmentedDownloadSegments = value,
        };

        Assert.Equal(expected, config.SegmentedDownloadSegments);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(25, 25)]
    [InlineData(2048, 1024)]
    public void MinimumSegmentedDownloadSizeMiB_IsClamped(int value, int expected)
    {
        var config = new PluginConfiguration { MinimumSegmentedDownloadSizeMiB = value };

        Assert.Equal(expected, config.MinimumSegmentedDownloadSizeMiB);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 4)]
    [InlineData(20, 16)]
    public void MaximumConcurrentRangeRequests_IsClamped(int value, int expected)
    {
        var config = new PluginConfiguration { MaximumConcurrentRangeRequests = value };

        Assert.Equal(expected, config.MaximumConcurrentRangeRequests);
    }

    [Fact]
    public void SeasonCollectionFormat_BlankValueUsesDefault()
    {
        var config = new PluginConfiguration { SeasonCollectionFormat = " " };

        Assert.Equal("{Season} {Year}", config.SeasonCollectionFormat);
    }

    [Fact]
    public void Normalize_LegacyFlatSettings_MigratesToStructuredProfiles()
    {
        var config = new PluginConfiguration
        {
            SeriesAudioMaxThemes = 2,
            SeriesAudioVolume = 45,
            SeriesAudioIgnoreOp = true,
            SeriesAudioIgnoreEd = false,
            SeriesAudioIgnoreOverlaps = true,
            SeriesAudioIgnoreCredits = true,
            SeriesVideoMaxThemes = 3,
            SeriesVideoVolume = 55,
            SeriesVideoIgnoreOp = false,
            SeriesVideoIgnoreEd = true,
            MovieAudioMaxThemes = 4,
            MovieAudioVolume = 65,
            MovieAudioIgnoreEd = false,
            MovieVideoMaxThemes = 5,
            MovieVideoVolume = 75,
            MovieVideoIgnoreOverlaps = true,
        };

        var changed = config.Normalize();

        Assert.True(changed);
        Assert.Equal(PluginConfiguration.CurrentConfigurationVersion, config.ConfigurationVersion);
        Assert.Equal(2, config.Series.Audio.MaxThemes);
        Assert.Equal(45, config.Series.Audio.Volume);
        Assert.True(config.Series.Audio.IgnoreOp);
        Assert.False(config.Series.Audio.IgnoreEd);
        Assert.True(config.Series.Audio.IgnoreOverlaps);
        Assert.True(config.Series.Audio.IgnoreCredits);
        Assert.Equal(3, config.Series.Video.MaxThemes);
        Assert.Equal(55, config.Series.Video.Volume);
        Assert.False(config.Series.Video.IgnoreOp);
        Assert.True(config.Series.Video.IgnoreEd);
        Assert.Equal(4, config.Movie.Audio.MaxThemes);
        Assert.Equal(65, config.Movie.Audio.Volume);
        Assert.False(config.Movie.Audio.IgnoreEd);
        Assert.Equal(5, config.Movie.Video.MaxThemes);
        Assert.Equal(75, config.Movie.Video.Volume);
        Assert.True(config.Movie.Video.IgnoreOverlaps);
    }

    [Fact]
    public void Normalize_PartialV2Config_RepairsAndClamps()
    {
        var config = new PluginConfiguration
        {
            Series = null!,
        };
        config.Movie.Audio = null!;
        config.Movie.Video.MaxThemes = -4;
        config.Movie.Video.Volume = 150;

        var changed = config.Normalize();

        Assert.True(changed);
        Assert.NotNull(config.Series);
        Assert.NotNull(config.Series.Audio);
        Assert.NotNull(config.Series.Video);
        Assert.NotNull(config.Movie.Audio);
        Assert.Equal(0, config.Movie.Video.MaxThemes);
        Assert.Equal(100, config.Movie.Video.Volume);
    }

    [Fact]
    public void Normalize_V2StructuredSettings_UpgradesWithoutReplacingProfiles()
    {
        var config = new PluginConfiguration
        {
            ConfigurationVersion = 2,
            ExtrasFileSuffix = (ExtrasFileSuffix)999,
        };
        config.Series.Audio.MaxThemes = 4;
        config.Series.Video.UseAsTheme = false;

        var changed = config.Normalize();

        Assert.True(changed);
        Assert.Equal(11, config.ConfigurationVersion);
        Assert.Equal(4, config.Series.Audio.MaxThemes);
        Assert.False(config.Series.Video.UseAsTheme);
        Assert.Equal(ExtrasFileSuffix.Other, config.ExtrasFileSuffix);
    }

    [Fact]
    public void Normalize_V3Config_UpgradesWithSegmentedDownloadDefaults()
    {
        var config = new PluginConfiguration
        {
            ConfigurationVersion = 3,
        };
        config.Series.Audio.MaxThemes = 3;

        var changed = config.Normalize();

        Assert.True(changed);
        Assert.Equal(11, config.ConfigurationVersion);
        Assert.Equal(3, config.Series.Audio.MaxThemes);
        Assert.False(config.SegmentedDownloadEnabled);
        Assert.Equal(2, config.SegmentedDownloadSegments);
    }

    [Fact]
    public void Normalize_V8Config_UpgradesWithCachePolicyDefaults()
    {
        var config = new PluginConfiguration
        {
            ConfigurationVersion = 8,
        };

        Assert.True(config.Normalize());
        Assert.Equal(11, config.ConfigurationVersion);
        Assert.Equal(30, config.SeasonMetadataCacheTtlDays);
        Assert.Equal(30, config.ProviderResponseCacheTtlDays);
    }

    [Fact]
    public void Normalize_V9Config_PreservesExistingTransferChoicesAndAddsNewDefaults()
    {
        var config = new PluginConfiguration
        {
            ConfigurationVersion = 9,
            MaxConcurrentDownloads = 5,
            SegmentedDownloadEnabled = true,
            SegmentedDownloadSegments = 7,
        };

        Assert.True(config.Normalize());
        Assert.Equal(11, config.ConfigurationVersion);
        Assert.Equal(5, config.MaxConcurrentDownloads);
        Assert.True(config.SegmentedDownloadEnabled);
        Assert.Equal(7, config.SegmentedDownloadSegments);
        Assert.Equal(25, config.MinimumSegmentedDownloadSizeMiB);
        Assert.Equal(2, config.MaximumConcurrentRangeRequests);
        Assert.Equal(string.Empty, config.DownloadStagingDirectory);
    }

    [Fact]
    public void Serialize_AfterMigration_DoesNotWriteLegacyFlatSettings()
    {
        var config = new PluginConfiguration
        {
            SeriesAudioMaxThemes = 2,
            MovieVideoVolume = 25,
        };
        config.Normalize();

        var serializer = new XmlSerializer(typeof(PluginConfiguration));
        using var writer = new StringWriter();
        serializer.Serialize(writer, config);
        var xml = writer.ToString();

        Assert.Contains("<ConfigurationVersion>11</ConfigurationVersion>", xml);
        Assert.Contains("<Series>", xml);
        Assert.Contains("<Movie>", xml);
        Assert.DoesNotContain("SeriesAudioMaxThemes", xml);
        Assert.DoesNotContain("MovieVideoVolume", xml);
        Assert.DoesNotContain("AllowDelete", xml);
    }

    [Fact]
    public void AllowDelete_LegacyValueIsAlwaysDisabledAndNotSerialized()
    {
        var config = new PluginConfiguration { AllowDelete = true, ConfigurationVersion = 4 };

        Assert.True(config.Normalize());
        Assert.False(config.AllowDelete);

        var serializer = new XmlSerializer(typeof(PluginConfiguration));
        using var writer = new StringWriter();
        serializer.Serialize(writer, config);
        Assert.DoesNotContain("AllowDelete", writer.ToString(), StringComparison.Ordinal);
    }
}

#pragma warning restore CS0618
