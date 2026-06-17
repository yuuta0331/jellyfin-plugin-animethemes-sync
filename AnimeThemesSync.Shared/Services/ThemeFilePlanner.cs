using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Builds local file plans and display-oriented filenames for AnimeThemes media.
/// </summary>
public static class ThemeFilePlanner
{
    private const int MaxExtrasFileNameLength = 180;
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex LegacyPluginFileRegex = new(@"^(OP|ED)\d+v?\d*(-video)?\.(webm|mp3)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CanonicalPluginFileRegex = new(@"^\d{2}-(OP|ED)\d+v?\d*( - .+)?\.(webm|mp3)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Builds all desired media and extras files for an item.
    /// </summary>
    /// <param name="anime">The AnimeThemes anime payload.</param>
    /// <param name="itemPath">The local library item path.</param>
    /// <param name="audioConfig">The audio theme configuration.</param>
    /// <param name="videoConfig">The video theme configuration.</param>
    /// <param name="extrasEnabled">Whether browseable extras should be planned.</param>
    /// <returns>The output plan for theme media and extras.</returns>
    public static ThemeOutputPlan BuildPlan(
        AnimeThemesAnime anime,
        string itemPath,
        ThemeConfig audioConfig,
        ThemeConfig videoConfig,
        bool extrasEnabled)
    {
        var mediaFiles = new List<ThemeFilePlan>();
        var extraFiles = new List<ThemeExtraPlan>();

        if (anime.AnimeThemes == null)
        {
            return new ThemeOutputPlan(mediaFiles, extraFiles, new List<AnimeThemesTheme>());
        }

        var backdropsPath = Path.Combine(itemPath, "backdrops");
        var themeMusicPath = Path.Combine(itemPath, "theme-music");
        var extrasPath = Path.Combine(itemPath, "extras");

        var videoFiles = BuildMediaPlans(anime.AnimeThemes, videoConfig, backdropsPath, isVideo: true);
        var audioFiles = BuildMediaPlans(anime.AnimeThemes, audioConfig, themeMusicPath, isVideo: false);

        mediaFiles.AddRange(videoFiles.Select(x => x.File));
        mediaFiles.AddRange(audioFiles.Select(x => x.File));

        if (extrasEnabled)
        {
            foreach (var item in videoFiles)
            {
                var extrasFileName = BuildExtrasFileName(item.Candidate, item.File.Order);
                extraFiles.Add(new ThemeExtraPlan(item.File.Path, Path.Combine(extrasPath, extrasFileName)));
            }
        }

        return new ThemeOutputPlan(mediaFiles, extraFiles, anime.AnimeThemes);
    }

    public static List<ScoredCandidate> GetBrowserCandidates(IEnumerable<AnimeThemesTheme> themes)
    {
        return ThemeScoringService.GetScoredCandidates(themes.ToList(), ignoreOp: false, ignoreEd: false, ignoreOverlaps: false, ignoreCredits: false)
            .OrderBy(GetThemeTypeOrder)
            .ThenBy(c => c.Theme.Sequence ?? int.MaxValue)
            .ThenBy(c => c.Entry.Version ?? 1)
            .ThenBy(c => c.Theme.Slug ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Score)
            .ToList();
    }

    public static ThemeOutputPlan BuildSingleCandidatePlan(
        AnimeThemesAnime anime,
        ScoredCandidate candidate,
        int order,
        string itemPath,
        bool includeAudio,
        bool includeVideo,
        bool includeExtras)
    {
        var mediaFiles = new List<ThemeFilePlan>();
        var extraFiles = new List<ThemeExtraPlan>();
        var themeKey = BuildThemeKey(candidate);
        var backdropsPath = Path.Combine(itemPath, "backdrops");
        var themeMusicPath = Path.Combine(itemPath, "theme-music");
        var extrasPath = Path.Combine(itemPath, "extras");

        ThemeFilePlan? videoPlan = null;
        if (includeVideo && !string.IsNullOrWhiteSpace(candidate.Video.Link))
        {
            videoPlan = new ThemeFilePlan(
                Path.Combine(backdropsPath, BuildVideoFileName(candidate, order, themeKey)),
                candidate.Video.Link!,
                true,
                order,
                themeKey);
            mediaFiles.Add(videoPlan);
        }

        var audioUrl = candidate.Video.Audio?.Link ?? candidate.Video.Link;
        if (includeAudio && !string.IsNullOrWhiteSpace(audioUrl))
        {
            mediaFiles.Add(new ThemeFilePlan(
                Path.Combine(themeMusicPath, BuildAudioFileName(candidate, order, themeKey)),
                audioUrl!,
                false,
                order,
                themeKey));
        }

        if (includeExtras && videoPlan != null)
        {
            extraFiles.Add(new ThemeExtraPlan(
                videoPlan.Path,
                Path.Combine(extrasPath, BuildExtrasFileName(candidate, order))));
        }

        return new ThemeOutputPlan(mediaFiles, extraFiles, anime.AnimeThemes ?? new List<AnimeThemesTheme>());
    }

    public static string BuildBrowserRowId(ScoredCandidate candidate)
    {
        if (candidate.Theme.Id > 0 && candidate.Entry.Id > 0 && candidate.Video.Id > 0)
        {
            var audioId = candidate.Video.Audio?.Id;
            return string.Format(
                CultureInfo.InvariantCulture,
                "t{0}-e{1}-v{2}-a{3}",
                candidate.Theme.Id,
                candidate.Entry.Id,
                candidate.Video.Id,
                audioId.HasValue && audioId.Value > 0 ? audioId.Value.ToString(CultureInfo.InvariantCulture) : "0");
        }

        var source = string.Join(
            "|",
            BuildThemeKey(candidate),
            candidate.Entry.Version?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            candidate.Video.Link ?? string.Empty,
            candidate.Video.Audio?.Link ?? string.Empty);
        return "h" + ComputeStableHash(source);
    }

    /// <summary>
    /// Determines whether a file should be considered plugin-owned for cleanup.
    /// </summary>
    /// <param name="filePath">The file path to inspect.</param>
    /// <param name="themes">The AnimeThemes themes for the item.</param>
    /// <returns><c>true</c> when the file matches plugin-owned naming.</returns>
    public static bool IsPluginOwnedFile(string filePath, IEnumerable<AnimeThemesTheme> themes)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.StartsWith("AnimeThemes - ", StringComparison.OrdinalIgnoreCase) &&
            fileName.EndsWith(".webm", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (CanonicalPluginFileRegex.IsMatch(fileName) || LegacyPluginFileRegex.IsMatch(fileName))
        {
            return true;
        }

        foreach (var theme in themes)
        {
            if (string.IsNullOrWhiteSpace(theme.Slug))
            {
                continue;
            }

            var slug = Regex.Escape(theme.Slug);
            var regex = new Regex($"^{slug}v?\\d*(-video)?\\.(webm|mp3)$", RegexOptions.IgnoreCase);
            if (regex.IsMatch(fileName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds the compact theme key used in stable theme-media filenames.
    /// </summary>
    /// <param name="candidate">The selected theme candidate.</param>
    /// <returns>The compact OP/ED key.</returns>
    public static string BuildThemeKey(ScoredCandidate candidate)
    {
        var type = string.IsNullOrWhiteSpace(candidate.Theme.Type) ? "Theme" : candidate.Theme.Type.Trim().ToUpperInvariant();
        var baseKey = !string.IsNullOrWhiteSpace(candidate.Theme.Slug)
            ? candidate.Theme.Slug.Trim()
            : candidate.Theme.Sequence.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0}{1}", type, candidate.Theme.Sequence.Value)
                : type;

        if (candidate.Entry.Version > 1 && !baseKey.EndsWith($"v{candidate.Entry.Version.Value}", StringComparison.OrdinalIgnoreCase))
        {
            baseKey += string.Format(CultureInfo.InvariantCulture, "v{0}", candidate.Entry.Version.Value);
        }

        return SanitizeFileNamePart(baseKey, fallback: type);
    }

    private static List<(ThemeFilePlan File, ScoredCandidate Candidate)> BuildMediaPlans(
        List<AnimeThemesTheme> themes,
        ThemeConfig config,
        string targetDir,
        bool isVideo)
    {
        var plans = new List<(ThemeFilePlan File, ScoredCandidate Candidate)>();
        if (config.MaxThemes <= 0)
        {
            return plans;
        }

        var candidates = ThemeScoringService.GetScoredCandidates(
                themes,
                config.IgnoreOp,
                config.IgnoreEd,
                config.IgnoreOverlaps,
                config.IgnoreCredits)
            .OrderBy(GetThemeTypeOrder)
            .ThenBy(c => c.Theme.Sequence ?? int.MaxValue)
            .ThenBy(c => c.Entry.Version ?? 1)
            .ThenBy(c => c.Theme.Slug ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Score)
            .ToList();

        var count = 0;
        foreach (var candidate in candidates)
        {
            if (count >= config.MaxThemes)
            {
                break;
            }

            var link = isVideo ? candidate.Video.Link : candidate.Video.Audio?.Link ?? candidate.Video.Link;
            if (string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            count++;
            var themeKey = BuildThemeKey(candidate);
            var fileName = isVideo
                ? BuildVideoFileName(candidate, count, themeKey)
                : BuildAudioFileName(candidate, count, themeKey);

            plans.Add((new ThemeFilePlan(Path.Combine(targetDir, fileName), link, isVideo, count, themeKey), candidate));
        }

        return plans;
    }

    private static int GetThemeTypeOrder(ScoredCandidate candidate)
    {
        if (string.Equals(candidate.Theme.Type, "OP", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(candidate.Theme.Type, "ED", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static string BuildAudioFileName(ScoredCandidate candidate, int order, string themeKey)
    {
        var details = BuildTitleArtistTokens(candidate);
        var tokens = new List<string>
        {
            string.Format(CultureInfo.InvariantCulture, "{0:00}-{1}", order, themeKey)
        };

        tokens.AddRange(details);

        var stem = string.Join(" - ", tokens.Where(t => !string.IsNullOrWhiteSpace(t)));
        return TruncateFileName(stem, ".mp3", MaxExtrasFileNameLength);
    }

    private static string BuildVideoFileName(ScoredCandidate candidate, int order, string themeKey)
    {
        var tokens = new List<string>
        {
            string.Format(CultureInfo.InvariantCulture, "{0:00}-{1}", order, themeKey)
        };

        tokens.AddRange(BuildTitleArtistTokens(candidate));

        var variant = BuildCompactVariantLabel(candidate);
        if (!string.IsNullOrWhiteSpace(variant))
        {
            tokens.Add(variant);
        }

        var stem = string.Join(" - ", tokens.Where(t => !string.IsNullOrWhiteSpace(t)));
        return TruncateFileName(stem, ".webm", MaxExtrasFileNameLength);
    }

    private static string BuildExtrasFileName(ScoredCandidate candidate, int order)
    {
        var themeKey = BuildThemeKey(candidate);
        var tokens = new List<string>
        {
            "AnimeThemes",
            order.ToString("00", CultureInfo.InvariantCulture),
            themeKey
        };

        tokens.AddRange(BuildTitleArtistTokens(candidate));

        if (!string.IsNullOrWhiteSpace(candidate.Entry.Episodes))
        {
            tokens.Add("Eps " + SanitizeFileNamePart(candidate.Entry.Episodes, fallback: string.Empty));
        }

        var labels = BuildLabels(candidate).ToList();
        if (labels.Count > 0)
        {
            tokens.Add(string.Join(" ", labels));
        }

        var stem = string.Join(" - ", tokens.Where(t => !string.IsNullOrWhiteSpace(t)));
        return TruncateFileName(stem, ".webm", MaxExtrasFileNameLength);
    }

    private static List<string> BuildTitleArtistTokens(ScoredCandidate candidate)
    {
        var tokens = new List<string>();
        var songTitle = SanitizeFileNamePart(candidate.Theme.Song?.Title, fallback: string.Empty);
        var artists = SanitizeFileNamePart(BuildArtistDisplay(candidate.Theme.Song), fallback: string.Empty);

        if (!string.IsNullOrWhiteSpace(songTitle))
        {
            tokens.Add(songTitle);
        }

        if (!string.IsNullOrWhiteSpace(artists))
        {
            tokens.Add(artists);
        }

        return tokens;
    }

    private static string BuildCompactVariantLabel(ScoredCandidate candidate)
    {
        var tokens = new List<string>();
        if (!string.IsNullOrWhiteSpace(candidate.Entry.Episodes))
        {
            tokens.Add("Eps " + SanitizeFileNamePart(candidate.Entry.Episodes, fallback: string.Empty));
        }

        var labels = BuildLabels(candidate).ToList();
        if (labels.Count > 0)
        {
            tokens.Add(string.Join(" ", labels));
        }

        return string.Join(" - ", tokens.Where(t => !string.IsNullOrWhiteSpace(t)));
    }

    public static string BuildArtistDisplay(AnimeThemesSong? song)
    {
        if (song == null)
        {
            return string.Empty;
        }

        var performanceArtists = song.Performances?
            .Where(p => p.Artist != null && !string.IsNullOrWhiteSpace(p.Artist.Name))
            .OrderBy(p => p.Relevance ?? int.MaxValue)
            .Select(p => p.Artist!.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (performanceArtists?.Count > 0)
        {
            return string.Join(", ", performanceArtists);
        }

        var artists = song.Artists?
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => a.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return artists?.Count > 0 ? string.Join(", ", artists) : string.Empty;
    }

    public static IEnumerable<string> BuildLabels(ScoredCandidate candidate)
    {
        if (candidate.Video.Nc == true || ContainsTag(candidate.Video.Tags, "NC") || ContainsTag(candidate.Video.Tags, "Creditless"))
        {
            yield return "NC";
        }

        if (candidate.Entry.Spoiler == true)
        {
            yield return "Spoiler";
        }

        if (candidate.Entry.Nsfw == true)
        {
            yield return "NSFW";
        }

        if (candidate.Video.Subbed == true)
        {
            yield return "Subbed";
        }

        if (candidate.Video.Lyrics == true)
        {
            yield return "Lyrics";
        }

        if (candidate.Video.Uncen == true)
        {
            yield return "Uncen";
        }

        var quality = BuildQualityLabel(candidate.Video);
        if (!string.IsNullOrWhiteSpace(quality))
        {
            yield return quality;
        }
    }

    public static string BuildQualityLabel(AnimeThemesVideo video)
    {
        var source = SanitizeFileNamePart(video.Source, fallback: string.Empty).ToUpperInvariant();
        var resolution = video.Resolution.HasValue
            ? video.Resolution.Value.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(resolution))
        {
            return source + resolution;
        }

        return !string.IsNullOrWhiteSpace(source) ? source : resolution;
    }

    private static bool ContainsTag(string? tags, string value)
    {
        return !string.IsNullOrWhiteSpace(tags) &&
               tags.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileNamePart(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(c => invalid.Contains(c) || c == '/' || c == '\\' ? ' ' : c).ToArray();
        var sanitized = WhitespaceRegex.Replace(new string(chars), " ").Trim(' ', '.');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string TruncateFileName(string stem, string extension, int maxLength)
    {
        var maxStemLength = Math.Max(1, maxLength - extension.Length);
        if (stem.Length <= maxStemLength)
        {
            return stem + extension;
        }

        return stem[..maxStemLength].Trim(' ', '.', '-') + extension;
    }

    private static string ComputeStableHash(string value)
    {
#pragma warning disable CA1850
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
#pragma warning restore CA1850
        var builder = new StringBuilder(16);
        for (var i = 0; i < 8 && i < hash.Length; i++)
        {
            builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
