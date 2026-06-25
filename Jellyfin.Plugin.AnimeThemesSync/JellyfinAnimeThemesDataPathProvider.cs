using System.IO;
using AnimeThemesSync.Shared.Interfaces;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.AnimeThemesSync;

/// <summary>
/// Provides the Jellyfin plugin data path.
/// </summary>
public sealed class JellyfinAnimeThemesDataPathProvider : IAnimeThemesDataPathProvider
{
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinAnimeThemesDataPathProvider"/> class.
    /// </summary>
    public JellyfinAnimeThemesDataPathProvider(IApplicationPaths applicationPaths)
    {
        _applicationPaths = applicationPaths;
    }

    /// <inheritdoc />
    public string GetPluginDataDirectory()
    {
        return Path.GetFullPath(Path.Combine(_applicationPaths.PluginConfigurationsPath, "AnimeThemesSync"));
    }
}
