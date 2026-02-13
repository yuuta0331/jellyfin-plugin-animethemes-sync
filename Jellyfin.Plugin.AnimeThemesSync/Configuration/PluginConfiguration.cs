using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AnimeThemesSync.Configuration;

/// <summary>
/// The configuration options.
/// </summary>

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        ThemeDownloadingEnabled = true;
    }

    /// <summary>
    /// Gets or sets a value indicating whether theme downloading is enabled.
    /// </summary>
    public bool ThemeDownloadingEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether season/year tags are enabled.
    /// </summary>
    public bool TagsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the tag localization setting (e.g., "None", "Japanese").
    /// </summary>
    public string TagLocalization { get; set; } = "None";
}
