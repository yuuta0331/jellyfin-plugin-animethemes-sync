using System;
using System.Collections.Generic;
using System.Linq;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Pure helpers for matching, scoring, and describing AnimeThemes entries.
/// Extracted from the host ThemeDownloader classes so both hosts share one
/// implementation.
/// </summary>
public static class AnimeMatchHelper
{
    /// <summary>
    /// Picks the best available cover image URL for an anime.
    /// </summary>
    public static string? GetAnimePrimaryImageUrl(AnimeThemesAnime? anime)
    {
        var images = anime?.Images;
        if (images == null || images.Count == 0)
        {
            return null;
        }

        return images.FirstOrDefault(i => string.Equals(i.Facet, "Small Cover", StringComparison.OrdinalIgnoreCase))?.Link
            ?? images.FirstOrDefault(i => string.Equals(i.Facet, "Large Cover", StringComparison.OrdinalIgnoreCase))?.Link
            ?? images.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Link))?.Link;
    }

    /// <summary>
    /// Builds a Season Finder search result row for one candidate.
    /// </summary>
    public static ThemeFinderSearchResult ToThemeFinderSearchResult(AnimeThemesAnime anime, int score, string? imageUrl, string query)
    {
        var ids = ExtractAnimeExternalIds(anime);
        var match = FindMatchedTitle(anime, query);
        return new ThemeFinderSearchResult(
            anime.Id,
            anime.Name ?? anime.Slug ?? "AnimeThemes",
            anime.Slug,
            anime.Year,
            anime.Season,
            ids.AniListId,
            ids.MyAnimeListId,
            !string.IsNullOrWhiteSpace(anime.Slug) ? Constants.AnimeThemesWebUrl + "/anime/" + anime.Slug : null,
            score,
            imageUrl,
            anime.MediaFormat,
            match.Title,
            match.Type);
    }

    /// <summary>
    /// Ranks a search candidate against the query text and optional year.
    /// </summary>
    public static int ScoreSearchCandidate(AnimeThemesAnime anime, string query, int? year)
    {
        var normalizedQuery = NormalizeSearchText(query);
        var normalizedSlug = NormalizeSearchText(anime.Slug);
        var normalizedNames = GetAnimeTitleCandidates(anime)
            .Select(NormalizeSearchText)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var score = 0;
        if (normalizedNames.Any(name => name == normalizedQuery))
        {
            score += 100;
        }
        else if (normalizedNames.Any(name =>
            name.Contains(normalizedQuery, StringComparison.Ordinal) ||
            normalizedQuery.Contains(name, StringComparison.Ordinal)))
        {
            score += 70;
        }
        else if (!string.IsNullOrWhiteSpace(normalizedSlug) && normalizedSlug.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            score += 55;
        }

        if (year.HasValue && anime.Year.HasValue)
        {
            var delta = Math.Abs(anime.Year.Value - year.Value);
            score += delta == 0 ? 30 : delta == 1 ? 15 : 0;
        }

        return score;
    }

    /// <summary>
    /// Enumerates the primary name and all synonyms of an anime.
    /// </summary>
    public static IEnumerable<string?> GetAnimeTitleCandidates(AnimeThemesAnime anime)
    {
        yield return anime.Name;

        foreach (var synonym in anime.Synonyms ?? [])
        {
            yield return synonym.Text;
        }
    }

    /// <summary>
    /// Finds the synonym that matched the query, if any.
    /// </summary>
    public static (string? Title, string? Type) FindMatchedTitle(AnimeThemesAnime anime, string query)
    {
        var normalizedQuery = NormalizeSearchText(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return (null, null);
        }

        foreach (var synonym in anime.Synonyms ?? [])
        {
            var normalizedTitle = NormalizeSearchText(synonym.Text);
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                continue;
            }

            if (normalizedTitle == normalizedQuery ||
                normalizedTitle.Contains(normalizedQuery, StringComparison.Ordinal) ||
                normalizedQuery.Contains(normalizedTitle, StringComparison.Ordinal))
            {
                return (synonym.Text, synonym.Type);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Lower-cases and collapses text to letters, digits, and single spaces.
    /// </summary>
    public static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray();
        return string.Join(" ", new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Extracts the AniList and MyAnimeList IDs from an anime's resources.
    /// </summary>
    public static (int? AniListId, int? MyAnimeListId) ExtractAnimeExternalIds(AnimeThemesAnime? anime)
    {
        int? aniListId = null;
        int? myAnimeListId = null;
        foreach (var resource in anime?.Resources ?? [])
        {
            if (resource.ExternalId == null || string.IsNullOrWhiteSpace(resource.Site))
            {
                continue;
            }

            if (string.Equals(resource.Site, Constants.AniListSiteKey, StringComparison.OrdinalIgnoreCase))
            {
                aniListId = resource.ExternalId;
            }
            else if (string.Equals(resource.Site, Constants.MyAnimeListSiteKey, StringComparison.OrdinalIgnoreCase))
            {
                myAnimeListId = resource.ExternalId;
            }
        }

        return (aniListId, myAnimeListId);
    }

    /// <summary>
    /// Compares two anime by ID when available, otherwise by slug.
    /// </summary>
    public static bool IsSameAnime(AnimeThemesAnime left, AnimeThemesAnime right)
    {
        if (left.Id > 0 && right.Id > 0)
        {
            return left.Id == right.Id;
        }

        return !string.IsNullOrWhiteSpace(left.Slug) &&
               !string.IsNullOrWhiteSpace(right.Slug) &&
               string.Equals(left.Slug, right.Slug, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the public AnimeThemes web URL for an anime.
    /// </summary>
    public static string? BuildAnimeThemesUrl(AnimeThemesAnime? anime)
    {
        return !string.IsNullOrWhiteSpace(anime?.Slug)
            ? Constants.AnimeThemesWebUrl + "/anime/" + anime.Slug
            : null;
    }
}
