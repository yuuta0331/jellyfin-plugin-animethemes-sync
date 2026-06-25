namespace AnimeThemesSync.Shared.Interfaces;

/// <summary>
/// Provides the plugin-owned data directory for AnimeThemes Sync.
/// </summary>
public interface IAnimeThemesDataPathProvider
{
    /// <summary>
    /// Gets an absolute directory path where the plugin can store its own data.
    /// </summary>
    /// <returns>The plugin data directory.</returns>
    string GetPluginDataDirectory();
}
