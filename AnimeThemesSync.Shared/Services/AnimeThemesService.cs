using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Service for searching anime on AnimeThemes.
/// </summary>
public sealed class AnimeThemesService
{
    private const int SearchCacheLimit = 100;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly ConcurrentDictionary<string, AnimeThemesAnime> _animeCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _searchCacheLock = new();
    private static readonly Dictionary<string, SearchCacheEntry> _searchCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(10);
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
        var cacheKey = $"resource:{site}:{externalId}";
        if (_animeCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

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

        var anime = await GetAnimeBySlug(partialAnime.Slug, cancellationToken).ConfigureAwait(false);
        if (anime != null)
        {
            _animeCache[cacheKey] = anime;
        }

        return anime;
    }

    /// <summary>
    /// Gets an anime by its slug.
    /// </summary>
    /// <param name="slug">The anime slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The found anime or null.</returns>
    public async Task<AnimeThemesAnime?> GetAnimeBySlug(string slug, CancellationToken cancellationToken)
    {
        var cacheKey = $"slug:{slug.Trim()}";
        if (_animeCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        const string Include = "images,resources,animethemes.animethemeentries.videos.audio,animethemes.group,animethemes.song,animethemes.song.artists,animethemes.song.performances.artist";
        var url = $"{Constants.AnimeThemesBaseUrl}/anime/{slug}?include={Include}";
        var anime = await GetAnimeFromUrl(url, cancellationToken).ConfigureAwait(false);
        if (anime != null)
        {
            _animeCache[cacheKey] = anime;
            if (!string.IsNullOrWhiteSpace(anime.Slug))
            {
                _animeCache[$"slug:{anime.Slug}"] = anime;
            }
        }

        return anime;
    }

    /// <summary>
    /// Searches AnimeThemes by title.
    /// </summary>
    /// <param name="query">The title query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching AnimeThemes anime candidates.</returns>
    public async Task<IReadOnlyList<AnimeThemesAnime>> SearchAnimeByTitle(string query, CancellationToken cancellationToken)
    {
        return await SearchAnimeByTitle(query, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AnimeThemesAnime>> SearchAnimeByTitle(string query, int? year, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var cacheKey = BuildSearchCacheKey(query, year);
        if (TryGetCachedSearch(cacheKey, out var cached))
        {
            return cached;
        }

        var escapedQuery = Uri.EscapeDataString(query.Trim());
        var url = $"{Constants.AnimeThemesBaseUrl}/anime?q={escapedQuery}" +
                  "&page%5Bsize%5D=15&page%5Bnumber%5D=1" +
                  "&include=synonyms,images,resources" +
                  "&fields%5Banime%5D=id,name,slug,year,season,media_format" +
                  "&fields%5Bsynonym%5D=id,text,type" +
                  "&fields%5Bimage%5D=id,facet,link" +
                  "&fields%5Bresource%5D=id,site,external_id";
        if (year.HasValue)
        {
            url += $"&filter%5Byear%5D={year.Value}";
        }

        var response = await SendRequestAsync<AnimeThemesAnimeIndexResponse>(url, cancellationToken).ConfigureAwait(false);
        var results = (IReadOnlyList<AnimeThemesAnime>)(response?.Anime ?? []);
        SetCachedSearch(cacheKey, results);
        return results;
    }

    private static string BuildSearchCacheKey(string query, int? year)
    {
        return string.Join("|", query.Trim().ToLowerInvariant(), year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static bool TryGetCachedSearch(string key, out IReadOnlyList<AnimeThemesAnime> results)
    {
        lock (_searchCacheLock)
        {
            if (_searchCache.TryGetValue(key, out var entry) &&
                DateTimeOffset.UtcNow - entry.CreatedAt < SearchCacheTtl)
            {
                results = entry.Results;
                return true;
            }

            _searchCache.Remove(key);
        }

        results = [];
        return false;
    }

    private static void SetCachedSearch(string key, IReadOnlyList<AnimeThemesAnime> results)
    {
        lock (_searchCacheLock)
        {
            _searchCache[key] = new SearchCacheEntry(DateTimeOffset.UtcNow, results);
            if (_searchCache.Count <= SearchCacheLimit)
            {
                return;
            }

            foreach (var staleKey in _searchCache
                         .OrderBy(pair => pair.Value.CreatedAt)
                         .Take(_searchCache.Count - SearchCacheLimit)
                         .Select(pair => pair.Key)
                         .ToList())
            {
                _searchCache.Remove(staleKey);
            }
        }
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

            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
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
        var client = _httpClientFactory.CreateClient(Constants.AnimeThemesHttpClientName);
        if (!client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
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
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync().ConfigureAwait(false), default, cancellationToken).ConfigureAwait(false);
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

    private sealed record SearchCacheEntry(DateTimeOffset CreatedAt, IReadOnlyList<AnimeThemesAnime> Results);
}
