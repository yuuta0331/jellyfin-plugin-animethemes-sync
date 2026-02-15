using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AnimeThemesSync.Services;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync;

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
    /// <param name="aniListService">The AniList service.</param>
    /// <param name="animeThemesService">The AnimeThemes service.</param>
    public AnimeThemesMetadataProvider(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        AniListService aniListService,
        AnimeThemesService animeThemesService)
    {
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<AnimeThemesMetadataProvider>();
        _aniListService = aniListService;
        _animeThemesService = animeThemesService;
    }

    /// <inheritdoc />
    public string Name => Constants.MetadataProviderName;

    /// <inheritdoc />
    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Series> { Item = new Series() };

        var seriesName = info.Name;
        var year = info.Year;

        // Clean series name (remove year if present, e.g. "Name (2021)" -> "Name")
        seriesName = System.Text.RegularExpressions.Regex.Replace(seriesName, @"\s\(\d{4}\)$", string.Empty).Trim();

        _logger.LogDebug("Resolving metadata for '{SeriesName}' ({Year})", seriesName, year);

        // 1. Direct ID Lookup
        int? aniListId = TryParseProviderId(info, "AniList");
        int? malId = TryParseProviderId(info, "MyAnimeList");

        // 2. High-Precision Metadata Search (if IDs missing)
        if (aniListId == null && malId == null)
        {
            _logger.LogDebug("No external IDs found. Searching AniList for '{SeriesName}'...", seriesName);
            (aniListId, malId) = await _aniListService.SearchAnime(seriesName, year, cancellationToken).ConfigureAwait(false);
        }

        if (aniListId == null && malId == null)
        {
            _logger.LogWarning("Could not resolve any IDs for '{SeriesName}'. Skipping AnimeThemes lookup.", seriesName);
            return result;
        }

        // 3. AnimeThemes Lookup
        AnimeThemesAnime? anime = await LookupAnimeThemes(aniListId, malId, cancellationToken).ConfigureAwait(false);

        // 4. Fallback: re-search AniList by name
        if (anime == null)
        {
            _logger.LogDebug("ID lookup failed. Falling back to name search for '{SeriesName}'.", seriesName);
            (var newAniListId, var newMalId) = await _aniListService.SearchAnime(seriesName, year, cancellationToken).ConfigureAwait(false);

            if (newAniListId.HasValue || newMalId.HasValue)
            {
                anime = await LookupAnimeThemes(newAniListId, newMalId, cancellationToken).ConfigureAwait(false);
                if (anime != null)
                {
                    aniListId = newAniListId ?? aniListId;
                    malId = newMalId ?? malId;
                    _logger.LogDebug("Fallback found AnimeThemes entry: AniList:{AniListId}, MAL:{MalId}", aniListId, malId);
                }
            }
        }

        // Set provider IDs
        SetProviderIds(result.Item, aniListId, malId, anime);

        // Tagging
        if (anime != null)
        {
            ApplyTags(result.Item, anime);
        }

        result.HasMetadata = true;
        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
    {
        var results = new List<RemoteSearchResult>();

        var seriesName = searchInfo.Name;
        // Clean series name (remove year if present)
        seriesName = System.Text.RegularExpressions.Regex.Replace(seriesName, @"\s\(\d{4}\)$", string.Empty).Trim();

        var (aniListId, malId) = await _aniListService.SearchAnime(seriesName, searchInfo.Year, cancellationToken).ConfigureAwait(false);

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

    private static int? TryParseProviderId(SeriesInfo info, string key)
    {
        if (info.ProviderIds.TryGetValue(key, out var value) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return id;
        }

        return null;
    }

    private async Task<AnimeThemesAnime?> LookupAnimeThemes(int? aniListId, int? malId, CancellationToken cancellationToken)
    {
        AnimeThemesAnime? anime = null;

        if (aniListId.HasValue)
        {
            anime = await _animeThemesService.GetAnimeByExternalId("anilist", aniListId.Value, cancellationToken).ConfigureAwait(false);
        }

        if (anime == null && malId.HasValue)
        {
            anime = await _animeThemesService.GetAnimeByExternalId("myanimelist", malId.Value, cancellationToken).ConfigureAwait(false);
        }

        return anime;
    }

    private static void SetProviderIds(Series item, int? aniListId, int? malId, AnimeThemesAnime? anime)
    {
        if (aniListId.HasValue)
        {
            item.SetProviderId("AniList", aniListId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (malId.HasValue)
        {
            item.SetProviderId("MyAnimeList", malId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (anime != null && !string.IsNullOrEmpty(anime.Slug))
        {
            item.SetProviderId("AnimeThemes", anime.Slug);
            item.SetProviderId("AnimeThemesId", anime.Id.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void ApplyTags(Series item, AnimeThemesAnime anime)
    {
        if (!(Plugin.Instance?.Configuration.TagsEnabled ?? false))
        {
            return;
        }

        if (!anime.Year.HasValue)
        {
            return;
        }

        AddTag(item, anime.Year.Value.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(anime.Season))
        {
            var config = Plugin.Instance!.Configuration;
            var seasonName = anime.Season.ToLowerInvariant() switch
            {
                "winter" => config.TagSeasonWinter,
                "spring" => config.TagSeasonSpring,
                "summer" => config.TagSeasonSummer,
                "fall" => config.TagSeasonFall,
                _ => anime.Season
            };

            var seasonTag = config.TagFormat
                .Replace("{Season}", seasonName, StringComparison.OrdinalIgnoreCase)
                .Replace("{Year}", anime.Year.Value.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

            AddTag(item, seasonTag);
        }
    }

    private static void AddTag(Series item, string tag)
    {
        if (item.Tags == null)
        {
            item.Tags = new[] { tag };
        }
        else if (!item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            item.Tags = item.Tags.Append(tag).ToArray();
        }
    }
}
