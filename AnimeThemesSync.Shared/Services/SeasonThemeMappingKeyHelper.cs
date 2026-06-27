using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

internal static class SeasonThemeMappingKeyHelper
{
    public static string? BuildMappingKey(SeasonThemeMapping mapping)
    {
        if (!string.IsNullOrWhiteSpace(mapping.SeasonItemId))
        {
            return "id:" + NormalizeId(mapping.SeasonItemId);
        }

        if (!string.IsNullOrWhiteSpace(mapping.SeasonPath))
        {
            return "path:" + NormalizePath(mapping.SeasonPath);
        }

        var series = !string.IsNullOrWhiteSpace(mapping.SeriesItemId)
            ? "id:" + NormalizeId(mapping.SeriesItemId)
            : !string.IsNullOrWhiteSpace(mapping.SeriesPath) ? "path:" + NormalizePath(mapping.SeriesPath) : null;
        return series != null && mapping.SeasonNumber.HasValue ? $"series:{series}:{mapping.SeasonNumber.Value}" : null;
    }

    public static IReadOnlyList<string> BuildTargetKeys(SeasonThemeMappingTarget target)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(target.SeasonItemId))
        {
            keys.Add("id:" + NormalizeId(target.SeasonItemId));
        }

        if (!string.IsNullOrWhiteSpace(target.SeasonPath))
        {
            keys.Add("path:" + NormalizePath(target.SeasonPath));
        }

        if (target.SeasonNumber.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(target.SeriesItemId))
            {
                keys.Add($"series:id:{NormalizeId(target.SeriesItemId)}:{target.SeasonNumber.Value}");
            }

            AddSeriesPathKey(keys, target.SeriesPath, target.SeasonNumber.Value);
            AddSeriesPathKey(keys, target.SeasonParentPath, target.SeasonNumber.Value);
        }

        return keys.ToList();
    }

    public static IReadOnlyList<SeasonThemeMapping> Deduplicate(IEnumerable<SeasonThemeMapping> mappings)
    {
        return mappings
            .Select((mapping, index) => new { Mapping = mapping, Index = index, Key = BuildMappingKey(mapping) })
            .Where(item => item.Key != null)
            .GroupBy(item => item.Key!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.Mapping.Locked)
                .ThenByDescending(item => item.Index)
                .First().Mapping)
            .ToList();
    }

    private static void AddSeriesPathKey(HashSet<string> keys, string? path, int seasonNumber)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            keys.Add($"series:path:{NormalizePath(path)}:{seasonNumber}");
        }
    }

    private static string NormalizeId(string value) =>
        Guid.TryParse(value, out var parsed) ? parsed.ToString("D") : value.Trim().ToLowerInvariant();

    private static string NormalizePath(string value) =>
        value.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
}
