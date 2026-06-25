using AnimeThemesSync.Shared.Interfaces;

namespace Emby.Plugin.AnimeThemesSync;

/// <summary>
/// Identifies Emby storage rows.
/// </summary>
public sealed class EmbyAnimeThemesServerIdentityProvider : IAnimeThemesServerIdentityProvider
{
    /// <inheritdoc />
    public string ServerKind => "Emby";
}
