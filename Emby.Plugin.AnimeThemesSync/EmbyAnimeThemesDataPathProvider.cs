using System.IO;
using AnimeThemesSync.Shared.Interfaces;
using MediaBrowser.Common.Configuration;

namespace Emby.Plugin.AnimeThemesSync;

/// <summary>
/// Provides the Emby plugin data path.
/// </summary>
public sealed class EmbyAnimeThemesDataPathProvider : IAnimeThemesDataPathProvider
{
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbyAnimeThemesDataPathProvider"/> class.
    /// </summary>
    public EmbyAnimeThemesDataPathProvider(IApplicationPaths applicationPaths)
    {
        _applicationPaths = applicationPaths;
    }

    /// <inheritdoc />
    public string GetPluginDataDirectory()
    {
        return Path.GetFullPath(Path.Combine(_applicationPaths.PluginConfigurationsPath, "AnimeThemesSync"));
    }
}
