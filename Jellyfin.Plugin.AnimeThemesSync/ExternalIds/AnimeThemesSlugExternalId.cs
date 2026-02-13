using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.AnimeThemesSync.ExternalIds
{
    /// <summary>
    /// External ID for AnimeThemes.
    /// </summary>
    public class AnimeThemesSlugExternalId : IExternalId
    {
        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string ProviderName => "AnimeThemes";

        /// <summary>
        /// Gets the provider key.
        /// </summary>
        public string Key => "AnimeThemes";

        /// <summary>
        /// Gets the external id media type.
        /// </summary>
        public ExternalIdMediaType? Type => ExternalIdMediaType.Series;

        /// <summary>
        /// Gets the url format string.
        /// </summary>
        public string UrlFormatString => "https://animethemes.moe/anime/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item)
        {
            return item is Series;
        }
    }
}
