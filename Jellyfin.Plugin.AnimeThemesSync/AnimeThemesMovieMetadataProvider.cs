using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AnimeThemesSync.Shared;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync;

/// <summary>
/// Metadata provider for AnimeThemes movies.
/// </summary>
public class AnimeThemesMovieMetadataProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AnimeThemesMovieMetadataProvider> _logger;
    private readonly AniListService _aniListService;
    private readonly AnimeThemesService _animeThemesService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeThemesMovieMetadataProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="aniListService">The AniList service.</param>
    /// <param name="animeThemesService">The AnimeThemes service.</param>
    public AnimeThemesMovieMetadataProvider(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        AniListService aniListService,
        AnimeThemesService animeThemesService)
    {
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<AnimeThemesMovieMetadataProvider>();
        _aniListService = aniListService;
        _animeThemesService = animeThemesService;
    }

    /// <inheritdoc />
    public string Name => Constants.MetadataProviderName;

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Movie> { Item = new Movie() };

        var movieName = info.Name;
        var year = info.Year;

        movieName = System.Text.RegularExpressions.Regex.Replace(movieName, @"\s\(\d{4}\)$", string.Empty).Trim();

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

        if (anime != null)
        {
            ApplyTags(result.Item, anime);
        }

        result.HasMetadata = true;
        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
    {
        var results = new List<RemoteSearchResult>();
        var movieName = System.Text.RegularExpressions.Regex.Replace(searchInfo.Name, @"\s\(\d{4}\)$", string.Empty).Trim();

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
                res.SetProviderId(Constants.AniListProviderId, aniListId.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (malId.HasValue)
            {
                res.SetProviderId(Constants.MyAnimeListProviderId, malId.Value.ToString(CultureInfo.InvariantCulture));
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
            item.SetProviderId(Constants.AniListProviderId, aniListId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (malId.HasValue)
        {
            item.SetProviderId(Constants.MyAnimeListProviderId, malId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (anime != null && !string.IsNullOrEmpty(anime.Slug))
        {
            item.SetProviderId(Constants.AnimeThemesProviderId, anime.Slug);
            item.SetProviderId(Constants.AnimeThemesNumericProviderId, anime.Id.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void ApplyTags(Movie item, AnimeThemesAnime anime)
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
