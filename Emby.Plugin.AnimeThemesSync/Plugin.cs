using AnimeThemesSync.Shared;
using Emby.Plugin.AnimeThemesSync.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;

namespace Emby.Plugin.AnimeThemesSync;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        if (Configuration.Normalize())
        {
            UpdateConfiguration(Configuration);
        }
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => Constants.PluginName;

    /// <inheritdoc />
    public override Guid Id => Guid.Parse(Constants.PluginGuid);

    /// <inheritdoc />
    public override string Description => "Syncs anime themes from AnimeThemes.moe to Emby.";

    /// <summary>
    /// Gets the configuration file name. Emby 4.9 does not call <c>SetAttributes</c>
    /// until after the constructor completes, so the default implementation (which
    /// derives the name from <see cref="BasePlugin{T}.AssemblyFileName"/>) returns
    /// <c>null</c> during construction and causes <see cref="ArgumentNullException"/>
    /// in <see cref="System.IO.Path.Combine(string, string)"/>. Returning a fixed
    /// name avoids that dependency.
    /// </summary>
    public override string ConfigurationFileName => "Emby.Plugin.AnimeThemesSync.xml";

    /// <summary>
    /// Saves the configuration to disk. Emby 4.9 does not call
    /// <c>SetStartupInfo</c> until after the constructor completes, so the base
    /// implementation (which relies on <c>_directoryCreateFn</c>) throws
    /// <see cref="NullReferenceException"/> during construction. This override
    /// creates the directory directly, mirroring the Jellyfin implementation.
    /// </summary>
    public override void SaveConfiguration()
    {
        var dir = Path.GetDirectoryName(ConfigurationFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        XmlSerializer.SerializeToFile(Configuration, ConfigurationFilePath);
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var type = GetType();
        var configPageName = "animethemessync" + Constants.UiAssetVersion;
        var browserPageName = "animethemessyncbrowser" + Constants.UiAssetVersion;
        var configScriptName = "animethemessyncconfigjs" + Constants.UiAssetVersion;
        var browserScriptName = "animethemessyncbrowserjs" + Constants.UiAssetVersion;

        return
        [
            new PluginPageInfo
            {
                Name = configPageName,
                DisplayName = Name,
                EmbeddedResourcePath = type.Namespace + ".Configuration.configPage.html",
                EnableInMainMenu = false,
                MenuSection = "server",
                MenuIcon = "music_note"
            },
            new PluginPageInfo
            {
                Name = browserPageName,
                DisplayName = "AnimeThemes Browser",
                EmbeddedResourcePath = type.Namespace + ".Configuration.browserPage.html",
                EnableInMainMenu = true,
                MenuSection = "server",
                MenuIcon = "video_library"
            },
            new PluginPageInfo
            {
                Name = configScriptName,
                EmbeddedResourcePath = type.Namespace + ".Configuration.configPage.js"
            },
            new PluginPageInfo
            {
                Name = browserScriptName,
                EmbeddedResourcePath = type.Namespace + ".Configuration.browserPage.js"
            }
        ];
    }
}
