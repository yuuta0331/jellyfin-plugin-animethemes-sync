using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync.Services;

/// <summary>
/// Service for searching anime on AnimeThemes.
/// </summary>
public sealed class AnimeThemesService
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AnimeThemesService> _logger;
    private readonly RateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeThemesService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="rateLimiter">The rate limiter.</param>
    public AnimeThemesService(IHttpClientFactory httpClientFactory, ILogger<AnimeThemesService> logger, RateLimiter rateLimiter)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _rateLimiter = rateLimiter;
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
        var url = $"{Constants.AnimeThemesBaseUrl}/resource?filter[site]={site}&filter[external_id]={externalId}&include=anime";

        var resourceResponse = await GetResourceFromUrl(url, cancellationToken).ConfigureAwait(false);
        var resource = resourceResponse?.Resources?.FirstOrDefault();

        if (resource?.Anime == null || resource.Anime.Count == 0)
        {
            _logger.LogWarning("No AnimeThemes resource found for {Site}:{Id}", site, externalId);
            return null;
        }

        var partialAnime = resource.Anime[0];
        if (string.IsNullOrEmpty(partialAnime.Slug))
        {
            _logger.LogWarning("AnimeThemes resource found for {Site}:{Id}, but slug is missing.", site, externalId);
            return null;
        }

        return await GetAnimeBySlug(partialAnime.Slug, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets an anime by its slug.
    /// </summary>
    /// <param name="slug">The anime slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The found anime or null.</returns>
    public async Task<AnimeThemesAnime?> GetAnimeBySlug(string slug, CancellationToken cancellationToken)
    {
        var url = $"{Constants.AnimeThemesBaseUrl}/anime/{slug}?include=images,resources,animethemes.animethemeentries.videos.audio";
        return await GetAnimeFromUrl(url, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T?> SendRequestAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            await _rateLimiter.WaitIfNeededAsync(cancellationToken).ConfigureAwait(false);

            var client = CreateClient();
            var response = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);

            _rateLimiter.UpdateState(response.Headers);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AnimeThemes API returned {StatusCode} for URL: {Url}", response.StatusCode, url);
                return null;
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching data from AnimeThemes: {Url}", url);
            return null;
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("AnimeThemes");
        if (!client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        return client;
    }

    private async Task<AnimeThemesResourceResponse?> GetResourceFromUrl(string url, CancellationToken cancellationToken)
    {
        return await SendRequestAsync<AnimeThemesResourceResponse>(url, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AnimeThemesAnime?> GetAnimeFromUrl(string url, CancellationToken cancellationToken)
    {
        try
        {
            await _rateLimiter.WaitIfNeededAsync(cancellationToken).ConfigureAwait(false);

            var client = CreateClient();
            var response = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);

            _rateLimiter.UpdateState(response.Headers);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AnimeThemes API returned {StatusCode} for URL: {Url}", response.StatusCode, url);
                return null;
            }

            // Use JsonElement to inspect structure (anime can be object or array)
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
}
