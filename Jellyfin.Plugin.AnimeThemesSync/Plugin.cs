using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AnimeThemesSync.Shared;
using AnimeThemesSync.Shared.Configuration;
using Jellyfin.Plugin.AnimeThemesSync.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AnimeThemesSync;

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
        Console.OutputEncoding = Encoding.UTF8;
        if (Configuration.Normalize())
        {
            UpdateConfiguration(Configuration);
        }
    }

    /// <inheritdoc />
    public override string Name => Constants.PluginName;

    /// <inheritdoc />
    public override Guid Id => Guid.Parse(Constants.PluginGuid);

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var configPageName = "animethemessync" + Constants.UiAssetVersion;
        var browserPageName = "animethemessyncbrowser" + Constants.UiAssetVersion;

        return
        [
            new PluginPageInfo
            {
                Name = configPageName,
                DisplayName = Name,
                EnableInMainMenu = false,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = browserPageName,
                DisplayName = "AnimeThemes Browser",
                EnableInMainMenu = true,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.browserPage.html", GetType().Namespace)
            }
        ];
    }
}
