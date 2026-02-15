using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Jellyfin.Plugin.AnimeThemesSync.Services;

/// <summary>
/// Provides scoring, filtering, and best-video selection logic for anime themes.
/// </summary>
internal static class ThemeScoringService
{
    private const int ScoreSpoiler = 50;
    private const int ScoreOverlapOver = 20;
    private const int ScoreOverlapTransition = 15;
    private const int ScoreSourceLdVhs = 10;
    private const int ScoreSourceWebRaw = 5;
    private const int ScoreCredits = 10;

    /// <summary>
    /// Gets all scored candidates across all entries of all themes, sorted by score (lowest first).
    /// Each entry contributes at most one candidate (its best video).
    /// </summary>
    /// <param name="themes">All themes for the anime.</param>
    /// <param name="ignoreOp">Whether to exclude OP themes.</param>
    /// <param name="ignoreEd">Whether to exclude ED themes.</param>
    /// <param name="ignoreOverlaps">Whether to exclude overlapping videos.</param>
    /// <param name="ignoreCredits">Whether to exclude non-creditless videos.</param>
    /// <returns>Sorted list of candidates.</returns>
    public static List<ScoredCandidate> GetScoredCandidates(
        List<AnimeThemesTheme> themes,
        bool ignoreOp,
        bool ignoreEd,
        bool ignoreOverlaps,
        bool ignoreCredits)
    {
        var filtered = FilterThemes(themes, ignoreOp, ignoreEd);
        var candidates = new List<ScoredCandidate>();

        foreach (var theme in filtered)
        {
            if (theme.Entries == null)
            {
                continue;
            }

            foreach (var entry in theme.Entries)
            {
                if (entry.Videos == null)
                {
                    continue;
                }

                // Score all videos in this entry and pick the best eligible one.
                var scoredVideos = entry.Videos
                    .Select(v => (Video: v, Score: Rate(entry, v)))
                    .OrderBy(x => x.Score)
                    .ToList();

                foreach (var sv in scoredVideos)
                {
                    if (ignoreOverlaps && !string.IsNullOrEmpty(sv.Video.Overlap) &&
                        !sv.Video.Overlap.Equals("None", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (ignoreCredits && !IsCreditless(sv.Video))
                    {
                        continue;
                    }

                    candidates.Add(new ScoredCandidate(theme, entry, sv.Video, sv.Score));
                    break; // One video per entry
                }
            }
        }

        return candidates.OrderBy(c => c.Score).ToList();
    }

    /// <summary>
    /// Filters a list of themes based on OP/ED exclusion.
    /// </summary>
    /// <param name="themes">The list of themes to filter.</param>
    /// <param name="ignoreOp">Whether to ignore OP themes.</param>
    /// <param name="ignoreEd">Whether to ignore ED themes.</param>
    /// <returns>A filtered list of themes.</returns>
    public static List<AnimeThemesTheme> FilterThemes(
        List<AnimeThemesTheme> themes,
        bool ignoreOp,
        bool ignoreEd)
    {
        return themes
            .Where(t => !(ignoreOp && string.Equals(t.Type, "OP", StringComparison.OrdinalIgnoreCase)))
            .Where(t => !(ignoreEd && string.Equals(t.Type, "ED", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Selects the best video for a theme (legacy: picks best across all entries).
    /// </summary>
    /// <param name="theme">The theme to select a video for.</param>
    /// <param name="ignoreOverlaps">Whether to ignore overlapping themes.</param>
    /// <param name="ignoreCredits">Whether to ignore credits.</param>
    /// <returns>The best video for the theme, or null if none found.</returns>
    public static AnimeThemesVideo? SelectBestVideo(
        AnimeThemesTheme theme,
        bool ignoreOverlaps,
        bool ignoreCredits)
    {
        if (theme.Entries == null)
        {
            return null;
        }

        var candidates = theme.Entries
            .Where(e => e.Videos != null)
            .SelectMany(e => e.Videos!.Select(v => (Entry: e, Video: v, Score: Rate(e, v))))
            .OrderBy(x => x.Score)
            .ToList();

        foreach (var candidate in candidates)
        {
            if (ignoreOverlaps && !string.IsNullOrEmpty(candidate.Video.Overlap) &&
                !candidate.Video.Overlap.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ignoreCredits && !IsCreditless(candidate.Video))
            {
                continue;
            }

            return candidate.Video;
        }

        return null;
    }

    /// <summary>
    /// Returns a human-readable breakdown of the score for logging.
    /// </summary>
    /// <param name="entry">The theme entry.</param>
    /// <param name="video">The video.</param>
    /// <returns>Score breakdown string.</returns>
    public static string GetScoreBreakdown(AnimeThemesEntry entry, AnimeThemesVideo video)
    {
        var details = GetScoreDetails(entry, video);
        var parts = details.Select(d => $"{d.Reason}:+{d.Score}").ToList();
        return parts.Count > 0 ? string.Join(", ", parts) : "(no penalty)";
    }

    /// <summary>
    /// Rates a video entry with a penalty score (lower is better).
    /// </summary>
    /// <param name="entry">The theme entry.</param>
    /// <param name="video">The video.</param>
    /// <returns>A penalty score where lower is better.</returns>
    internal static double Rate(AnimeThemesEntry entry, AnimeThemesVideo video)
    {
        return GetScoreDetails(entry, video).Sum(d => d.Score);
    }

    private static List<(int Score, string Reason)> GetScoreDetails(AnimeThemesEntry entry, AnimeThemesVideo video)
    {
        var details = new List<(int Score, string Reason)>();

        // Spoiler
        if (entry.Spoiler == true)
        {
            details.Add((ScoreSpoiler, "Spoiler"));
        }

        // Overlap
        if (!string.IsNullOrEmpty(video.Overlap))
        {
            if (video.Overlap.Equals("Over", StringComparison.OrdinalIgnoreCase))
            {
                details.Add((ScoreOverlapOver, "Overlap(Over)"));
            }
            else if (video.Overlap.Equals("Transition", StringComparison.OrdinalIgnoreCase))
            {
                details.Add((ScoreOverlapTransition, "Overlap(Trans)"));
            }
        }

        // Source quality
        if (!string.IsNullOrEmpty(video.Source))
        {
            if (video.Source.Equals("LD", StringComparison.OrdinalIgnoreCase) ||
                video.Source.Equals("VHS", StringComparison.OrdinalIgnoreCase))
            {
                details.Add((ScoreSourceLdVhs, string.Format(CultureInfo.InvariantCulture, "Source({0})", video.Source)));
            }
            else if (video.Source.Equals("WEB", StringComparison.OrdinalIgnoreCase) ||
                     video.Source.Equals("RAW", StringComparison.OrdinalIgnoreCase))
            {
                details.Add((ScoreSourceWebRaw, string.Format(CultureInfo.InvariantCulture, "Source({0})", video.Source)));
            }
        }

        // Credits (if not creditless — creditless is preferred)
        if (!IsCreditless(video))
        {
            details.Add((ScoreCredits, "Credits"));
        }

        return details;
    }

    private static bool IsCreditless(AnimeThemesVideo video)
    {
        return video.Nc == true ||
               video.Tags?.Contains("NC", StringComparison.OrdinalIgnoreCase) == true ||
               video.Tags?.Contains("Creditless", StringComparison.OrdinalIgnoreCase) == true;
    }
}
