using System;
using System.IO;
using AnimeThemesSync.Shared.Configuration;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Pure helpers for matching a stored <see cref="SeasonThemeMapping"/> against a
/// library season. Extracted from the host ThemeDownloader classes so both hosts
/// share one implementation.
/// </summary>
public static class SeasonMappingMatchHelper
{
    /// <summary>
    /// Ranks how specifically a mapping identifies the given season:
    /// 4 season id, 3 season path, 2 series id + season number,
    /// 1 series path + season number, 0 no match.
    /// </summary>
    public static int GetSeasonMappingMatchRank(
        SeasonThemeMapping mapping,
        string seasonItemId,
        string compactSeasonItemId,
        string seasonPath,
        string seriesItemId,
        string compactSeriesItemId,
        string seriesPath,
        string seasonParentPath,
        int? seasonNumber)
    {
        if (MatchesId(mapping.SeasonItemId, seasonItemId, compactSeasonItemId))
        {
            return 4;
        }

        if (MatchesPath(mapping.SeasonPath, seasonPath))
        {
            return 3;
        }

        if (!mapping.SeasonNumber.HasValue || seasonNumber != mapping.SeasonNumber.Value)
        {
            return 0;
        }

        if (MatchesId(mapping.SeriesItemId, seriesItemId, compactSeriesItemId))
        {
            return 2;
        }

        return MatchesPath(mapping.SeriesPath, seriesPath) || MatchesPath(mapping.SeriesPath, seasonParentPath) ? 1 : 0;
    }

    /// <summary>
    /// Compares a configured item id against both the dashed and compact forms.
    /// </summary>
    public static bool MatchesId(string? configuredId, string itemId, string compactItemId)
    {
        return !string.IsNullOrWhiteSpace(configuredId) &&
               (string.Equals(configuredId.Trim(), itemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(configuredId.Trim(), compactItemId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Compares a configured path against a normalized item path.
    /// </summary>
    public static bool MatchesPath(string? configuredPath, string itemPath)
    {
        return !string.IsNullOrWhiteSpace(configuredPath) &&
               string.Equals(NormalizeMappingPath(configuredPath), itemPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Trims whitespace and trailing directory separators.
    /// </summary>
    public static string NormalizeMappingPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Gets whether the mapping carries a resolvable AnimeThemes identity.
    /// </summary>
    public static bool HasThemeIdentity(SeasonThemeMapping mapping)
    {
        return !string.IsNullOrWhiteSpace(mapping.AnimeThemesSlug) ||
               mapping.AniListId.HasValue ||
               mapping.MyAnimeListId.HasValue;
    }
}
