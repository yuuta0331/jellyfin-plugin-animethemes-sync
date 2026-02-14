using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AnimeThemesSync.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    private int _maxConcurrentDownloads = 1;

    private string _tagSeasonSpring = "Spring";
    private string _tagSeasonSummer = "Summer";
    private string _tagSeasonFall = "Fall";
    private string _tagSeasonWinter = "Winter";
    private string _tagFormat = "{Season} {Year}";
    private int _seriesAudioVolume = 100;
    private int _seriesVideoVolume = 100;
    private int _movieAudioVolume = 100;
    private int _movieVideoVolume = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        ThemeDownloadingEnabled = true;
        MaxConcurrentDownloads = 1;
        DownloadTimeoutSeconds = 600;
        AllowAdd = true;
        ForceRedownload = false;
        AllowDelete = false;
        TagsEnabled = true;
        TagLocalization = "None";
        TagSeasonSpring = "Spring";
        TagSeasonSummer = "Summer";
        TagSeasonFall = "Fall";
        TagSeasonWinter = "Winter";
        TagFormat = "{Season} {Year}";

        // Series Audio defaults
        SeriesAudioMaxThemes = 1;
        SeriesAudioVolume = 100;
        SeriesAudioIgnoreOp = false;
        SeriesAudioIgnoreEd = true;
        SeriesAudioIgnoreOverlaps = false;
        SeriesAudioIgnoreCredits = false;

        // Series Video defaults
        SeriesVideoMaxThemes = 1;
        SeriesVideoVolume = 100;
        SeriesVideoIgnoreOp = false;
        SeriesVideoIgnoreEd = true;
        SeriesVideoIgnoreOverlaps = false;
        SeriesVideoIgnoreCredits = false;

        // Movie Audio defaults
        MovieAudioMaxThemes = 1;
        MovieAudioVolume = 100;
        MovieAudioIgnoreOp = false;
        MovieAudioIgnoreEd = true;
        MovieAudioIgnoreOverlaps = false;
        MovieAudioIgnoreCredits = false;

        // Movie Video defaults
        MovieVideoMaxThemes = 1;
        MovieVideoVolume = 100;
        MovieVideoIgnoreOp = false;
        MovieVideoIgnoreEd = true;
        MovieVideoIgnoreOverlaps = false;
        MovieVideoIgnoreCredits = false;
    }

    // Global Settings
    public bool ThemeDownloadingEnabled { get; set; }

    public int MaxConcurrentDownloads
    {
        get => _maxConcurrentDownloads;
        set => _maxConcurrentDownloads = value < 1 ? 1 : value;
    }

    public int DownloadTimeoutSeconds { get; set; }

    public bool AllowAdd { get; set; }

    public bool ForceRedownload { get; set; }

    public bool AllowDelete { get; set; }

    public bool TagsEnabled { get; set; }

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

    // Series Audio Settings
    public int SeriesAudioMaxThemes { get; set; }

    public int SeriesAudioVolume
    {
        get => _seriesAudioVolume;
        set => _seriesAudioVolume = Math.Clamp(value, 0, 100);
    }

    public bool SeriesAudioIgnoreOp { get; set; }

    public bool SeriesAudioIgnoreEd { get; set; }

    public bool SeriesAudioIgnoreOverlaps { get; set; }

    public bool SeriesAudioIgnoreCredits { get; set; }

    // Series Video Settings
    public int SeriesVideoMaxThemes { get; set; }

    public int SeriesVideoVolume
    {
        get => _seriesVideoVolume;
        set => _seriesVideoVolume = Math.Clamp(value, 0, 100);
    }

    public bool SeriesVideoIgnoreOp { get; set; }

    public bool SeriesVideoIgnoreEd { get; set; }

    public bool SeriesVideoIgnoreOverlaps { get; set; }

    public bool SeriesVideoIgnoreCredits { get; set; }

    // Movie Audio Settings
    public int MovieAudioMaxThemes { get; set; }

    public int MovieAudioVolume
    {
        get => _movieAudioVolume;
        set => _movieAudioVolume = Math.Clamp(value, 0, 100);
    }

    public bool MovieAudioIgnoreOp { get; set; }

    public bool MovieAudioIgnoreEd { get; set; }

    public bool MovieAudioIgnoreOverlaps { get; set; }

    public bool MovieAudioIgnoreCredits { get; set; }

    // Movie Video Settings
    public int MovieVideoMaxThemes { get; set; }

    public int MovieVideoVolume
    {
        get => _movieVideoVolume;
        set => _movieVideoVolume = Math.Clamp(value, 0, 100);
    }

    public bool MovieVideoIgnoreOp { get; set; }

    public bool MovieVideoIgnoreEd { get; set; }

    public bool MovieVideoIgnoreOverlaps { get; set; }

    public bool MovieVideoIgnoreCredits { get; set; }
}
