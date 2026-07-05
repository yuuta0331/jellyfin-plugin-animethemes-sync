using AnimeThemesSync.Shared.Interfaces;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.AnimeThemesSync;

/// <summary>
/// Provides Jellyfin's configured temporary directory.
/// </summary>
public sealed class JellyfinAnimeThemesTempPathProvider : IAnimeThemesTempPathProvider
{
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinAnimeThemesTempPathProvider"/> class.
    /// </summary>
    public JellyfinAnimeThemesTempPathProvider(IApplicationPaths applicationPaths)
    {
        _applicationPaths = applicationPaths;
    }

    /// <inheritdoc />
    public string GetTempDirectory()
    {
        return _applicationPaths.TempDirectory;
    }
}
