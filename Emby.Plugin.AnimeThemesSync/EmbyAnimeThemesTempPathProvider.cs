using AnimeThemesSync.Shared.Interfaces;
using MediaBrowser.Common.Configuration;

namespace Emby.Plugin.AnimeThemesSync;

/// <summary>
/// Provides Emby's configured temporary directory.
/// </summary>
public sealed class EmbyAnimeThemesTempPathProvider : IAnimeThemesTempPathProvider
{
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbyAnimeThemesTempPathProvider"/> class.
    /// </summary>
    public EmbyAnimeThemesTempPathProvider(IApplicationPaths applicationPaths)
    {
        _applicationPaths = applicationPaths;
    }

    /// <inheritdoc />
    public string GetTempDirectory()
    {
        return _applicationPaths.TempDirectory;
    }
}
