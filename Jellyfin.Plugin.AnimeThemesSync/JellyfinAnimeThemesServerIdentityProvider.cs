using AnimeThemesSync.Shared.Interfaces;

namespace Jellyfin.Plugin.AnimeThemesSync;

/// <summary>
/// Identifies Jellyfin storage rows.
/// </summary>
public sealed class JellyfinAnimeThemesServerIdentityProvider : IAnimeThemesServerIdentityProvider
{
    /// <inheritdoc />
    public string ServerKind => "Jellyfin";
}
