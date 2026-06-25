namespace AnimeThemesSync.Shared.Interfaces;

/// <summary>
/// Identifies the host server implementation.
/// </summary>
public interface IAnimeThemesServerIdentityProvider
{
    /// <summary>
    /// Gets the stable server kind used in plugin-owned storage.
    /// </summary>
    string ServerKind { get; }
}
