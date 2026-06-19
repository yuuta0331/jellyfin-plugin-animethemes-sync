using System.IO;
using System.Xml.Serialization;
using Jellyfin.Plugin.AnimeThemesSync.Configuration;

#pragma warning disable CS0618

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class PluginConfigurationMigrationTests
{
    [Fact]
    public void Constructor_CreatesV2Defaults()
    {
        var config = new PluginConfiguration();

        Assert.Equal(PluginConfiguration.CurrentConfigurationVersion, config.ConfigurationVersion);
        Assert.Equal(1, config.Series.Audio.MaxThemes);
        Assert.Equal(100, config.Series.Audio.Volume);
        Assert.True(config.Series.Audio.IgnoreEd);
        Assert.Equal(1, config.Movie.Video.MaxThemes);
        Assert.Equal(100, config.Movie.Video.Volume);
        Assert.True(config.Movie.Video.IgnoreEd);
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

        Assert.Contains("<ConfigurationVersion>2</ConfigurationVersion>", xml);
        Assert.Contains("<Series>", xml);
        Assert.Contains("<Movie>", xml);
        Assert.DoesNotContain("SeriesAudioMaxThemes", xml);
        Assert.DoesNotContain("MovieVideoVolume", xml);
    }
}

#pragma warning restore CS0618
