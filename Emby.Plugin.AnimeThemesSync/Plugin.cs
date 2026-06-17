using AnimeThemesSync.Shared;
using Emby.Plugin.AnimeThemesSync.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;

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
                EnableInMainMenu = true,
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
