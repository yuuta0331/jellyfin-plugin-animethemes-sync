using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AnimeThemesSync.Configuration;
using Jellyfin.Plugin.AnimeThemesSync.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync.ScheduledTasks;

/// <summary>
/// Scheduled task to download OP/ED themes.
/// </summary>
public class ThemeDownloader : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ThemeDownloader> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly AnimeThemesService _animeThemesService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeDownloader"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="mediaEncoder">The media encoder.</param>
    public ThemeDownloader(
        ILibraryManager libraryManager,
        IFileSystem fileSystem,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IMediaEncoder mediaEncoder)
    {
        _libraryManager = libraryManager;
        _fileSystem = fileSystem;
        _logger = loggerFactory.CreateLogger<ThemeDownloader>();
        _httpClientFactory = httpClientFactory;
        _mediaEncoder = mediaEncoder;
        var rateLimiter = new RateLimiter(loggerFactory.CreateLogger<RateLimiter>(), Constants.PluginName, 80);
        _animeThemesService = new AnimeThemesService(httpClientFactory, loggerFactory.CreateLogger<AnimeThemesService>(), rateLimiter);
    }

    /// <inheritdoc />
    public string Name => "Download Anime Themes";

    /// <inheritdoc />
    public string Key => "AnimeThemesSyncDownloader";

    /// <inheritdoc />
    public string Description => "Downloads OP/ED themes for anime in your library from AnimeThemes.moe.";

    /// <inheritdoc />
    public string Category => "Anime";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Anime Themes Download Task...");

        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.ThemeDownloadingEnabled)
        {
            _logger.LogInformation("Theme downloading is disabled in plugin configuration.");
            return;
        }

        var items = GetEnabledLibraryItems();
        _logger.LogInformation("Found {Count} items to process.", items.Count);

        // ── Phase 1: Resolve all items sequentially (API calls are rate-limited) ──
        _logger.LogInformation("=== Phase 1: Resolving themes for {Count} items ===", items.Count);

        // Each entry: (TargetPath, Url, IsVideo, Volume, ItemName)
        var allDownloads = new List<(string Path, string Url, bool IsVideo, int Volume, string ItemName)>();
        var cleanupTasks = new List<(string Directory, Dictionary<string, (string Url, bool IsVideo)> DesiredFiles, List<AnimeThemesTheme> Themes)>();

        for (var i = 0; i < items.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var item = items[i];
            var itemName = item.Name ?? "Unknown";
            _logger.LogInformation("[{Index}/{Total}] Resolving: {ItemName}", i + 1, items.Count, itemName);

            var audioConfig = CreateThemeConfig(item, config, isVideo: false);
            var videoConfig = CreateThemeConfig(item, config, isVideo: true);

            var result = await ResolveItem(item, audioConfig, videoConfig, cancellationToken).ConfigureAwait(false);
            if (result != null)
            {
                foreach (var file in result.Value.DesiredFiles)
                {
                    var volume = file.Value.IsVideo ? videoConfig.Volume : audioConfig.Volume;
                    if (config.ForceRedownload || !_fileSystem.FileExists(file.Key))
                    {
                        allDownloads.Add((file.Key, file.Value.Url, file.Value.IsVideo, volume, itemName));
                    }
                }

                // Queue cleanup tasks
                if (config.AllowDelete && result.Value.Themes != null)
                {
                    var themeMusicPath = Path.Combine(item.Path, "theme-music");
                    var backdropsPath = Path.Combine(item.Path, "backdrops");
                    cleanupTasks.Add((themeMusicPath, result.Value.DesiredFiles, result.Value.Themes));
                    cleanupTasks.Add((backdropsPath, result.Value.DesiredFiles, result.Value.Themes));
                }
            }

            progress?.Report((double)(i + 1) / items.Count * 50); // Phase 1 = 0-50%
        }

        // ── Phase 2: Download all files in parallel ──
        _logger.LogInformation("=== Phase 2: Downloading {Count} files (MaxConcurrent={Max}) ===", allDownloads.Count, config.MaxConcurrentDownloads);

        if (allDownloads.Count > 0)
        {
            var throttler = new SemaphoreSlim(config.MaxConcurrentDownloads > 0 ? config.MaxConcurrentDownloads : 1);
            var downloaded = 0;
            var downloadTasks = new List<Task>();

            foreach (var dl in allDownloads)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);

                downloadTasks.Add(Task.Run(
                    async () =>
                    {
                        try
                        {
                            var dir = Path.GetDirectoryName(dl.Path);
                            if (dir != null && !_fileSystem.DirectoryExists(dir))
                            {
                                _ = Directory.CreateDirectory(dir);
                            }

                            const int MaxRetries = 3;
                            for (var attempt = 1; attempt <= MaxRetries; attempt++)
                            {
                                try
                                {
                                    _logger.LogDebug("Downloading [{ItemName}] {Filename}...", dl.ItemName, Path.GetFileName(dl.Path));
                                    await DownloadFile(dl.Url, dl.Path, dl.Volume, cancellationToken).ConfigureAwait(false);
                                    _logger.LogInformation("Downloaded [{ItemName}] {Filename}", dl.ItemName, Path.GetFileName(dl.Path));
                                    break;
                                }
                                catch (OperationCanceledException)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    if (attempt < MaxRetries)
                                    {
                                        _logger.LogWarning(ex, "Download attempt {Attempt}/{MaxRetries} failed for {Url}. Retrying...", attempt, MaxRetries, dl.Url);
                                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        _logger.LogError(ex, "Download failed after {MaxRetries} attempts for {Url}. Skipping file.", MaxRetries, dl.Url);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            throttler.Release();
                            var currentCount = Interlocked.Increment(ref downloaded);
                            progress?.Report(50 + ((double)currentCount / allDownloads.Count * 50)); // Phase 2 = 50-100%
                        }
                    },
                    cancellationToken));
            }

            await Task.WhenAll(downloadTasks).ConfigureAwait(false);
        }

        // ── Cleanup ──
        foreach (var cleanup in cleanupTasks)
        {
            CleanupDirectory(cleanup.Directory, cleanup.DesiredFiles, cleanup.Themes);
        }

        _logger.LogInformation("Anime Themes Download Task Completed. Downloaded {Count} files.", allDownloads.Count);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromDays(7).Ticks
            }
        };
    }

    /// <summary>
    /// Gets all library items from enabled libraries.
    /// </summary>
    private List<BaseItem> GetEnabledLibraryItems()
    {
        var root = _libraryManager.RootFolder;
        var enabledFolderIds = new HashSet<Guid>();

        foreach (var child in root.Children)
        {
            if (child is Folder folder)
            {
                var options = _libraryManager.GetLibraryOptions(folder);
                if (options?.TypeOptions == null)
                {
                    _logger.LogWarning("Could not get LibraryOptions for folder: {LibraryName}", folder.Name);
                    continue;
                }

                foreach (var typeOption in options.TypeOptions)
                {
                    _logger.LogInformation(
                        "Library {LibraryName} ({Type}): Fetchers={Fetchers}",
                        folder.Name,
                        typeOption.Type,
                        typeOption.MetadataFetchers != null ? string.Join(",", typeOption.MetadataFetchers) : "null");
                }

                var isEnabled = options.TypeOptions.Any(t =>
                    (string.Equals(t.Type, "Series", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(t.Type, "Movie", StringComparison.OrdinalIgnoreCase)) &&
                    t.MetadataFetchers != null &&
                    (t.MetadataFetchers.Contains(Constants.MetadataProviderName) ||
                     t.MetadataFetchers.Contains(Plugin.Instance?.Name) ||
                     t.MetadataFetchers.Contains(Constants.PluginName)));

                if (isEnabled)
                {
                    _logger.LogInformation("AnimeThemesSync is enabled for library: {LibraryName}", folder.Name);
                    enabledFolderIds.Add(folder.Id);
                }
                else
                {
                    _logger.LogWarning("AnimeThemesSync is NOT enabled for library: {LibraryName}.", folder.Name);
                }
            }
        }

        var items = new List<BaseItem>();
        foreach (var folderId in enabledFolderIds)
        {
            var folder = _libraryManager.GetItemById(folderId) as Folder;
            if (folder != null)
            {
                var folderItems = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series, BaseItemKind.Movie },
                    Recursive = true,
                    Parent = folder
                });
                items.AddRange(folderItems);
            }
        }

        return items;
    }

    /// <summary>
    /// Creates a ThemeConfig for the given item based on the plugin configuration.
    /// </summary>
    private static ThemeConfig CreateThemeConfig(BaseItem item, PluginConfiguration config, bool isVideo)
    {
        if (item is Series)
        {
            return isVideo
                ? new ThemeConfig
                {
                    MaxThemes = config.SeriesVideoMaxThemes,
                    Volume = config.SeriesVideoVolume,
                    IgnoreOp = config.SeriesVideoIgnoreOp,
                    IgnoreEd = config.SeriesVideoIgnoreEd,
                    IgnoreOverlaps = config.SeriesVideoIgnoreOverlaps,
                    IgnoreCredits = config.SeriesVideoIgnoreCredits,
                }
                : new ThemeConfig
                {
                    MaxThemes = config.SeriesAudioMaxThemes,
                    Volume = config.SeriesAudioVolume,
                    IgnoreOp = config.SeriesAudioIgnoreOp,
                    IgnoreEd = config.SeriesAudioIgnoreEd,
                    IgnoreOverlaps = config.SeriesAudioIgnoreOverlaps,
                    IgnoreCredits = config.SeriesAudioIgnoreCredits,
                };
        }

        // Movie
        return isVideo
            ? new ThemeConfig
            {
                MaxThemes = config.MovieVideoMaxThemes,
                Volume = config.MovieVideoVolume,
                IgnoreOp = config.MovieVideoIgnoreOp,
                IgnoreEd = config.MovieVideoIgnoreEd,
                IgnoreOverlaps = config.MovieVideoIgnoreOverlaps,
                IgnoreCredits = config.MovieVideoIgnoreCredits,
            }
            : new ThemeConfig
            {
                MaxThemes = config.MovieAudioMaxThemes,
                Volume = config.MovieAudioVolume,
                IgnoreOp = config.MovieAudioIgnoreOp,
                IgnoreEd = config.MovieAudioIgnoreEd,
                IgnoreOverlaps = config.MovieAudioIgnoreOverlaps,
                IgnoreCredits = config.MovieAudioIgnoreCredits,
            };
    }

    /// <summary>
    /// Resolves a single library item to determine its desired theme files.
    /// Returns null if the item cannot be resolved.
    /// </summary>
    private async Task<(Dictionary<string, (string Url, bool IsVideo)> DesiredFiles, List<AnimeThemesTheme> Themes)?> ResolveItem(
        BaseItem item,
        ThemeConfig audioConfig,
        ThemeConfig videoConfig,
        CancellationToken cancellationToken)
    {
        if (audioConfig.MaxThemes <= 0 && videoConfig.MaxThemes <= 0)
        {
            return null;
        }

        // Resolve IDs
        int? aniListId = null;
        int? malId = null;

        if (item.ProviderIds.TryGetValue("AniList", out var aniListIdStr) && int.TryParse(aniListIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aid))
        {
            aniListId = aid;
        }

        if (item.ProviderIds.TryGetValue("MyAnimeList", out var malIdStr) && int.TryParse(malIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mid))
        {
            malId = mid;
        }

        if (aniListId == null && malId == null)
        {
            _logger.LogWarning("  No AniList or MAL ID found for {ItemName}. Skipping.", item.Name);
            return null;
        }

        // Get AnimeThemes Data
        AnimeThemesAnime? anime = null;
        if (item.ProviderIds.TryGetValue("AnimeThemes", out var animeThemesSlug) && !string.IsNullOrEmpty(animeThemesSlug))
        {
            anime = await _animeThemesService.GetAnimeBySlug(animeThemesSlug, cancellationToken).ConfigureAwait(false);
        }

        if (anime == null)
        {
            if (aniListId.HasValue)
            {
                anime = await _animeThemesService.GetAnimeByExternalId("anilist", aniListId.Value, cancellationToken).ConfigureAwait(false);
            }

            if (anime == null && malId.HasValue)
            {
                anime = await _animeThemesService.GetAnimeByExternalId("myanimelist", malId.Value, cancellationToken).ConfigureAwait(false);
            }
        }

        if (anime?.AnimeThemes == null)
        {
            _logger.LogWarning("  No themes found for {ItemName}.", item.Name);
            return null;
        }

        var backdropsPath = Path.Combine(item.Path, "backdrops");
        var themeMusicPath = Path.Combine(item.Path, "theme-music");

        // Determine desired files: Key = Destination Path, Value = (URL, IsVideo)
        var desiredFiles = new Dictionary<string, (string Url, bool IsVideo)>();

        CollectDesiredFiles(desiredFiles, anime, videoConfig, backdropsPath, isVideo: true);
        CollectDesiredFiles(desiredFiles, anime, audioConfig, themeMusicPath, isVideo: false);

        return (desiredFiles, anime.AnimeThemes);
    }

    /// <summary>
    /// Collects desired files for either audio or video themes, iterating at the entry level.
    /// </summary>
    private void CollectDesiredFiles(
        Dictionary<string, (string Url, bool IsVideo)> desiredFiles,
        AnimeThemesAnime anime,
        ThemeConfig config,
        string targetDir,
        bool isVideo)
    {
        if (config.MaxThemes <= 0 || anime.AnimeThemes == null)
        {
            return;
        }

        var mediaType = isVideo ? "Video" : "Audio";

        // Get all scored candidates at the entry level
        var candidates = ThemeScoringService.GetScoredCandidates(
            anime.AnimeThemes,
            config.IgnoreOp,
            config.IgnoreEd,
            config.IgnoreOverlaps,
            config.IgnoreCredits);

        // Log all candidates for debugging
        _logger.LogInformation(
            "[{MediaType}] {Count} candidate entries found (MaxThemes={Max})",
            mediaType,
            candidates.Count,
            config.MaxThemes);

        foreach (var c in candidates)
        {
            var versionLabel = c.Entry.Version > 1
                ? string.Format(CultureInfo.InvariantCulture, "v{0}", c.Entry.Version)
                : "v1";

            _logger.LogInformation(
                "  [{MediaType}] {Slug} {Version} | {Basename} | Score={Score} ({Breakdown})",
                mediaType,
                c.Theme.Slug ?? c.Theme.Type,
                versionLabel,
                c.Video.Basename ?? "?",
                c.Score,
                ThemeScoringService.GetScoreBreakdown(c.Entry, c.Video));
        }

        // Select up to MaxThemes entries
        var count = 0;
        foreach (var candidate in candidates)
        {
            if (count >= config.MaxThemes)
            {
                break;
            }

            var slug = candidate.Theme.Slug ?? candidate.Theme.Type;
            var versionSuffix = candidate.Entry.Version > 1
                ? string.Format(CultureInfo.InvariantCulture, "v{0}", candidate.Entry.Version)
                : string.Empty;

            string? link;
            string filename;
            if (isVideo)
            {
                link = candidate.Video.Link;
                filename = $"{slug}{versionSuffix}-video.webm";
            }
            else
            {
                link = candidate.Video.Audio?.Link ?? candidate.Video.Link;
                filename = $"{slug}{versionSuffix}.mp3";
            }

            if (!string.IsNullOrEmpty(link))
            {
                var path = Path.Combine(targetDir, filename);
                if (!desiredFiles.ContainsKey(path))
                {
                    desiredFiles.Add(path, (link!, isVideo));
                    count++;

                    _logger.LogInformation(
                        "  → Selected [{MediaType}] {Filename} (Score={Score})",
                        mediaType,
                        filename,
                        candidate.Score);
                }
            }
        }
    }

    /// <summary>
    /// Cleans up files from a directory that are no longer desired.
    /// </summary>
    private void CleanupDirectory(
        string directory,
        Dictionary<string, (string Url, bool IsVideo)> desiredFiles,
        List<AnimeThemesTheme> themes)
    {
        if (!_fileSystem.DirectoryExists(directory))
        {
            return;
        }

        foreach (var file in _fileSystem.GetFilePaths(directory))
        {
            if (desiredFiles.ContainsKey(file))
            {
                continue;
            }

            var filename = Path.GetFileName(file);
            if (themes.Any(t => !string.IsNullOrEmpty(t.Slug) && filename.Contains(t.Slug, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("Deleting unwanted theme file: {Path}", file);
                _fileSystem.DeleteFile(file);
            }
        }
    }

    /// <summary>
    /// Downloads a file from a given URL to a specified path.
    /// </summary>
    private async Task DownloadFile(string url, string path, int volume, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        var timeoutSeconds = config?.DownloadTimeoutSeconds > 0 ? config.DownloadTimeoutSeconds : 600;

        var client = _httpClientFactory.CreateClient("AnimeThemes");
        if (!client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
        }

        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        var tempPath = path + ".part";

        try
        {
            using var response = await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                await stream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogError("Download failed with status code {StatusCode} for {Url}", response.StatusCode, url);
                return;
            }
        }
        catch (Exception)
        {
            CleanupTempFile(tempPath);
            throw;
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        var isVideo = extension == ".webm";
        var needsConversion = !isVideo && !url.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
        var needsVolume = volume < 100;

        if (needsConversion || needsVolume)
        {
            // Use ffmpeg: convert to target format and/or adjust volume
            try
            {
                await FfmpegProcess(tempPath, path, volume, isVideo, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CleanupTempFile(tempPath);
            }
        }
        else
        {
            // Already correct format, no volume change — just move
            try
            {
                File.Move(tempPath, path, overwrite: true);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to move temp file {TempPath} to {FinalPath}", tempPath, path);
                CleanupTempFile(tempPath);
                throw;
            }
        }
    }

    private static void CleanupTempFile(string tempPath)
    {
        if (File.Exists(tempPath))
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Ignore delete errors
            }
        }
    }

    /// <summary>
    /// Runs ffmpeg to convert format and/or adjust volume.
    /// Input is the temp file (.part), output is the final path.
    /// </summary>
    private async Task FfmpegProcess(string inputPath, string outputPath, int volume, bool isVideo, CancellationToken cancellationToken)
    {
        var encoderPath = _mediaEncoder.EncoderPath;
        if (string.IsNullOrEmpty(encoderPath))
        {
            _logger.LogWarning("FFmpeg not found. Copying raw file without conversion.");
            File.Move(inputPath, outputPath, true);
            return;
        }

        var extension = Path.GetExtension(outputPath).ToLowerInvariant();
        string args;

        if (isVideo)
        {
            // Video: copy video stream, adjust audio volume
            if (volume <= 0)
            {
                args = $"-i \"{inputPath}\" -c:v copy -an \"{outputPath}\"";
            }
            else if (volume < 100)
            {
                var volStr = (volume / 100.0).ToString("F2", CultureInfo.InvariantCulture);
                args = $"-i \"{inputPath}\" -c:v copy -filter:a \"volume={volStr}\" -c:a libvorbis \"{outputPath}\"";
            }
            else
            {
                args = $"-i \"{inputPath}\" -c copy \"{outputPath}\"";
            }
        }
        else
        {
            // Audio: convert to MP3 if needed, adjust volume if needed
            var volFilter = volume <= 0
                ? "-filter:a \"volume=0\""
                : volume < 100
                    ? $"-filter:a \"volume={(volume / 100.0).ToString("F2", CultureInfo.InvariantCulture)}\""
                    : string.Empty;

            args = extension == ".mp3"
                ? $"-i \"{inputPath}\" {volFilter} -codec:a libmp3lame -q:a 2 \"{outputPath}\""
                : $"-i \"{inputPath}\" {volFilter} \"{outputPath}\"";
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = encoderPath,
            Arguments = $"-y {args}".Trim(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = processStartInfo };
            _logger.LogInformation("Running ffmpeg: {Arguments}", processStartInfo.Arguments);

            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                _logger.LogInformation("ffmpeg OK: {Output}", Path.GetFileName(outputPath));
            }
            else
            {
                _logger.LogError("FFmpeg failed (exit={ExitCode}): {Stderr}", process.ExitCode, stderr);

                // Fallback: use raw file
                File.Move(inputPath, outputPath, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running ffmpeg.");
            if (!File.Exists(outputPath))
            {
                File.Move(inputPath, outputPath, true);
            }
        }
    }
}
