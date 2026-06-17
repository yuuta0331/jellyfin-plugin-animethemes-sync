using AnimeThemesSync.Shared;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.AnimeThemesSync.ExternalIds;

/// <summary>
/// External ID for AnimeThemes (Numeric ID).
/// </summary>
public class AnimeThemesIdExternalId : IExternalId
{
    /// <inheritdoc />
    public string Name => "AnimeThemes ID";

    /// <inheritdoc />
    public string Key => Constants.AnimeThemesNumericProviderId;

    /// <inheritdoc />
    public string UrlFormatString => string.Empty;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item)
    {
        return item is Series || item is Movie;
    }
}
