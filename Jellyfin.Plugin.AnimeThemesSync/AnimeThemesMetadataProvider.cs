using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AnimeThemesSync.Services;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync
{
    /// <summary>
    /// Metadata provider for AnimeThemes.
    /// </summary>
    public class AnimeThemesMetadataProvider : IRemoteMetadataProvider<Series, SeriesInfo>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AnimeThemesMetadataProvider> _logger;
        private readonly AniListService _aniListService;
        private readonly AnimeThemesService _animeThemesService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimeThemesMetadataProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        public AnimeThemesMetadataProvider(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory;
            _logger = loggerFactory.CreateLogger<AnimeThemesMetadataProvider>();

            _aniListService = new AniListService(httpClientFactory, loggerFactory.CreateLogger<AniListService>());
            _animeThemesService = new AnimeThemesService(httpClientFactory, loggerFactory.CreateLogger<AnimeThemesService>());
        }

        /// <inheritdoc />
        public string Name => "AnimeThemes Sync";

        /// <inheritdoc />
        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>
            {
                Item = new Series()
            };

            var seriesName = info.Name;
            var year = info.Year;

            _logger.LogInformation("Resolving metadata for '{SeriesName}' ({Year})", seriesName, year);

            // 1. Direct ID Lookup
            int? aniListId = null;
            int? malId = null;

            if (info.ProviderIds.TryGetValue("AniList", out var aniListIdStr) && int.TryParse(aniListIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aid))
            {
                aniListId = aid;
            }

            if (info.ProviderIds.TryGetValue("MyAnimeList", out var malIdStr) && int.TryParse(malIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mid))
            {
                malId = mid;
            }

            // 2. High-Precision Metadata Search (if IDs missing)
            if (aniListId == null && malId == null)
            {
                _logger.LogInformation("No external IDs found. searching AniList for '{SeriesName}'...", seriesName);
                (aniListId, malId) = await _aniListService.SearchAnime(seriesName, year, cancellationToken).ConfigureAwait(false);
            }

            if (aniListId == null && malId == null)
            {
                _logger.LogWarning("Could not resolve any IDs for '{SeriesName}'. Skipping AnimeThemes lookup.", seriesName);
                return result; // return empty/basic result, we failed to enhance
            }

            // 3. AnimeThemes Lookup
            Services.AnimeThemesService.AnimeThemesAnime? anime = null;

            // Try via AniList ID first
            if (aniListId.HasValue)
            {
                anime = await _animeThemesService.GetAnimeByExternalId("anilist", aniListId.Value, cancellationToken).ConfigureAwait(false);
            }

            // Try via MAL ID if missed
            if (anime == null && malId.HasValue)
            {
                anime = await _animeThemesService.GetAnimeByExternalId("myanimelist", malId.Value, cancellationToken).ConfigureAwait(false);
            }

            // If not found by IDs, and we have a name, try searching by name (Tier 2 Fallback)
            // This handles cases where the provided IDs are incorrect or don't exist in AnimeThemes
            if (anime == null)
            {
                _logger.LogInformation("ID lookup failed or IDs missing. Falling back to name search for '{SeriesName}'.", seriesName);

                // Search AniList
                (var newAniListId, var newMalId) = await _aniListService.SearchAnime(seriesName, year, cancellationToken).ConfigureAwait(false);

                if (newAniListId.HasValue || newMalId.HasValue)
                {
                    // Try lookup again with new IDs
                    if (newAniListId.HasValue)
                    {
                        anime = await _animeThemesService.GetAnimeByExternalId("anilist", newAniListId.Value, cancellationToken).ConfigureAwait(false);
                    }

                    if (anime == null && newMalId.HasValue)
                    {
                        anime = await _animeThemesService.GetAnimeByExternalId("myanimelist", newMalId.Value, cancellationToken).ConfigureAwait(false);
                    }

                    // If found, update the IDs to the correct ones
                    if (anime != null)
                    {
                        if (newAniListId.HasValue)
                        {
                            aniListId = newAniListId;
                        }

                        if (newMalId.HasValue)
                        {
                            malId = newMalId;
                        }

                        _logger.LogInformation("Fallback search successful. Found AnimeThemes entry via new IDs: AniList:{AniListId}, MAL:{MalId}", aniListId, malId);
                    }
                }
            }

            if (anime == null)
            {
                _logger.LogWarning("AnimeThemes has no record for AniList:{AniListId} / MAL:{MalId} even after fallback search.", aniListId, malId);
                // We still save the IDs to the item if we found them (even if AT failed, valid AniList ID might exist)
                // But if we came from fallback, we should save the NEW IDs.
                if (aniListId.HasValue)
                {
                    result.Item.SetProviderId("AniList", aniListId.Value.ToString(CultureInfo.InvariantCulture));
                }

                if (malId.HasValue)
                {
                    result.Item.SetProviderId("MyAnimeList", malId.Value.ToString(CultureInfo.InvariantCulture));
                }

                result.HasMetadata = true;
                return result;
            }

            _logger.LogInformation("Found AnimeThemes entry: {Slug} ({Year} {Season})", anime.Slug, anime.Year, anime.Season);

            // Populate Metadata
            if (aniListId.HasValue)
            {
                result.Item.SetProviderId("AniList", aniListId.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (malId.HasValue)
            {
                result.Item.SetProviderId("MyAnimeList", malId.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrEmpty(anime.Slug))
            {
                // Store the Slug for the Web Link (IExternalId)
                result.Item.SetProviderId("AnimeThemesSlug", anime.Slug);

                // Store the Integer ID for internal API logic (Immutable)
                result.Item.SetProviderId("AnimeThemesId", anime.Id.ToString(CultureInfo.InvariantCulture));
            }

            // Tagging: Year and Season
            if (anime.Year.HasValue)
            {
                AddTag(result.Item, anime.Year.Value.ToString(CultureInfo.InvariantCulture));

                if (!string.IsNullOrEmpty(anime.Season))
                {
                    // "Winter 2024"
                    // Capitalize first letter just in case
                    var season = char.ToUpper(anime.Season[0], CultureInfo.InvariantCulture) + anime.Season.Substring(1);
                    AddTag(result.Item, $"{season} {anime.Year.Value.ToString(CultureInfo.InvariantCulture)}");
                }
            }

            result.HasMetadata = true;
            return result;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            // We can implement search here to allow manual identification in Jellyfin UI
            // Reuse AniList service
            var results = new List<RemoteSearchResult>();

            // Search AniList
            var (aniListId, malId) = await _aniListService.SearchAnime(searchInfo.Name, searchInfo.Year, cancellationToken).ConfigureAwait(false);

            if (aniListId.HasValue || malId.HasValue)
            {
                var res = new RemoteSearchResult
                {
                    Name = searchInfo.Name,
                    ProductionYear = searchInfo.Year,
                    SearchProviderName = Name
                };
                if (aniListId.HasValue)
                {
                    res.SetProviderId("AniList", aniListId.Value.ToString(CultureInfo.InvariantCulture));
                }

                if (malId.HasValue)
                {
                    res.SetProviderId("MyAnimeList", malId.Value.ToString(CultureInfo.InvariantCulture));
                }

                results.Add(res);
            }

            return results;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient().GetAsync(new Uri(url), cancellationToken);
        }

        private static void AddTag(Series item, string tag)
        {
            if (item.Tags == null)
            {
                item.Tags = new[] { tag };
            }
            else if (!System.Linq.Enumerable.Contains(item.Tags, tag, StringComparer.OrdinalIgnoreCase))
            {
                var list = System.Linq.Enumerable.ToList(item.Tags);
                list.Add(tag);
                item.Tags = list.ToArray();
            }
        }
    }
}
