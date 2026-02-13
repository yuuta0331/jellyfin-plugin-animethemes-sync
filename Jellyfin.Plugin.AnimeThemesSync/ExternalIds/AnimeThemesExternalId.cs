using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.AnimeThemesSync.ExternalIds
{
    /// <summary>
    /// External ID for AnimeThemes.
    /// </summary>
    public class AnimeThemesExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => "AnimeThemes";

        /// <inheritdoc />
        public string Key => "AnimeThemesSlug";

        /// <inheritdoc />
        public ExternalIdMediaType? Type => ExternalIdMediaType.Series;

        /// <inheritdoc />
        public string UrlFormatString => "https://animethemes.moe/anime/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item)
        {
            return item is Series;
        }
    }
}
