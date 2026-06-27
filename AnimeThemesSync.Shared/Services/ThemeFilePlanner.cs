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
    public const string DefaultExtrasFileNameFormat = "{Order}. {Theme} - {Song}";
    private const int MaxExtrasFileNameLength = 180;
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".webm", ".mkv", ".mp4", ".m4v", ".mov", ".avi", ".ts", ".mts", ".m2ts", ".ogv"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".ogg", ".oga", ".opus", ".flac", ".m4a", ".aac", ".wav", ".wma"
    };

    private static readonly HashSet<char> CrossPlatformInvalidFileNameChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex LegacyPluginFileRegex = new(@"^(OP|ED)\d+v?\d*(-video)?\.[A-Za-z0-9]{1,8}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CanonicalPluginFileRegex = new(@"^\d{2}-(OP|ED)\d+v?\d*( - .+)?\.[A-Za-z0-9]{1,8}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DefaultExtrasPluginFileRegex = new(@"^\d{2}\. (OP|ED)\d+v?\d*( - .+)?(?:-(?:other|short|scene))?\.[A-Za-z0-9]{1,8}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SeasonFilePrefixRegex = new(@"^Season \d{2} - ", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Builds all desired media and extras files for an item.
    /// </summary>
    /// <param name="anime">The AnimeThemes anime payload.</param>
    /// <param name="itemPath">The local library item path.</param>
    /// <param name="audioConfig">The audio theme configuration.</param>
    /// <param name="videoConfig">The video theme configuration.</param>
    /// <param name="extrasEnabled">Whether browseable extras should be planned.</param>
    /// <param name="extrasFileNameFormat">The browseable extras display-name format.</param>
    /// <param name="extrasFileSuffix">The server-recognized extras filename suffix.</param>
    /// <param name="fileNamePrefix">An optional prefix for every planned filename.</param>
    /// <param name="outputTarget">The logical owner and physical output root metadata.</param>
    /// <returns>The output plan for theme media and extras.</returns>
    public static ThemeOutputPlan BuildPlan(
        AnimeThemesAnime anime,
        string itemPath,
        ThemeConfig audioConfig,
        ThemeConfig videoConfig,
        bool extrasEnabled,
        string? extrasFileNameFormat = null,
        ExtrasFileSuffix extrasFileSuffix = ExtrasFileSuffix.Other,
        string? fileNamePrefix = null,
        ThemeOutputTarget? outputTarget = null)
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

        var videoFiles = BuildMediaPlans(anime.AnimeThemes, videoConfig, backdropsPath, isVideo: true, fileNamePrefix, outputTarget);
        var audioFiles = BuildMediaPlans(anime.AnimeThemes, audioConfig, themeMusicPath, isVideo: false, fileNamePrefix, outputTarget);

        if (videoConfig.UseAsTheme)
        {
            mediaFiles.AddRange(videoFiles.Select(x => x.File));
        }

        if (audioConfig.UseAsTheme)
        {
            mediaFiles.AddRange(audioFiles.Select(x => x.File));
        }

        if (extrasEnabled)
        {
            var plannedExtraNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in videoFiles)
            {
                var extrasFileName = EnsureUniqueFileName(
                    PrefixFileName(BuildExtrasFileName(item.Candidate, item.File.Order, Path.GetExtension(item.File.Path), extrasFileNameFormat, extrasFileSuffix), fileNamePrefix),
                    plannedExtraNames);
                var sourcePath = videoConfig.UseAsTheme ? item.File.Path : null;
                extraFiles.Add(BuildExtraPlan(sourcePath, item.File.Url, item.File.RequiresTranscoding, extrasPath, extrasFileName, item.Candidate, item.File.Order, fileNamePrefix, outputTarget));
            }
        }

        return new ThemeOutputPlan(mediaFiles, extraFiles, anime.AnimeThemes)
        {
            CleanupPlans = BuildCleanupPlans(itemPath, mediaFiles, extraFiles, anime.AnimeThemes)
        };
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

    /// <summary>
    /// Resolves a safe source extension from AnimeThemes metadata and the media URL.
    /// </summary>
    public static string ResolveMediaExtension(
        string? filename,
        string? basename,
        string? url,
        string fallback,
        bool isVideo)
    {
        return TryResolveMediaExtension(filename, basename, url, isVideo) ?? fallback.ToLowerInvariant();
    }

    /// <summary>Returns whether the extension is a supported local theme media type.</summary>
    public static bool IsSupportedMediaExtension(string? extension)
        => !string.IsNullOrWhiteSpace(extension) &&
           (VideoExtensions.Contains(extension) || AudioExtensions.Contains(extension));

    /// <summary>Returns the content type used when streaming local theme media.</summary>
    public static string GetMediaContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".ogg" or ".oga" => "audio/ogg",
            ".opus" => "audio/opus",
            ".flac" => "audio/flac",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".wav" => "audio/wav",
            ".wma" => "audio/x-ms-wma",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            ".mp4" or ".m4v" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".ts" or ".mts" or ".m2ts" => "video/mp2t",
            ".ogv" => "video/ogg",
            _ => "application/octet-stream"
        };
    }

    public static ThemeOutputPlan BuildSingleCandidatePlan(
        AnimeThemesAnime anime,
        ScoredCandidate candidate,
        int order,
        string itemPath,
        bool includeAudio,
        bool includeVideo,
        bool includeExtras,
        string? extrasFileNameFormat = null,
        ExtrasFileSuffix extrasFileSuffix = ExtrasFileSuffix.Other,
        string? fileNamePrefix = null,
        ThemeOutputTarget? outputTarget = null)
    {
        var mediaFiles = new List<ThemeFilePlan>();
        var extraFiles = new List<ThemeExtraPlan>();
        var themeKey = BuildThemeKey(candidate);
        var backdropsPath = Path.Combine(itemPath, "backdrops");
        var themeMusicPath = Path.Combine(itemPath, "theme-music");
        var extrasPath = Path.Combine(itemPath, "extras");

        ThemeFilePlan? videoPlan = null;
        var sourceVideoExtension = TryResolveMediaExtension(
            candidate.Video.Filename,
            candidate.Video.Basename,
            candidate.Video.Link,
            isVideo: true);
        var videoExtension = sourceVideoExtension ?? ".webm";
        if (includeVideo && !string.IsNullOrWhiteSpace(candidate.Video.Link))
        {
            videoPlan = new ThemeFilePlan(
                Path.Combine(backdropsPath, PrefixFileName(BuildVideoFileName(candidate, order, themeKey, videoExtension), fileNamePrefix)),
                candidate.Video.Link!,
                true,
                order,
                themeKey)
            {
                RequiresTranscoding = sourceVideoExtension == null,
                OutputTarget = outputTarget
            };
            mediaFiles.Add(videoPlan);
        }

        var separateAudio = !string.IsNullOrWhiteSpace(candidate.Video.Audio?.Link);
        var audioUrl = separateAudio ? candidate.Video.Audio!.Link : candidate.Video.Link;
        if (includeAudio && !string.IsNullOrWhiteSpace(audioUrl))
        {
            var sourceAudioExtension = separateAudio
                ? TryResolveMediaExtension(candidate.Video.Audio!.Filename, candidate.Video.Audio.Basename, audioUrl, isVideo: false)
                : null;
            var audioExtension = sourceAudioExtension ?? ".mp3";
            mediaFiles.Add(new ThemeFilePlan(
                Path.Combine(themeMusicPath, PrefixFileName(BuildAudioFileName(candidate, order, themeKey, audioExtension), fileNamePrefix)),
                audioUrl!,
                false,
                order,
                themeKey)
            {
                RequiresTranscoding = !separateAudio || sourceAudioExtension == null,
                OutputTarget = outputTarget
            });
        }

        if (includeExtras && !string.IsNullOrWhiteSpace(candidate.Video.Link))
        {
            var extrasFileName = PrefixFileName(BuildExtrasFileName(candidate, order, videoExtension, extrasFileNameFormat, extrasFileSuffix), fileNamePrefix);
            extraFiles.Add(BuildExtraPlan(videoPlan?.Path, candidate.Video.Link!, sourceVideoExtension == null, extrasPath, extrasFileName, candidate, order, fileNamePrefix, outputTarget));
        }

        return new ThemeOutputPlan(mediaFiles, extraFiles, anime.AnimeThemes ?? new List<AnimeThemesTheme>());
    }

    public static ThemeOutputPlan MergePlans(IEnumerable<ThemeOutputPlan> plans)
    {
        var planList = plans.ToList();
        var merged = new ThemeOutputPlan(
            planList.SelectMany(p => p.MediaFiles).ToList(),
            planList.SelectMany(p => p.ExtraFiles).ToList(),
            planList.SelectMany(p => p.Themes).ToList())
        {
            CleanupPlans = planList
                .SelectMany(p => p.CleanupPlans)
                .GroupBy(p => p.Directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparer.OrdinalIgnoreCase)
                .Select(group => new ThemeCleanupPlan(
                    group.First().Directory,
                    group.SelectMany(p => p.DesiredFiles).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    group.SelectMany(p => p.Themes).ToList()))
                .ToList()
        };

        return merged;
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

        if (!IsSupportedMediaExtension(Path.GetExtension(fileName)))
        {
            return false;
        }

        fileName = SeasonFilePrefixRegex.Replace(fileName, string.Empty, 1);

        if (fileName.StartsWith("AnimeThemes - ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (DefaultExtrasPluginFileRegex.IsMatch(fileName) ||
            CanonicalPluginFileRegex.IsMatch(fileName) ||
            LegacyPluginFileRegex.IsMatch(fileName))
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
            var regex = new Regex($"^{slug}v?\\d*(-video)?\\.[A-Za-z0-9]{{1,8}}$", RegexOptions.IgnoreCase);
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
        bool isVideo,
        string? fileNamePrefix,
        ThemeOutputTarget? outputTarget)
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

            var separateAudio = !string.IsNullOrWhiteSpace(candidate.Video.Audio?.Link);
            var link = isVideo ? candidate.Video.Link : separateAudio ? candidate.Video.Audio!.Link : candidate.Video.Link;
            if (string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            count++;
            var themeKey = BuildThemeKey(candidate);
            var sourceExtension = isVideo
                ? TryResolveMediaExtension(candidate.Video.Filename, candidate.Video.Basename, link, isVideo: true)
                : separateAudio
                    ? TryResolveMediaExtension(candidate.Video.Audio!.Filename, candidate.Video.Audio.Basename, link, isVideo: false)
                    : null;
            var extension = sourceExtension ?? (isVideo ? ".webm" : ".mp3");
            var fileName = isVideo
                ? BuildVideoFileName(candidate, count, themeKey, extension)
                : BuildAudioFileName(candidate, count, themeKey, extension);
            fileName = PrefixFileName(fileName, fileNamePrefix);

            plans.Add((new ThemeFilePlan(Path.Combine(targetDir, fileName), link, isVideo, count, themeKey)
            {
                RequiresTranscoding = sourceExtension == null,
                OutputTarget = outputTarget
            }, candidate));
        }

        return plans;
    }

    private static List<ThemeCleanupPlan> BuildCleanupPlans(
        string itemPath,
        List<ThemeFilePlan> mediaFiles,
        List<ThemeExtraPlan> extraFiles,
        List<AnimeThemesTheme> themes)
    {
        var themeMusicPath = Path.Combine(itemPath, "theme-music");
        var backdropsPath = Path.Combine(itemPath, "backdrops");
        var extrasPath = Path.Combine(itemPath, "extras");

        return
        [
            new ThemeCleanupPlan(
                themeMusicPath,
                mediaFiles.Where(f => !f.IsVideo).Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase),
                themes),
            new ThemeCleanupPlan(
                backdropsPath,
                mediaFiles.Where(f => f.IsVideo).Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase),
                themes),
            new ThemeCleanupPlan(
                extrasPath,
                extraFiles.Select(f => f.TargetPath).ToHashSet(StringComparer.OrdinalIgnoreCase),
                themes)
        ];
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

    private static string BuildAudioFileName(ScoredCandidate candidate, int order, string themeKey, string extension)
    {
        var details = BuildTitleArtistTokens(candidate);
        var tokens = new List<string>
        {
            string.Format(CultureInfo.InvariantCulture, "{0:00}-{1}", order, themeKey)
        };

        tokens.AddRange(details);

        var stem = string.Join(" - ", tokens.Where(t => !string.IsNullOrWhiteSpace(t)));
        return TruncateFileName(stem, extension, MaxExtrasFileNameLength);
    }

    private static string BuildVideoFileName(ScoredCandidate candidate, int order, string themeKey, string extension)
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
        return TruncateFileName(stem, extension, MaxExtrasFileNameLength);
    }

    private static ThemeExtraPlan BuildExtraPlan(
        string? sourcePath,
        string downloadUrl,
        bool requiresTranscoding,
        string extrasPath,
        string extrasFileName,
        ScoredCandidate candidate,
        int order,
        string? fileNamePrefix,
        ThemeOutputTarget? outputTarget)
    {
        var targetPath = Path.Combine(extrasPath, extrasFileName);
        var extension = Path.GetExtension(extrasFileName);
        var legacyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PrefixFileName(BuildLegacyRichExtrasFileName(candidate, order, extension), fileNamePrefix),
            PrefixFileName(BuildLegacyRichExtrasFileName(candidate, order, ".webm"), fileNamePrefix),
            PrefixFileName(BuildExtrasFileName(candidate, order, extension, null, ExtrasFileSuffix.None), fileNamePrefix),
            PrefixFileName(BuildExtrasFileName(candidate, order, ".webm", null, ExtrasFileSuffix.None), fileNamePrefix)
        };
        legacyNames.Remove(extrasFileName);

        return new ThemeExtraPlan(sourcePath, targetPath)
        {
            DownloadUrl = downloadUrl,
            RequiresTranscoding = requiresTranscoding,
            Key = BuildBrowserRowId(candidate),
            LegacyTargetPaths = legacyNames.Select(name => Path.Combine(extrasPath, name)).ToArray(),
            OutputTarget = outputTarget
        };
    }

    private static string PrefixFileName(string fileName, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return fileName;
        }

        var sanitizedPrefix = SanitizeFileNamePart(prefix, fallback: string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sanitizedPrefix))
        {
            return fileName;
        }

        sanitizedPrefix += " ";
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return TruncateFileName(sanitizedPrefix + stem, extension, MaxExtrasFileNameLength);
    }

    private static string EnsureUniqueFileName(string fileName, HashSet<string> usedNames)
    {
        if (usedNames.Add(fileName))
        {
            return fileName;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var index = 2; ; index++)
        {
            var suffix = string.Format(CultureInfo.InvariantCulture, " ({0})", index);
            var candidate = TruncateFileName(stem + suffix, extension, MaxExtrasFileNameLength);
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string BuildExtrasFileName(
        ScoredCandidate candidate,
        int order,
        string extension,
        string? format,
        ExtrasFileSuffix suffix)
    {
        var selectedFormat = string.IsNullOrWhiteSpace(format) ? DefaultExtrasFileNameFormat : format!;
        var tokens = BuildExtrasFormatTokens(candidate, order);
        var stem = selectedFormat;
        foreach (var token in tokens)
        {
            stem = Regex.Replace(
                stem,
                Regex.Escape("{" + token.Key + "}"),
                _ => token.Value,
                RegexOptions.IgnoreCase);
        }

        stem = Regex.Replace(stem, @"\{[A-Za-z0-9]+\}", string.Empty);
        stem = Regex.Replace(stem, @"(\s*-\s*){2,}", " - ");
        stem = Regex.Replace(stem, @"^\s*-\s*|\s*-\s*$", string.Empty);
        stem = SanitizeFileNamePart(stem, fallback: BuildThemeKey(candidate)).Trim(' ', '.', '-');
        stem += GetExtrasSuffixText(suffix);
        return TruncateFileName(stem, extension, MaxExtrasFileNameLength);
    }

    private static string GetExtrasSuffixText(ExtrasFileSuffix suffix)
    {
        return suffix switch
        {
            ExtrasFileSuffix.None => string.Empty,
            ExtrasFileSuffix.Short => "-short",
            ExtrasFileSuffix.Scene => "-scene",
            _ => "-other"
        };
    }

    private static Dictionary<string, string> BuildExtrasFormatTokens(ScoredCandidate candidate, int order)
    {
        var themeKey = BuildThemeKey(candidate);
        var episodes = string.IsNullOrWhiteSpace(candidate.Entry.Episodes)
            ? string.Empty
            : "Eps " + SanitizeFileNamePart(candidate.Entry.Episodes, fallback: string.Empty);
        var labels = BuildLabels(candidate).ToList();
        var quality = BuildQualityLabel(candidate.Video);

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Order"] = order.ToString("00", CultureInfo.InvariantCulture),
            ["Theme"] = themeKey,
            ["Type"] = SanitizeFileNamePart(candidate.Theme.Type, fallback: "Theme").ToUpperInvariant(),
            ["Sequence"] = candidate.Theme.Sequence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["Version"] = candidate.Entry.Version.HasValue && candidate.Entry.Version.Value > 1
                ? "v" + candidate.Entry.Version.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty,
            ["Song"] = SanitizeFileNamePart(candidate.Theme.Song?.Title, fallback: string.Empty),
            ["Artist"] = SanitizeFileNamePart(BuildArtistDisplay(candidate.Theme.Song), fallback: string.Empty),
            ["Episodes"] = episodes,
            ["Labels"] = labels.Count > 0 ? string.Join(" ", labels) : string.Empty,
            ["Quality"] = quality
        };
    }

    private static string BuildLegacyRichExtrasFileName(ScoredCandidate candidate, int order, string extension)
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
        return TruncateFileName(stem, extension, MaxExtrasFileNameLength);
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

    private static string? GetValidatedExtension(string? value, bool isVideo)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var extension = Path.GetExtension(value).ToLowerInvariant();
        var supported = isVideo ? VideoExtensions : AudioExtensions;
        return supported.Contains(extension) ? extension : null;
    }

    private static string? TryResolveMediaExtension(string? filename, string? basename, string? url, bool isVideo)
    {
        foreach (var value in new[] { filename, basename })
        {
            var extension = GetValidatedExtension(value, isVideo);
            if (extension != null)
            {
                return extension;
            }
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return GetValidatedExtension(Uri.UnescapeDataString(uri.AbsolutePath), isVideo);
        }

        return null;
    }

    private static string SanitizeFileNamePart(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(c => invalid.Contains(c) || CrossPlatformInvalidFileNameChars.Contains(c) ? ' ' : c).ToArray();
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
