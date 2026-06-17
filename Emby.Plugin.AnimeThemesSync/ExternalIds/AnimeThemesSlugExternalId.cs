using AnimeThemesSync.Shared;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.AnimeThemesSync.ExternalIds;

/// <summary>
/// External ID for AnimeThemes (Slug-based URL).
/// </summary>
public class AnimeThemesSlugExternalId : IExternalId
{
    /// <inheritdoc />
    public string Name => "AnimeThemes Slug";

    /// <inheritdoc />
    public string Key => Constants.AnimeThemesProviderId;

    /// <inheritdoc />
    public string UrlFormatString => $"{Constants.AnimeThemesWebUrl}/anime/{{0}}";

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item)
    {
        return item is Series || item is Movie;
    }
}
