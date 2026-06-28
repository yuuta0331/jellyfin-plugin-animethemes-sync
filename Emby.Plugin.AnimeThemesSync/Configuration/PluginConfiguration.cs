using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Services;
using MediaBrowser.Model.Plugins;

#pragma warning disable CS0618

namespace Emby.Plugin.AnimeThemesSync.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    public const int CurrentConfigurationVersion = 4;

    private int _maxConcurrentDownloads = 1;
    private string _tagSeasonSpring = "Spring";
    private string _tagSeasonSummer = "Summer";
    private string _tagSeasonFall = "Fall";
    private string _tagSeasonWinter = "Winter";
    private string _tagFormat = "{Season} {Year}";
    private string _extrasFileNameFormat = ThemeFilePlanner.DefaultExtrasFileNameFormat;
    private bool _legacyConfigurationLoaded;

    private int _segmentedDownloadSegments = 4;

    private int _seriesAudioMaxThemes = 1;
    private int _seriesAudioVolume = 100;
    private bool _seriesAudioIgnoreEd = true;
    private int _seriesVideoMaxThemes = 1;
    private int _seriesVideoVolume = 100;
    private bool _seriesVideoIgnoreEd = true;
    private int _movieAudioMaxThemes = 1;
    private int _movieAudioVolume = 100;
    private bool _movieAudioIgnoreEd = true;
    private int _movieVideoMaxThemes = 1;
    private int _movieVideoVolume = 100;
    private bool _movieVideoIgnoreEd = true;

    public PluginConfiguration()
    {
        ConfigurationVersion = CurrentConfigurationVersion;
        ThemeDownloadingEnabled = true;
        MaxConcurrentDownloads = 1;
        DownloadTimeoutSeconds = 600;
        SegmentedDownloadEnabled = true;
        SegmentedDownloadSegments = 4;
        AllowAdd = true;
        ForceRedownload = false;
        AllowDelete = false;
        ExtrasEnabled = false;
        ExtrasLinkMode = ExtrasLinkMode.HardLinkWithCopyFallback;
        ExtrasFileSuffix = ExtrasFileSuffix.Other;
        ExtrasFileNameFormat = ThemeFilePlanner.DefaultExtrasFileNameFormat;
        SeasonThemeDownloadsEnabled = true;
        TagsEnabled = true;
        SeasonThemeMappings = [];
        TagLocalization = "None";
        Series = CreateDefaultMediaTypeConfig();
        Movie = CreateDefaultMediaTypeConfig();
    }

    public int ConfigurationVersion { get; set; }

    public bool ThemeDownloadingEnabled { get; set; }

    public int MaxConcurrentDownloads
    {
        get => _maxConcurrentDownloads;
        set => _maxConcurrentDownloads = value < 1 ? 1 : value;
    }

    public int DownloadTimeoutSeconds { get; set; }

    public bool SegmentedDownloadEnabled { get; set; } = true;

    public int SegmentedDownloadSegments
    {
        get => _segmentedDownloadSegments;
        set => _segmentedDownloadSegments = value < 2 ? 2 : value > 8 ? 8 : value;
    }

    public bool AllowAdd { get; set; }

    public bool ForceRedownload { get; set; }

    public bool AllowDelete { get; set; }

    public bool ExtrasEnabled { get; set; }

    public ExtrasLinkMode ExtrasLinkMode { get; set; }

    public ExtrasFileSuffix ExtrasFileSuffix { get; set; }

    public string ExtrasFileNameFormat
    {
        get => _extrasFileNameFormat;
        set => _extrasFileNameFormat = string.IsNullOrWhiteSpace(value)
            ? ThemeFilePlanner.DefaultExtrasFileNameFormat
            : value;
    }

    public bool SeasonThemeDownloadsEnabled { get; set; }

    public bool TagsEnabled { get; set; }

    public List<SeasonThemeMapping> SeasonThemeMappings { get; set; }

    public string TagLocalization { get; set; }

    public string TagSeasonSpring
    {
        get => _tagSeasonSpring;
        set => _tagSeasonSpring = string.IsNullOrWhiteSpace(value) ? "Spring" : value;
    }

    public string TagSeasonSummer
    {
        get => _tagSeasonSummer;
        set => _tagSeasonSummer = string.IsNullOrWhiteSpace(value) ? "Summer" : value;
    }

    public string TagSeasonFall
    {
        get => _tagSeasonFall;
        set => _tagSeasonFall = string.IsNullOrWhiteSpace(value) ? "Fall" : value;
    }

    public string TagSeasonWinter
    {
        get => _tagSeasonWinter;
        set => _tagSeasonWinter = string.IsNullOrWhiteSpace(value) ? "Winter" : value;
    }

    public string TagFormat
    {
        get => _tagFormat;
        set => _tagFormat = string.IsNullOrWhiteSpace(value) ? "{Season} {Year}" : value;
    }

    public MediaTypeConfig Series { get; set; }

    public MediaTypeConfig Movie { get; set; }

    [Obsolete("Use Series.Audio.MaxThemes.")]
    [JsonIgnore]
    public int SeriesAudioMaxThemes
    {
        get => _seriesAudioMaxThemes;
        set
        {
            _legacyConfigurationLoaded = true;
            _seriesAudioMaxThemes = value;
        }
    }

    [Obsolete("Use Series.Audio.Volume.")]
    [JsonIgnore]
    public int SeriesAudioVolume
    {
        get => _seriesAudioVolume;
        set
        {
            _legacyConfigurationLoaded = true;
            _seriesAudioVolume = Math.Clamp(value, 0, 100);
        }
    }

    [Obsolete("Use Series.Audio.IgnoreOp.")]
    [JsonIgnore]
    public bool SeriesAudioIgnoreOp { get; set; }

    [Obsolete("Use Series.Audio.IgnoreEd.")]
    [JsonIgnore]
    public bool SeriesAudioIgnoreEd
    {
        get => _seriesAudioIgnoreEd;
        set
        {
            _legacyConfigurationLoaded = true;
            _seriesAudioIgnoreEd = value;
        }
    }

    [Obsolete("Use Series.Audio.IgnoreOverlaps.")]
    [JsonIgnore]
    public bool SeriesAudioIgnoreOverlaps { get; set; }

    [Obsolete("Use Series.Audio.IgnoreCredits.")]
    [JsonIgnore]
    public bool SeriesAudioIgnoreCredits { get; set; }

    [Obsolete("Use Series.Video.MaxThemes.")]
    [JsonIgnore]
    public int SeriesVideoMaxThemes
    {
        get => _seriesVideoMaxThemes;
        set
        {
            _legacyConfigurationLoaded = true;
            _seriesVideoMaxThemes = value;
        }
    }

    [Obsolete("Use Series.Video.Volume.")]
    [JsonIgnore]
    public int SeriesVideoVolume
    {
        get => _seriesVideoVolume;
        set
        {
            _legacyConfigurationLoaded = true;
            _seriesVideoVolume = Math.Clamp(value, 0, 100);
        }
    }

    [Obsolete("Use Series.Video.IgnoreOp.")]
    [JsonIgnore]
    public bool SeriesVideoIgnoreOp { get; set; }

    [Obsolete("Use Series.Video.IgnoreEd.")]
    [JsonIgnore]
    public bool SeriesVideoIgnoreEd
    {
        get => _seriesVideoIgnoreEd;
        set
        {
            _legacyConfigurationLoaded = true;
            _seriesVideoIgnoreEd = value;
        }
    }

    [Obsolete("Use Series.Video.IgnoreOverlaps.")]
    [JsonIgnore]
    public bool SeriesVideoIgnoreOverlaps { get; set; }

    [Obsolete("Use Series.Video.IgnoreCredits.")]
    [JsonIgnore]
    public bool SeriesVideoIgnoreCredits { get; set; }

    [Obsolete("Use Movie.Audio.MaxThemes.")]
    [JsonIgnore]
    public int MovieAudioMaxThemes
    {
        get => _movieAudioMaxThemes;
        set
        {
            _legacyConfigurationLoaded = true;
            _movieAudioMaxThemes = value;
        }
    }

    [Obsolete("Use Movie.Audio.Volume.")]
    [JsonIgnore]
    public int MovieAudioVolume
    {
        get => _movieAudioVolume;
        set
        {
            _legacyConfigurationLoaded = true;
            _movieAudioVolume = Math.Clamp(value, 0, 100);
        }
    }

    [Obsolete("Use Movie.Audio.IgnoreOp.")]
    [JsonIgnore]
    public bool MovieAudioIgnoreOp { get; set; }

    [Obsolete("Use Movie.Audio.IgnoreEd.")]
    [JsonIgnore]
    public bool MovieAudioIgnoreEd
    {
        get => _movieAudioIgnoreEd;
        set
        {
            _legacyConfigurationLoaded = true;
            _movieAudioIgnoreEd = value;
        }
    }

    [Obsolete("Use Movie.Audio.IgnoreOverlaps.")]
    [JsonIgnore]
    public bool MovieAudioIgnoreOverlaps { get; set; }

    [Obsolete("Use Movie.Audio.IgnoreCredits.")]
    [JsonIgnore]
    public bool MovieAudioIgnoreCredits { get; set; }

    [Obsolete("Use Movie.Video.MaxThemes.")]
    [JsonIgnore]
    public int MovieVideoMaxThemes
    {
        get => _movieVideoMaxThemes;
        set
        {
            _legacyConfigurationLoaded = true;
            _movieVideoMaxThemes = value;
        }
    }

    [Obsolete("Use Movie.Video.Volume.")]
    [JsonIgnore]
    public int MovieVideoVolume
    {
        get => _movieVideoVolume;
        set
        {
            _legacyConfigurationLoaded = true;
            _movieVideoVolume = Math.Clamp(value, 0, 100);
        }
    }

    [Obsolete("Use Movie.Video.IgnoreOp.")]
    [JsonIgnore]
    public bool MovieVideoIgnoreOp { get; set; }

    [Obsolete("Use Movie.Video.IgnoreEd.")]
    [JsonIgnore]
    public bool MovieVideoIgnoreEd
    {
        get => _movieVideoIgnoreEd;
        set
        {
            _legacyConfigurationLoaded = true;
            _movieVideoIgnoreEd = value;
        }
    }

    [Obsolete("Use Movie.Video.IgnoreOverlaps.")]
    [JsonIgnore]
    public bool MovieVideoIgnoreOverlaps { get; set; }

    [Obsolete("Use Movie.Video.IgnoreCredits.")]
    [JsonIgnore]
    public bool MovieVideoIgnoreCredits { get; set; }

    public bool Normalize()
    {
        var changed = false;
        if (_legacyConfigurationLoaded || ConfigurationVersion < 2)
        {
            Series = new MediaTypeConfig
            {
                Audio = new ThemeConfig
                {
                    MaxThemes = _seriesAudioMaxThemes,
                    Volume = _seriesAudioVolume,
                    IgnoreOp = SeriesAudioIgnoreOp,
                    IgnoreEd = _seriesAudioIgnoreEd,
                    IgnoreOverlaps = SeriesAudioIgnoreOverlaps,
                    IgnoreCredits = SeriesAudioIgnoreCredits,
                },
                Video = new ThemeConfig
                {
                    MaxThemes = _seriesVideoMaxThemes,
                    Volume = _seriesVideoVolume,
                    IgnoreOp = SeriesVideoIgnoreOp,
                    IgnoreEd = _seriesVideoIgnoreEd,
                    IgnoreOverlaps = SeriesVideoIgnoreOverlaps,
                    IgnoreCredits = SeriesVideoIgnoreCredits,
                },
            };
            Movie = new MediaTypeConfig
            {
                Audio = new ThemeConfig
                {
                    MaxThemes = _movieAudioMaxThemes,
                    Volume = _movieAudioVolume,
                    IgnoreOp = MovieAudioIgnoreOp,
                    IgnoreEd = _movieAudioIgnoreEd,
                    IgnoreOverlaps = MovieAudioIgnoreOverlaps,
                    IgnoreCredits = MovieAudioIgnoreCredits,
                },
                Video = new ThemeConfig
                {
                    MaxThemes = _movieVideoMaxThemes,
                    Volume = _movieVideoVolume,
                    IgnoreOp = MovieVideoIgnoreOp,
                    IgnoreEd = _movieVideoIgnoreEd,
                    IgnoreOverlaps = MovieVideoIgnoreOverlaps,
                    IgnoreCredits = MovieVideoIgnoreCredits,
                },
            };
            _legacyConfigurationLoaded = false;
            changed = true;
        }

        if (Series == null)
        {
            Series = CreateDefaultMediaTypeConfig();
            changed = true;
        }
        else
        {
            changed |= NormalizeMediaConfig(Series);
        }

        if (Movie == null)
        {
            Movie = CreateDefaultMediaTypeConfig();
            changed = true;
        }
        else
        {
            changed |= NormalizeMediaConfig(Movie);
        }

        if (SeasonThemeMappings == null)
        {
            SeasonThemeMappings = [];
            changed = true;
        }

        if (!Enum.IsDefined(typeof(ExtrasFileSuffix), ExtrasFileSuffix))
        {
            ExtrasFileSuffix = ExtrasFileSuffix.Other;
            changed = true;
        }

        if (ConfigurationVersion != CurrentConfigurationVersion)
        {
            ConfigurationVersion = CurrentConfigurationVersion;
            changed = true;
        }

        return changed;
    }

    public bool ShouldSerializeSeriesAudioMaxThemes() => false;

    public bool ShouldSerializeSeriesAudioVolume() => false;

    public bool ShouldSerializeSeriesAudioIgnoreOp() => false;

    public bool ShouldSerializeSeriesAudioIgnoreEd() => false;

    public bool ShouldSerializeSeriesAudioIgnoreOverlaps() => false;

    public bool ShouldSerializeSeriesAudioIgnoreCredits() => false;

    public bool ShouldSerializeSeriesVideoMaxThemes() => false;

    public bool ShouldSerializeSeriesVideoVolume() => false;

    public bool ShouldSerializeSeriesVideoIgnoreOp() => false;

    public bool ShouldSerializeSeriesVideoIgnoreEd() => false;

    public bool ShouldSerializeSeriesVideoIgnoreOverlaps() => false;

    public bool ShouldSerializeSeriesVideoIgnoreCredits() => false;

    public bool ShouldSerializeMovieAudioMaxThemes() => false;

    public bool ShouldSerializeMovieAudioVolume() => false;

    public bool ShouldSerializeMovieAudioIgnoreOp() => false;

    public bool ShouldSerializeMovieAudioIgnoreEd() => false;

    public bool ShouldSerializeMovieAudioIgnoreOverlaps() => false;

    public bool ShouldSerializeMovieAudioIgnoreCredits() => false;

    public bool ShouldSerializeMovieVideoMaxThemes() => false;

    public bool ShouldSerializeMovieVideoVolume() => false;

    public bool ShouldSerializeMovieVideoIgnoreOp() => false;

    public bool ShouldSerializeMovieVideoIgnoreEd() => false;

    public bool ShouldSerializeMovieVideoIgnoreOverlaps() => false;

    public bool ShouldSerializeMovieVideoIgnoreCredits() => false;

    private static MediaTypeConfig CreateDefaultMediaTypeConfig()
    {
        return new MediaTypeConfig
        {
            Audio = new ThemeConfig(),
            Video = new ThemeConfig(),
        };
    }

    private static bool NormalizeMediaConfig(MediaTypeConfig config)
    {
        var changed = false;
        if (config.Audio == null)
        {
            config.Audio = new ThemeConfig();
            changed = true;
        }
        else
        {
            changed |= NormalizeThemeConfig(config.Audio);
        }

        if (config.Video == null)
        {
            config.Video = new ThemeConfig();
            changed = true;
        }
        else
        {
            changed |= NormalizeThemeConfig(config.Video);
        }

        return changed;
    }

    private static bool NormalizeThemeConfig(ThemeConfig config)
    {
        var originalMax = config.MaxThemes;
        var originalVolume = config.Volume;
        config.MaxThemes = Math.Max(0, config.MaxThemes);
        config.Volume = Math.Clamp(config.Volume, 0, 100);
        return originalMax != config.MaxThemes || originalVolume != config.Volume;
    }
}

#pragma warning restore CS0618
