using System;
using System.Collections.Generic;
using System.Linq;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Builds AnimeThemes Browser theme rows from a planned candidate set.
/// Extracted from the host ThemeDownloader classes so both hosts share one
/// implementation; the file system is abstracted through
/// <see cref="IThemeMediaFileSystem"/>.
/// </summary>
public static class ThemeBrowserRowBuilder
{
    /// <summary>
    /// Builds one browser row per OP/ED candidate of the anime for the given
    /// output target, flagging which planned files already exist locally.
    /// </summary>
    public static List<ThemeBrowserThemeRow> BuildRowsForPath(
        ThemeOutputTarget outputTarget,
        AnimeThemesAnime anime,
        bool includeExtras,
        string extrasFileNameFormat,
        ExtrasFileSuffix extrasFileSuffix,
        string? fileNamePrefix,
        IThemeMediaFileSystem fileSystem)
    {
        return ThemeFilePlanner.GetBrowserCandidates(anime.AnimeThemes!)
            .Select((c, index) =>
            {
                var order = index + 1;
                var plan = ThemeFilePlanner.BuildSingleCandidatePlan(
                    anime,
                    c,
                    order,
                    outputTarget.OutputRootPath,
                    includeAudio: true,
                    includeVideo: true,
                    includeExtras: includeExtras,
                    extrasFileNameFormat: extrasFileNameFormat,
                    extrasFileSuffix: extrasFileSuffix,
                    fileNamePrefix: fileNamePrefix,
                    outputTarget: outputTarget);
                return BuildRow(
                    c,
                    order,
                    anime,
                    plan.MediaFiles.FirstOrDefault(f => f.IsVideo),
                    plan.MediaFiles.FirstOrDefault(f => !f.IsVideo),
                    plan.ExtraFiles.FirstOrDefault(),
                    fileSystem);
            })
            .ToList();
    }

    /// <summary>
    /// Builds one browser row for a scored candidate and its planned files.
    /// </summary>
    public static ThemeBrowserThemeRow BuildRow(
        ScoredCandidate candidate,
        int order,
        AnimeThemesAnime anime,
        ThemeFilePlan? videoPlan,
        ThemeFilePlan? audioPlan,
        ThemeExtraPlan? extraPlan,
        IThemeMediaFileSystem fileSystem)
    {
        var audioUrl = candidate.Video.Audio?.Link ?? candidate.Video.Link;
        var labels = string.Join(", ", ThemeFilePlanner.BuildLabels(candidate));
        var animeThemesUrl = !string.IsNullOrWhiteSpace(anime.Slug)
            ? Constants.AnimeThemesWebUrl + "/anime/" + anime.Slug
            : null;

        return new ThemeBrowserThemeRow(
            ThemeFilePlanner.BuildBrowserRowId(candidate),
            order,
            candidate.Theme.Id,
            candidate.Entry.Id,
            candidate.Video.Id,
            candidate.Video.Audio?.Id,
            ThemeFilePlanner.BuildThemeKey(candidate),
            candidate.Theme.Type ?? "Theme",
            candidate.Theme.Sequence,
            candidate.Entry.Version,
            candidate.Theme.Slug,
            candidate.Theme.Group?.Name,
            candidate.Entry.Episodes,
            candidate.Entry.Spoiler == true,
            candidate.Entry.Nsfw == true,
            candidate.Entry.Notes,
            candidate.Theme.Song?.Title,
            ThemeFilePlanner.BuildArtistDisplay(candidate.Theme.Song),
            ThemeFilePlanner.BuildQualityLabel(candidate.Video),
            string.IsNullOrWhiteSpace(labels) ? null : labels,
            candidate.Video.Link,
            audioUrl,
            videoPlan?.Path,
            videoPlan != null && fileSystem.FileExists(videoPlan.Path),
            videoPlan != null && fileSystem.FileExists(videoPlan.Path),
            audioPlan?.Path,
            audioPlan != null && fileSystem.FileExists(audioPlan.Path),
            audioPlan != null && fileSystem.FileExists(audioPlan.Path),
            extraPlan?.TargetPath,
            extraPlan != null && fileSystem.FileExists(extraPlan.TargetPath),
            extraPlan != null && fileSystem.FileExists(extraPlan.TargetPath),
            animeThemesUrl);
    }
}
