using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync.Services
{
    /// <summary>
    /// Service for searching anime on AniList.
    /// </summary>
    public sealed class AniListService
    {
        private const string AniListUrl = "https://graphql.anilist.co";
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AniListService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AniListService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public AniListService(IHttpClientFactory httpClientFactory, ILogger<AniListService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Searches for an anime on AniList by title and year.
        /// </summary>
        /// <param name="name">The title of the anime.</param>
        /// <param name="year">The release year (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A tuple containing (AniListId, MalId) or nulls if not found.</returns>
        public async Task<(int? AniListId, int? MalId)> SearchAnime(string name, int? year, CancellationToken cancellationToken)
        {
            try
            {
                var query = @"
                query ($search: String) {
                    Page(page: 1, perPage: 5) {
                        media(search: $search, type: ANIME) {
                            id
                            idMal
                            title { romaji english native }
                            startDate { year }
                        }
                    }
                }";

                var variables = new { search = name };
                var requestBody = new { query, variables };

                var client = _httpClientFactory.CreateClient("AniList");

                // Construct the request manually to ensure headers are correct if needed, though usually standard post works.
                // AniList API requires simple POST with JSON.
                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await client.PostAsync(new Uri(AniListUrl), content, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var result = await JsonSerializer.DeserializeAsync<AniListResponse>(responseStream, _jsonOptions, cancellationToken).ConfigureAwait(false);

                if (result?.Data?.Page?.Media == null || result.Data.Page.Media.Count == 0)
                {
                    _logger.LogWarning("AniList search returned no results for '{Name}'.", name);
                    return (null, null);
                }

                var matches = result.Data.Page.Media;

                // 2. High-Precision Metadata Search Strategy
                // Filter results where `startDate.year` == `year` (allow +/- 1 year tolerance) if year is provided.
                if (year.HasValue)
                {
                    var exactMatch = matches.FirstOrDefault(m => m.StartDate?.Year == year.Value);
                    if (exactMatch != null)
                    {
                        _logger.LogInformation("Found exact match for '{Name}' ({Year}): AniListId={AniListId}", name, year, exactMatch.Id);
                        return (exactMatch.Id, exactMatch.IdMal);
                    }

                    var tolerantMatch = matches.FirstOrDefault(m => m.StartDate?.Year.HasValue == true && Math.Abs(m.StartDate.Year.Value - year.Value) <= 1);
                    if (tolerantMatch != null)
                    {
                         _logger.LogInformation("Found tolerant match for '{Name}' ({Year}): AniListId={AniListId}", name, year, tolerantMatch.Id);
                         return (tolerantMatch.Id, tolerantMatch.IdMal);
                    }

                    _logger.LogWarning("No match found within +/- 1 year for '{Name}' ({Year}). High-precision search failed.", name, year);
                    return (null, null);
                }

                // If no year provided, return the first result (highest relevance by AniList search)
                var bestMatch = matches.First();
                return (bestMatch.Id, bestMatch.IdMal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching AniList for '{Name}'.", name);
                return (null, null);
            }
        }

        // Internal DTOs for AniList Response
        private sealed class AniListResponse
        {
            [JsonPropertyName("data")]
            public AniListData? Data { get; set; }
        }

        private sealed class AniListData
        {
            [JsonPropertyName("Page")]
            public AniListPage? Page { get; set; }
        }

        private sealed class AniListPage
        {
            [JsonPropertyName("media")]
            public List<AniListMedia>? Media { get; set; }
        }

        private sealed class AniListMedia
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("idMal")]
            public int? IdMal { get; set; }

            [JsonPropertyName("title")]
            public AniListTitle? Title { get; set; }

            [JsonPropertyName("startDate")]
            public AniListDate? StartDate { get; set; }
        }

        private sealed class AniListTitle
        {
            [JsonPropertyName("romaji")]
            public string? Romaji { get; set; }

            [JsonPropertyName("english")]
            public string? English { get; set; }

            [JsonPropertyName("native")]
            public string? Native { get; set; }
        }

        private sealed class AniListDate
        {
            [JsonPropertyName("year")]
            public int? Year { get; set; }
        }
    }
}
