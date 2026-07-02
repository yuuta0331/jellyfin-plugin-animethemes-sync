using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Builds stable broadcast-season values and desired metadata assignments.
/// </summary>
public static class SeasonMetadataPlanner
{
    public static BroadcastSeasonValue? CreateBroadcastSeason(
        int? year,
        string? season,
        string format,
        string spring,
        string summer,
        string fall,
        string winter)
    {
        if (!year.HasValue || string.IsNullOrWhiteSpace(season))
        {
            return null;
        }

        var canonicalSeason = season.Trim().ToLowerInvariant() switch
        {
            "spring" => "spring",
            "summer" => "summer",
            "fall" or "autumn" => "fall",
            "winter" => "winter",
            _ => season.Trim().ToLowerInvariant(),
        };
        var localizedSeason = canonicalSeason switch
        {
            "spring" => spring,
            "summer" => summer,
            "fall" => fall,
            "winter" => winter,
            _ => season.Trim(),
        };
        var yearText = year.Value.ToString(CultureInfo.InvariantCulture);
        var label = (string.IsNullOrWhiteSpace(format) ? "{Season} {Year}" : format)
            .Replace("{Season}", localizedSeason, StringComparison.OrdinalIgnoreCase)
            .Replace("{Year}", yearText, StringComparison.OrdinalIgnoreCase)
            .Trim();
        if (string.IsNullOrWhiteSpace(label))
        {
            label = localizedSeason + " " + yearText;
        }

        return new BroadcastSeasonValue(
            yearText + "-" + canonicalSeason,
            label,
            year.Value,
            canonicalSeason);
    }

    public static IReadOnlyList<string> BuildTags(BroadcastSeasonValue broadcastSeason)
    {
        return new[]
        {
            broadcastSeason.Year.ToString(CultureInfo.InvariantCulture),
            broadcastSeason.Label,
        }.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool AppliesToSeries(SeasonTagTarget target) => target is SeasonTagTarget.Series or SeasonTagTarget.Both;

    public static bool AppliesToSeason(SeasonTagTarget target) => target is SeasonTagTarget.Season or SeasonTagTarget.Both;

    public static bool UsesSeriesForCollection(int? seasonNumber, bool enabled) => enabled && seasonNumber == 1;
}
