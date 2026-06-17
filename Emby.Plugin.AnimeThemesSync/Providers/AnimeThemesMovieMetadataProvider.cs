using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AnimeThemesSync.Shared;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using Emby.Plugin.AnimeThemesSync.Extensions;
using Emby.Plugin.AnimeThemesSync.Helpers;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.AnimeThemesSync.Providers;

/// <summary>
/// Movie metadata provider for AnimeThemes.
/// </summary>
public class AnimeThemesMovieMetadataProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
{
    private readonly ILogger _logger;
    private readonly IHttpClient _httpClient;
    private readonly AniListService _aniListService;
    private readonly AnimeThemesService _animeThemesService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeThemesMovieMetadataProvider"/> class.
    /// </summary>
    /// <param name="logManager">Log manager.</param>
    /// <param name="httpClient">Emby HTTP client.</param>
    public AnimeThemesMovieMetadataProvider(ILogManager logManager, IHttpClient httpClient)
    {
        _logger = logManager.GetLogger(nameof(AnimeThemesMovieMetadataProvider));
        _httpClient = httpClient;

        var httpClientFactory = new StaticHttpClientFactory();
        var aniListLogger = new EmbyLoggerAdapter<AniListService>(new EmbyLoggerAdapter(logManager.GetLogger(nameof(AniListService))));
        var animeThemesLogger = new EmbyLoggerAdapter<AnimeThemesService>(new EmbyLoggerAdapter(logManager.GetLogger(nameof(AnimeThemesService))));

        var aniListRateLimiter = new RateLimiter(new EmbyLoggerAdapter(logManager.GetLogger("AniListRateLimiter")), Constants.AniListHttpClientName, 90);
        var animeThemesRateLimiter = new RateLimiter(new EmbyLoggerAdapter(logManager.GetLogger("AnimeThemesRateLimiter")), Constants.AnimeThemesHttpClientName, 80);

        _aniListService = new AniListService(httpClientFactory, aniListLogger, aniListRateLimiter);
        _animeThemesService = new AnimeThemesService(httpClientFactory, animeThemesLogger, animeThemesRateLimiter);
    }

    /// <inheritdoc />
    public string Name => Constants.MetadataProviderName;

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Movie> { Item = new Movie() };

        var movieName = Regex.Replace(info.Name, @"\s\(\d{4}\)$", string.Empty).Trim();
        var year = info.Year;

        _logger.LogDebug("Resolving movie metadata for '{MovieName}' ({Year})", movieName, year);

        int? aniListId = TryParseProviderId(info, Constants.AniListProviderId);
        int? malId = TryParseProviderId(info, Constants.MyAnimeListProviderId);

        if (aniListId == null && malId == null)
        {
            _logger.LogDebug("No external IDs found. Searching AniList for movie '{MovieName}'...", movieName);
            (aniListId, malId) = await _aniListService.SearchAnime(movieName, year, cancellationToken).ConfigureAwait(false);
        }

        if (aniListId == null && malId == null)
        {
            _logger.LogWarning("Could not resolve any IDs for movie '{MovieName}'. Skipping AnimeThemes lookup.", movieName);
            return result;
        }

        AnimeThemesAnime? anime = await LookupAnimeThemes(aniListId, malId, cancellationToken).ConfigureAwait(false);
        if (anime == null)
        {
            _logger.LogDebug("ID lookup failed. Falling back to name search for movie '{MovieName}'.", movieName);
            (var newAniListId, var newMalId) = await _aniListService.SearchAnime(movieName, year, cancellationToken).ConfigureAwait(false);

            if (newAniListId.HasValue || newMalId.HasValue)
            {
                anime = await LookupAnimeThemes(newAniListId, newMalId, cancellationToken).ConfigureAwait(false);
                if (anime != null)
                {
                    aniListId = newAniListId ?? aniListId;
                    malId = newMalId ?? malId;
                    _logger.LogDebug("Fallback found AnimeThemes movie entry: AniList:{AniListId}, MAL:{MalId}", aniListId, malId);
                }
            }
        }

        SetProviderIds(result.Item, aniListId, malId, anime);
        ApplyTags(result.Item, anime);

        result.HasMetadata = true;
        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
    {
        var results = new List<RemoteSearchResult>();
        var movieName = Regex.Replace(searchInfo.Name, @"\s\(\d{4}\)$", string.Empty).Trim();

        var (aniListId, malId) = await _aniListService.SearchAnime(movieName, searchInfo.Year, cancellationToken).ConfigureAwait(false);
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
                SetProviderId(res, Constants.AniListProviderId, aniListId.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (malId.HasValue)
            {
                SetProviderId(res, Constants.MyAnimeListProviderId, malId.Value.ToString(CultureInfo.InvariantCulture));
            }

            results.Add(res);
        }

        return results;
    }

    /// <inheritdoc />
    public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClient.GetResponse(new HttpRequestOptions
        {
            Url = url,
            CancellationToken = cancellationToken,
            EnableKeepAlive = false,
            EnableDefaultUserAgent = true
        });
    }

    private static int? TryParseProviderId(MovieInfo info, string key)
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
            anime = await _animeThemesService.GetAnimeByExternalId(Constants.AniListSiteKey, aniListId.Value, cancellationToken).ConfigureAwait(false);
        }

        if (anime == null && malId.HasValue)
        {
            anime = await _animeThemesService.GetAnimeByExternalId(Constants.MyAnimeListSiteKey, malId.Value, cancellationToken).ConfigureAwait(false);
        }

        return anime;
    }

    private static void SetProviderIds(Movie item, int? aniListId, int? malId, AnimeThemesAnime? anime)
    {
        if (aniListId.HasValue)
        {
            SetProviderId(item, Constants.AniListProviderId, aniListId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (malId.HasValue)
        {
            SetProviderId(item, Constants.MyAnimeListProviderId, malId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (anime != null && !string.IsNullOrEmpty(anime.Slug))
        {
            SetProviderId(item, Constants.AnimeThemesProviderId, anime.Slug);
            SetProviderId(item, Constants.AnimeThemesNumericProviderId, anime.Id.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void SetProviderId(Movie item, string key, string value)
    {
        if (item.ProviderIds == null)
        {
            item.ProviderIds = new MediaBrowser.Model.Entities.ProviderIdDictionary();
        }

        item.ProviderIds[key] = value;
    }

    private static void SetProviderId(RemoteSearchResult item, string key, string value)
    {
        if (item.ProviderIds == null)
        {
            item.ProviderIds = new MediaBrowser.Model.Entities.ProviderIdDictionary();
        }

        item.ProviderIds[key] = value;
    }

    private static void ApplyTags(Movie item, AnimeThemesAnime? anime)
    {
        if (!(Plugin.Instance?.Configuration.TagsEnabled ?? false) || anime?.Year == null)
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

    private static void AddTag(Movie item, string tag)
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
