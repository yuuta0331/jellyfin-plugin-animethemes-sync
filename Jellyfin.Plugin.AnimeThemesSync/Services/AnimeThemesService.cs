using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync.Services
{
    /// <summary>
    /// Service for searching anime on AnimeThemes.
    /// </summary>
    internal sealed class AnimeThemesService
    {
        private const string BaseUrl = "https://api.animethemes.moe";
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AnimeThemesService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimeThemesService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public AnimeThemesService(IHttpClientFactory httpClientFactory, ILogger<AnimeThemesService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Gets an anime by its external ID (e.g. MAL, AniList).
        /// </summary>
        /// <param name="site">The site name (e.g., "anilist", "myanimelist").</param>
        /// <param name="externalId">The external ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The found anime or null.</returns>
        public async Task<AnimeThemesAnime?> GetAnimeByExternalId(string site, int externalId, CancellationToken cancellationToken)
        {
            // Use the /resource endpoint to find the anime via external ID
            // GET /resource?filter[site]={site}&filter[external_id]={externalId}&include=anime
            var url = $"{BaseUrl}/resource?filter[site]={site}&filter[external_id]={externalId}&include=anime";

            var resourceResponse = await GetResourceFromUrl(url, cancellationToken).ConfigureAwait(false);
            var resource = resourceResponse?.Resources?.FirstOrDefault();

            if (resource?.Anime == null || resource.Anime.Count == 0)
            {
                _logger.LogWarning("No AnimeThemes resource found for {Site}:{Id}", site, externalId);
                return null;
            }

            // Since 'anime' is a list in the API response, take the first one
            return resource.Anime[0];
        }

        /// <summary>
        /// Gets an anime by its slug.
        /// </summary>
        /// <param name="slug">The anime slug.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The found anime or null.</returns>
        public async Task<AnimeThemesAnime?> GetAnimeBySlug(string slug, CancellationToken cancellationToken)
        {
            var url = $"{BaseUrl}/anime/{slug}?include=images,resources,animethemes.animethemeentries.videos";
            return await GetAnimeFromUrl(url, cancellationToken).ConfigureAwait(false);
        }

        private async Task<AnimeThemesResourceResponse?> GetResourceFromUrl(string url, CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("AnimeThemes");
                var response = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AnimeThemes API returned {StatusCode} for URL: {Url}", response.StatusCode, url);
                    return null;
                }

                return await JsonSerializer.DeserializeAsync<AnimeThemesResourceResponse>(
                    await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                    _jsonOptions,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching resource from AnimeThemes: {Url}", url);
                return null;
            }
        }

        private async Task<AnimeThemesAnime?> GetAnimeFromUrl(string url, CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("AnimeThemes");
                var response = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AnimeThemes API returned {StatusCode} for URL: {Url}", response.StatusCode, url);
                    return null;
                }

                // Use JsonElement to inspect structure
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), default, cancellationToken).ConfigureAwait(false);
                var root = doc.RootElement;

                if (root.TryGetProperty("anime", out var animeProp))
                {
                    if (animeProp.ValueKind == JsonValueKind.Array)
                    {
                        var list = JsonSerializer.Deserialize<List<AnimeThemesAnime>>(animeProp.GetRawText(), _jsonOptions);
                        return list?.FirstOrDefault();
                    }
                    else if (animeProp.ValueKind == JsonValueKind.Object)
                    {
                        return JsonSerializer.Deserialize<AnimeThemesAnime>(animeProp.GetRawText(), _jsonOptions);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching data from AnimeThemes: {Url}", url);
                return null;
            }
        }

        internal sealed class AnimeThemesResourceResponse
        {
            [JsonPropertyName("resources")]
            public List<AnimeThemesResource>? Resources { get; set; }
        }

        internal sealed class AnimeThemesResponse
        {
            [JsonPropertyName("anime")]
            public List<AnimeThemesAnime>? Anime { get; set; }
        }

        internal sealed class AnimeThemesAnime
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("slug")]
            public string? Slug { get; set; }

            [JsonPropertyName("year")]
            public int? Year { get; set; }

            [JsonPropertyName("season")]
            public string? Season { get; set; }

            [JsonPropertyName("resources")]
            public List<AnimeThemesResource>? Resources { get; set; }

            [JsonPropertyName("animethemes")]
            public List<AnimeThemesTheme>? AnimeThemes { get; set; }
        }

        internal sealed class AnimeThemesResource
        {
            [JsonPropertyName("site")]
            public string? Site { get; set; }

            [JsonPropertyName("external_id")]
            public int ExternalId { get; set; }

            [JsonPropertyName("anime")]
            public List<AnimeThemesAnime>? Anime { get; set; }
        }

        internal sealed class AnimeThemesTheme
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("slug")]
            public string? Slug { get; set; }

            [JsonPropertyName("animethemeentries")]
            public List<AnimeThemesEntry>? Entries { get; set; }
        }

        internal sealed class AnimeThemesEntry
        {
            [JsonPropertyName("version")]
            public int? Version { get; set; }

            [JsonPropertyName("videos")]
            public List<AnimeThemesVideo>? Videos { get; set; }
        }

        internal sealed class AnimeThemesVideo
        {
            [JsonPropertyName("basename")]
            public string? Basename { get; set; }

            [JsonPropertyName("link")]
            public string? Link { get; set; }

            [JsonPropertyName("resolution")]
            public int? Resolution { get; set; }

            [JsonPropertyName("audio")]
            public AnimeThemesAudio? Audio { get; set; }
        }

        internal sealed class AnimeThemesAudio
        {
            [JsonPropertyName("filename")]
            public string? Filename { get; set; }

            [JsonPropertyName("link")]
            public string? Link { get; set; }
        }
    }
}
