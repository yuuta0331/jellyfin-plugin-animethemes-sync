namespace AnimeThemesSync.Shared.Interfaces;

/// <summary>
/// Provides the media server's configured temporary directory.
/// </summary>
public interface IAnimeThemesTempPathProvider
{
    /// <summary>
    /// Gets the host-managed temporary directory.
    /// </summary>
    string GetTempDirectory();
}
