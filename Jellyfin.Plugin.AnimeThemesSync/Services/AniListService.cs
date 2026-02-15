using System;
using System.Collections.Generic;

using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync.Services;

/// <summary>
/// Service for searching anime on AniList.
/// </summary>
public sealed class AniListService
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AniListService> _logger;
    private readonly RateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AniListService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="rateLimiter">The rate limiter.</param>
    public AniListService(IHttpClientFactory httpClientFactory, ILogger<AniListService> logger, RateLimiter rateLimiter)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Searches for an anime on AniList by title and year.
    /// Uses composite scoring (title similarity + year matching) to find the best match.
    /// </summary>
    /// <param name="name">The title of the anime.</param>
    /// <param name="year">The release year (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing (AniListId, MalId) or nulls if not found.</returns>
    public async Task<(int? AniListId, int? MalId)> SearchAnime(string name, int? year, CancellationToken cancellationToken)
    {
        try
        {
            // First attempt: search with year filter if available
            var matches = await ExecuteSearch(name, year, cancellationToken).ConfigureAwait(false);

            if (matches == null || matches.Count == 0)
            {
                // If year-filtered search returned nothing, retry without year
                if (year.HasValue)
                {
                    _logger.LogDebug("Year-filtered search returned no results for '{Name}' ({Year}). Retrying without year.", name, year);
                    matches = await ExecuteSearch(name, null, cancellationToken).ConfigureAwait(false);
                }

                if (matches == null || matches.Count == 0)
                {
                    _logger.LogWarning("AniList search returned no results for '{Name}'.", name);
                    return (null, null);
                }
            }

            // Score all candidates by title similarity + year proximity
            var best = SelectBestMatch(matches, name, year);

            _logger.LogDebug(
                "Best match for '{Name}' ({Year}): AniListId={AniListId}, Title={Title}, Score={Score:F1}",
                name,
                year,
                best.Media.Id,
                best.Media.Title?.Romaji ?? best.Media.Title?.English ?? "?",
                best.Score);

            // If year was provided and best match has too large a year gap, reject
            if (year.HasValue && best.Media.StartDate?.Year.HasValue == true)
            {
                var yearDiff = Math.Abs(best.Media.StartDate.Year.Value - year.Value);
                if (yearDiff > 1)
                {
                    _logger.LogWarning(
                        "Best match year {MatchYear} is {Diff} years from requested {Year}. Rejecting.",
                        best.Media.StartDate.Year.Value,
                        yearDiff,
                        year);
                    return (null, null);
                }
            }

            return (best.Media.Id, best.Media.IdMal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching AniList for '{Name}'.", name);
            return (null, null);
        }
    }

    /// <summary>
    /// Executes the AniList GraphQL search query.
    /// </summary>
    private async Task<List<AniListMedia>?> ExecuteSearch(string name, int? year, CancellationToken cancellationToken)
    {
        var query = @"
            query ($search: String, $seasonYear: Int) {
                Page(page: 1, perPage: 10) {
                    media(search: $search, type: ANIME, seasonYear: $seasonYear) {
                        id
                        idMal
                        title { romaji english native }
                        startDate { year }
                    }
                }
            }";

        // Use Dictionary for reliable serialization (anonymous types + object boxing can lose properties)
        var variables = new Dictionary<string, object> { { "search", name } };
        if (year.HasValue)
        {
            variables["seasonYear"] = year.Value;
        }

        var requestBody = new Dictionary<string, object>
        {
            { "query", query },
            { "variables", variables },
        };

        var client = _httpClientFactory.CreateClient("AniList");
        var json = JsonSerializer.Serialize(requestBody);
        _logger.LogDebug("AniList request body: {Json}", json);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await _rateLimiter.WaitIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var response = await client.PostAsync(new Uri(Constants.AniListBaseUrl), content, cancellationToken).ConfigureAwait(false);

        _rateLimiter.UpdateState(response.Headers);

        response.EnsureSuccessStatusCode();

        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<AniListResponse>(responseStream, _jsonOptions, cancellationToken).ConfigureAwait(false);

        return result?.Data?.Page?.Media;
    }

    /// <summary>
    /// Selects the best match from candidates using composite scoring (lower = better).
    /// </summary>
    /// <param name="candidates">The list of media candidates.</param>
    /// <param name="searchName">The search title.</param>
    /// <param name="year">The optional year.</param>
    /// <returns>The best scored match.</returns>
    internal static (AniListMedia Media, double Score) SelectBestMatch(List<AniListMedia> candidates, string searchName, int? year)
    {
        var best = candidates
            .Select(m => (Media: m, Score: ScoreCandidate(m, searchName, year)))
            .OrderBy(x => x.Score)
            .First();

        return best;
    }

    /// <summary>
    /// Scores a single candidate (lower = better).
    /// Components: title similarity (0-100) + year penalty (0-500).
    /// </summary>
    /// <param name="media">The media candidate.</param>
    /// <param name="searchName">The search title.</param>
    /// <param name="year">The optional year.</param>
    /// <returns>A composite score where lower is better.</returns>
    internal static double ScoreCandidate(AniListMedia media, string searchName, int? year)
    {
        var titleScore = ScoreTitle(media.Title, searchName);
        double yearPenalty = 0;

        if (year.HasValue && media.StartDate?.Year.HasValue == true)
        {
            var diff = Math.Abs(media.StartDate.Year.Value - year.Value);
            yearPenalty = diff * 100;
        }
        else if (year.HasValue && media.StartDate?.Year == null)
        {
            // No year data available — mild penalty
            yearPenalty = 50;
        }

        return titleScore + yearPenalty;
    }

    /// <summary>
    /// Scores how well a candidate title matches the search name (lower = better).
    /// 0 = exact match, 10 = containment match, 20 = normalized match, 50 = poor match.
    /// </summary>
    /// <param name="title">The AniList title object with romaji/english/native variants.</param>
    /// <param name="searchName">The search title to compare against.</param>
    /// <returns>A title similarity score where lower is better.</returns>
    internal static double ScoreTitle(AniListTitle? title, string searchName)
    {
        if (title == null)
        {
            return 50;
        }

        var searchNormalized = NormalizeTitle(searchName);

        var titleVariants = new[] { title.Romaji, title.English, title.Native }
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (titleVariants.Count == 0)
        {
            return 50;
        }

        // Check exact match (case-insensitive)
        if (titleVariants.Any(t => string.Equals(t, searchName, StringComparison.OrdinalIgnoreCase)))
        {
            return 0;
        }

        // Check normalized exact match (strip special chars)
        var normalizedVariants = titleVariants.Select(t => NormalizeTitle(t!)).ToList();
        if (normalizedVariants.Any(n => string.Equals(n, searchNormalized, StringComparison.OrdinalIgnoreCase)))
        {
            return 5;
        }

        // Check containment (search name is part of title or vice versa)
        if (titleVariants.Any(t =>
            t!.Contains(searchName, StringComparison.OrdinalIgnoreCase) ||
            searchName.Contains(t!, StringComparison.OrdinalIgnoreCase)))
        {
            return 10;
        }

        // Check normalized containment
        if (normalizedVariants.Any(n =>
            n.Contains(searchNormalized, StringComparison.OrdinalIgnoreCase) ||
            searchNormalized.Contains(n, StringComparison.OrdinalIgnoreCase)))
        {
            return 15;
        }

        return 50;
    }

    /// <summary>
    /// Normalizes a title for fuzzy comparison by removing special characters and extra spaces.
    /// </summary>
    /// <param name="title">The title string to normalize.</param>
    /// <returns>The normalized title string.</returns>
    internal static string NormalizeTitle(string title)
    {
        // Remove common special characters (★, ☆, !, ?, etc.) and punctuation, replace with space
        var normalized = Regex.Replace(title, @"[^\p{L}\p{N}\s]", " ");
        // Collapse whitespace
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
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

    internal sealed class AniListMedia
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

    internal sealed class AniListTitle
    {
        [JsonPropertyName("romaji")]
        public string? Romaji { get; set; }

        [JsonPropertyName("english")]
        public string? English { get; set; }

        [JsonPropertyName("native")]
        public string? Native { get; set; }
    }

    internal sealed class AniListDate
    {
        [JsonPropertyName("year")]
        public int? Year { get; set; }
    }
}
