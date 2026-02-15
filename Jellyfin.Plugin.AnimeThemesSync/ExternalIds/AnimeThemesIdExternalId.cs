using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.AnimeThemesSync.ExternalIds;

/// <summary>
/// External ID for AnimeThemes (Numeric ID).
/// </summary>
public class AnimeThemesIdExternalId : IExternalId
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public string ProviderName => "AnimeThemes ID";

    /// <summary>
    /// Gets the provider key.
    /// </summary>
    public string Key => "AnimeThemesId";

    /// <summary>
    /// Gets the external id media type.
    /// </summary>
    public ExternalIdMediaType? Type => ExternalIdMediaType.Series;

    /// <summary>
    /// Gets the url format string.
    /// </summary>
    public string UrlFormatString => string.Empty;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item)
    {
        return item is Series;
    }
}
