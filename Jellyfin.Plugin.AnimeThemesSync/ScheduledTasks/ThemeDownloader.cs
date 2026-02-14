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
using Jellyfin.Plugin.AnimeThemesSync;
using Jellyfin.Plugin.AnimeThemesSync.Configuration;
using Jellyfin.Plugin.AnimeThemesSync.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync.ScheduledTasks
{
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
            var rateLimiter = new RateLimiter(loggerFactory.CreateLogger<RateLimiter>(), "AnimeThemes", 80);
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

            // 1. Identify enabled libraries
            var root = _libraryManager.RootFolder;
            var enabledFolderIds = new HashSet<Guid>();

            foreach (var child in root.Children)
            {
                if (child is Folder folder)
                {
                    // Check if this plugin is enabled as a metadata fetcher for this folder (Series or Movie)
                    var options = _libraryManager.GetLibraryOptions(folder);
                    if (options != null && options.TypeOptions != null)
                    {
                        foreach (var typeOption in options.TypeOptions)
                        {
                            _logger.LogInformation("Library {LibraryName} ({Type}): Fetchers={Fetchers}", folder.Name, typeOption.Type, typeOption.MetadataFetchers != null ? string.Join(",", typeOption.MetadataFetchers) : "null");
                        }

                        var isEnabled = options.TypeOptions.Any(t =>
                            (string.Equals(t.Type, "Series", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(t.Type, "Movie", StringComparison.OrdinalIgnoreCase)) &&
                            t.MetadataFetchers != null &&
                            (t.MetadataFetchers.Contains(Plugin.Instance?.Name) || t.MetadataFetchers.Contains("AnimeThemes Sync")));

                        if (isEnabled)
                        {
                            _logger.LogInformation("AnimeThemesSync is enabled for library: {LibraryName}", folder.Name);
                            enabledFolderIds.Add(folder.Id);
                        }
                        else
                        {
                            _logger.LogWarning("AnimeThemesSync is NOT enabled for library: {LibraryName}. Plugin Name: {PluginName}", folder.Name, Plugin.Instance?.Name);
                            // Fallback: If user didn't enable it but expects it to work?
                            // Maybe we should check if metadata fetchers are empty?
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not get LibraryOptions for folder: {LibraryName}", folder.Name);
                        // Fallback?
                    }
                }
            }

            // 2. Get Items
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

            var throttler = new SemaphoreSlim(config.MaxConcurrentDownloads > 0 ? config.MaxConcurrentDownloads : 1);
            var total = items.Count;
            var current = 0;

            var tasks = new List<Task>();

            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);

                tasks.Add(Task.Run(
                    async () =>
                    {
                        try
                        {
                            if (item is Series series)
                            {
                                await ProcessSeriesItem(series, config, cancellationToken).ConfigureAwait(false);
                            }
                            else if (item is Movie movie)
                            {
                                await ProcessMovieItem(movie, config, cancellationToken).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            throttler.Release();
                            var currentCount = Interlocked.Increment(ref current);
                            if (total > 0)
                            {
                                progress?.Report((double)currentCount / total * 100);
                            }
                        }
                    },
                    cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            _logger.LogInformation("Anime Themes Download Task Completed.");
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
        /// Processes a single Series item to download its themes.
        /// </summary>
        /// <param name="series">The series to process.</param>
        /// <param name="config">The global plugin configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task ProcessSeriesItem(Series series, PluginConfiguration config, CancellationToken cancellationToken)
        {
            await ProcessItemInternal(
                series,
                config.SeriesAudioMaxThemes,
                config.SeriesAudioVolume,
                config.SeriesAudioIgnoreOp,
                config.SeriesAudioIgnoreEd,
                config.SeriesAudioIgnoreOverlaps,
                config.SeriesAudioIgnoreCredits,
                config.SeriesVideoMaxThemes,
                config.SeriesVideoVolume,
                config.SeriesVideoIgnoreOp,
                config.SeriesVideoIgnoreEd,
                config.SeriesVideoIgnoreOverlaps,
                config.SeriesVideoIgnoreCredits,
                config,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Processes a single Movie item to download its themes.
        /// </summary>
        /// <param name="movie">The movie to process.</param>
        /// <param name="config">The global plugin configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task ProcessMovieItem(Movie movie, PluginConfiguration config, CancellationToken cancellationToken)
        {
            await ProcessItemInternal(
                movie,
                config.MovieAudioMaxThemes,
                config.MovieAudioVolume,
                config.MovieAudioIgnoreOp,
                config.MovieAudioIgnoreEd,
                config.MovieAudioIgnoreOverlaps,
                config.MovieAudioIgnoreCredits,
                config.MovieVideoMaxThemes,
                config.MovieVideoVolume,
                config.MovieVideoIgnoreOp,
                config.MovieVideoIgnoreEd,
                config.MovieVideoIgnoreOverlaps,
                config.MovieVideoIgnoreCredits,
                config,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Internal method to process a library item to download its themes.
        /// </summary>
        private async Task ProcessItemInternal(
            BaseItem item,
            int audioMaxThemes,
            int audioVolume,
            bool audioIgnoreOp,
            bool audioIgnoreEd,
            bool audioIgnoreOverlaps,
            bool audioIgnoreCredits,
            int videoMaxThemes,
            int videoVolume,
            bool videoIgnoreOp,
            bool videoIgnoreEd,
            bool videoIgnoreOverlaps,
            bool videoIgnoreCredits,
            PluginConfiguration globalConfig,
            CancellationToken cancellationToken)
        {
            if (audioMaxThemes <= 0 && videoMaxThemes <= 0)
            {
                return;
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
                return;
            }

            // Get AnimeThemes Data
            Services.AnimeThemesService.AnimeThemesAnime? anime = null;
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
                return;
            }

            var backdropsPath = Path.Combine(item.Path, "backdrops");
            var themeMusicPath = Path.Combine(item.Path, "theme-music");

            // 1. Determine Desired Files
            // Structure: Key = Destination Path, Value = Download URL
            var desiredFiles = new Dictionary<string, (string Url, bool IsVideo)>();

            // Collect Videos
            if (videoMaxThemes > 0)
            {
                var validThemes = FilterThemes(anime.AnimeThemes, videoIgnoreOp, videoIgnoreEd);
                int count = 0;
                foreach (var theme in validThemes)
                {
                    if (count >= videoMaxThemes)
                    {
                        break;
                    }

                    var bestVideo = SelectBestVideo(theme, videoIgnoreOverlaps, videoIgnoreCredits);
                    if (bestVideo != null && !string.IsNullOrEmpty(bestVideo.Link))
                    {
                        var filename = $"{theme.Slug ?? theme.Type}-video.webm";
                        var path = Path.Combine(backdropsPath, filename);
                        if (!desiredFiles.ContainsKey(path))
                        {
                            desiredFiles.Add(path, (bestVideo.Link!, true));
                            count++;
                        }
                    }
                }
            }

            // Collect Audio
            if (audioMaxThemes > 0)
            {
                var validThemes = FilterThemes(anime.AnimeThemes, audioIgnoreOp, audioIgnoreEd);
                int count = 0;
                foreach (var theme in validThemes)
                {
                    if (count >= audioMaxThemes)
                    {
                        break;
                    }

                    var bestVideo = SelectBestVideo(theme, audioIgnoreOverlaps, audioIgnoreCredits);
                    if (bestVideo != null)
                    {
                        var link = bestVideo.Audio?.Link ?? bestVideo.Link;
                        if (!string.IsNullOrEmpty(link))
                        {
                            var filename = $"{theme.Slug ?? theme.Type}.mp3"; // Prefer simpler name for audio
                            var path = Path.Combine(themeMusicPath, filename);
                            if (!desiredFiles.ContainsKey(path))
                            {
                                desiredFiles.Add(path, (link!, false));
                                count++;
                            }
                        }
                    }
                }
            }

            // 2. Process Files
            // Add (Download)
            foreach (var file in desiredFiles)
            {
                var targetPath = file.Key;
                var url = file.Value.Url;

                if (globalConfig.ForceRedownload || !_fileSystem.FileExists(targetPath))
                {
                    var dir = Path.GetDirectoryName(targetPath);
                    if (dir != null && !_fileSystem.DirectoryExists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var volume = file.Value.IsVideo ? videoVolume : audioVolume;
                    await DownloadFile(url, targetPath, volume, cancellationToken).ConfigureAwait(false);
                }
            }

            // Delete (Cleanup)
            if (globalConfig.AllowDelete)
            {
                if (_fileSystem.DirectoryExists(themeMusicPath))
                {
                    foreach (var file in _fileSystem.GetFilePaths(themeMusicPath))
                    {
                        if (desiredFiles.ContainsKey(file))
                        {
                            continue;
                        }

                        var filename = Path.GetFileName(file);
                        if (IsAnimeThemeFile(filename, anime.AnimeThemes))
                        {
                            _logger.LogInformation("Deleting unwanted theme music: {Path}", file);
                            _fileSystem.DeleteFile(file);
                        }
                    }
                }

                if (_fileSystem.DirectoryExists(backdropsPath))
                {
                    foreach (var file in _fileSystem.GetFilePaths(backdropsPath))
                    {
                        if (desiredFiles.ContainsKey(file))
                        {
                            continue;
                        }

                        var filename = Path.GetFileName(file);
                        if (IsAnimeThemeFile(filename, anime.AnimeThemes))
                        {
                            _logger.LogInformation("Deleting unwanted theme video: {Path}", file);
                            _fileSystem.DeleteFile(file);
                        }
                    }
                }
            }
        }

        private bool IsAnimeThemeFile(string filename, List<Services.AnimeThemesService.AnimeThemesTheme> themes)
        {
            // Simple check: does filename contain any of the theme slugs?
            return themes.Any(t => !string.IsNullOrEmpty(t.Slug) && filename.Contains(t.Slug, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Filters a list of themes based on the provided configuration.
        /// </summary>
        /// <param name="themes">The list of themes to filter.</param>
        /// <param name="ignoreOp">Whether to ignore OP themes.</param>
        /// <param name="ignoreEd">Whether to ignore ED themes.</param>
        /// <returns>A filtered list of themes.</returns>
        private List<Services.AnimeThemesService.AnimeThemesTheme> FilterThemes(List<Services.AnimeThemesService.AnimeThemesTheme> themes, bool ignoreOp, bool ignoreEd)
        {
            var list = new List<Services.AnimeThemesService.AnimeThemesTheme>();
            foreach (var t in themes)
            {
                if (ignoreOp && (t.Type?.Equals("OP", StringComparison.OrdinalIgnoreCase) == true))
                {
                    continue;
                }

                if (ignoreEd && (t.Type?.Equals("ED", StringComparison.OrdinalIgnoreCase) == true))
                {
                    continue;
                }

                list.Add(t);
            }

            return list;
        }

        /// <summary>
        /// Selects the best video for a theme based on the configuration.
        /// </summary>
        /// <param name="theme">The theme to select a video for.</param>
        /// <param name="ignoreOverlaps">Whether to ignore overlapping themes.</param>
        /// <param name="ignoreCredits">Whether to ignore credits.</param>
        /// <returns>The best video for the theme, or null if none found.</returns>
        private Services.AnimeThemesService.AnimeThemesVideo? SelectBestVideo(Services.AnimeThemesService.AnimeThemesTheme theme, bool ignoreOverlaps, bool ignoreCredits)
        {
            if (theme.Entries == null)
            {
                return null;
            }

            var candidates = new List<(Services.AnimeThemesService.AnimeThemesEntry Entry, Services.AnimeThemesService.AnimeThemesVideo Video, double Score)>();

            foreach (var entry in theme.Entries)
            {
                if (entry.Videos != null)
                {
                    foreach (var video in entry.Videos)
                    {
                        candidates.Add((entry, video, Rate(entry, video)));
                    }
                }
            }

            var orderedCandidates = candidates.OrderBy(x => x.Score).ToList();

            foreach (var candidate in orderedCandidates)
            {
                if (ignoreOverlaps && !string.IsNullOrEmpty(candidate.Video.Overlap) && !candidate.Video.Overlap.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool isCreditless = candidate.Video.Tags?.Contains("NC", StringComparison.OrdinalIgnoreCase) == true || candidate.Video.Tags?.Contains("Creditless", StringComparison.OrdinalIgnoreCase) == true;
                if (ignoreCredits && !isCreditless)
                {
                    continue;
                }

                return candidate.Video;
            }

            return null;
        }

        private double Rate(Services.AnimeThemesService.AnimeThemesEntry entry, Services.AnimeThemesService.AnimeThemesVideo video)
        {
            double score = 0;

            // Spoiler: +50
            if (entry.Spoiler == true)
            {
                score += 50;
            }

            // Overlap: +15-20
            if (!string.IsNullOrEmpty(video.Overlap))
            {
                if (video.Overlap.Equals("Over", StringComparison.OrdinalIgnoreCase))
                {
                    score += 20;
                }
                else if (video.Overlap.Equals("Transition", StringComparison.OrdinalIgnoreCase))
                {
                    score += 15;
                }
            }

            // Source quality: +5-10
            if (!string.IsNullOrEmpty(video.Source))
            {
                if (video.Source.Equals("LD", StringComparison.OrdinalIgnoreCase) || video.Source.Equals("VHS", StringComparison.OrdinalIgnoreCase))
                {
                    score += 10;
                }
                else if (video.Source.Equals("WEB", StringComparison.OrdinalIgnoreCase) || video.Source.Equals("RAW", StringComparison.OrdinalIgnoreCase))
                {
                    score += 5;
                }
            }

            // Credits: +10 (if not creditless)
            bool isCreditless = video.Tags?.Contains("NC", StringComparison.OrdinalIgnoreCase) == true || video.Tags?.Contains("Creditless", StringComparison.OrdinalIgnoreCase) == true;
            if (!isCreditless)
            {
                score += 10;
            }

            return score;
        }

        /// <summary>
        /// Downloads a file from a given URL to a specified path.
        /// </summary>
        /// <param name="url">The URL of the file to download.</param>
        /// <param name="path">The local path where the file should be saved.</param>
        /// <param name="volume">The target volume (0-100).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task DownloadFile(string url, string path, int volume, CancellationToken cancellationToken)
        {
            var config = Plugin.Instance?.Configuration;
            var timeoutSeconds = config?.DownloadTimeoutSeconds > 0 ? config.DownloadTimeoutSeconds : 600;

            var client = _httpClientFactory.CreateClient("AnimeThemes");
            // Increase timeout for large file downloads (videos) which might take longer than default 100s
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            var tempPath = path + ".part";

            try
            {
                using var response = await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    // Use FileShare.None to prevent access while writing
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
                // Cleanup partial file on error
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

                throw;
            }

            // Move to final path atomically (or as close as possible)
            // If destination exists (from a previous run or partial download), overwrite it.
            try
            {
                File.Move(tempPath, path, overwrite: true);
                await BakeVolume(path, volume, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to move temp file {TempPath} to {FinalPath}", tempPath, path);
                // Attempt cleanup
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }

                throw;
            }
        }

        private async Task BakeVolume(string path, int volume, CancellationToken cancellationToken)
        {
            _logger.LogInformation("BakeVolume called for {Path} with Volume {Volume}", path, volume);

            if (volume >= 100)
            {
                _logger.LogInformation("Volume is 100 or more. Skipping adjustment.");
                return;
            }

            var encoderPath = _mediaEncoder.EncoderPath;
            if (string.IsNullOrEmpty(encoderPath))
            {
                _logger.LogWarning("FFmpeg encoder path not found. Skipping volume adjustment.");
                return;
            }

            var tempPath = path + ".temp" + Path.GetExtension(path);
            var extension = Path.GetExtension(path).ToLowerInvariant();
            string args;

            if (volume <= 0)
            {
                // Mute / Remove audio
                if (extension == ".webm")
                {
                    // Remove audio track for video
                    args = $"-i \"{path}\" -c:v copy -an \"{tempPath}\"";
                }
                else
                {
                    // For audio files, we can't remove the track (empty file), so we silence it.
                    // Or implies deletion? User said "0でミュート、音声トラック削除".
                    // For theme music (audio file), deleting track means 0 bytes?
                    // Let's assume Mute (volume=0) for audio files.
                    args = $"-i \"{path}\" -filter:a \"volume=0\" \"{tempPath}\"";
                }
            }
            else
            {
                // Scale volume
                var volScale = volume / 100.0;
                // Use generic volume filter.
                // For webm (video), we copy video stream and re-encode audio.
                // We default to libvorbis for webm audio to be safe, or just let ffmpeg decide default for webm.
                if (extension == ".webm")
                {
                    args = $"-i \"{path}\" -c:v copy -filter:a \"volume={volScale.ToString("F2", CultureInfo.InvariantCulture)}\" -c:a libvorbis \"{tempPath}\"";
                }
                else
                {
                    // Audio file (mp3)
                    args = $"-i \"{path}\" -filter:a \"volume={volScale.ToString("F2", CultureInfo.InvariantCulture)}\" \"{tempPath}\"";
                }
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = encoderPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // FFmpeg output is on stderr
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = new Process { StartInfo = processStartInfo };
                _logger.LogInformation("Running ffmpeg: {FileName} {Arguments}", processStartInfo.FileName, processStartInfo.Arguments);

                process.Start();

                // Read stderr to log if needed (FFmpeg writes to stderr)
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                if (process.ExitCode == 0 && File.Exists(tempPath))
                {
                    try
                    {
                        File.Move(tempPath, path, true);
                        _logger.LogInformation("Volume adjusted for {Path}. FFmpeg Output: {Stderr}", path, stderr); // Log success detail
                    }
                    catch (IOException ex)
                    {
                        _logger.LogError(ex, "Failed to replace file with volume adjusted version.");
                    }
                }
                else
                {
                    _logger.LogError("FFmpeg failed with exit code {ExitCode}. Error: {Stderr}", process.ExitCode, stderr);
                    // Cleanup temp
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running volume adjustment.");
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }
}
