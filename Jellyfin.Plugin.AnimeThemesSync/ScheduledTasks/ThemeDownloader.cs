using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AnimeThemesSync.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
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
        private readonly AnimeThemesService _animeThemesService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeDownloader"/> class.
        /// </summary>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public ThemeDownloader(
            ILibraryManager libraryManager,
            IFileSystem fileSystem,
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory)
        {
            _libraryManager = libraryManager;
            _fileSystem = fileSystem;
            _logger = loggerFactory.CreateLogger<ThemeDownloader>();
            _httpClientFactory = httpClientFactory;
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

            if (Plugin.Instance?.Configuration.ThemeDownloadingEnabled != true)
            {
                _logger.LogInformation("Theme downloading is disabled in plugin configuration.");
                return;
            }

            // Query specifically for Series
            var seriesList = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Series },
                Recursive = true
            }).Cast<Series>().ToList();

            var total = seriesList.Count;
            var current = 0;

            foreach (var series in seriesList)
            {
                current++;
                progress?.Report((double)current / total * 100);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await ProcessSeries(series, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Anime Themes Download Task Completed.");
        }

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // TODO: Update for Jellyfin 10.11 TaskTriggerInfoType changes
            // new TaskTriggerInfo
            // {
            //     Type = "Weekly",
            //     DayOfWeek = DayOfWeek.Monday,
            //     TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            // }

            return new[]
            {
                // Run once a week
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.IntervalTrigger,
                    IntervalTicks = TimeSpan.FromDays(7).Ticks
                }
            };

            // return Array.Empty<TaskTriggerInfo>();
        }

        private async Task ProcessSeries(Series series, CancellationToken cancellationToken)
        {
            var seriesName = series.Name;

            // Resolve IDs
            int? aniListId = null;
            int? malId = null;

            if (series.ProviderIds.TryGetValue("AniList", out var aniListIdStr) && int.TryParse(aniListIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aid))
            {
                aniListId = aid;
            }

            if (series.ProviderIds.TryGetValue("MyAnimeList", out var malIdStr) && int.TryParse(malIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mid))
            {
                malId = mid;
            }

            if (aniListId == null && malId == null)
            {
                _logger.LogDebug("Skipping '{SeriesName}': No AniList or MAL ID found.", seriesName);
                return;
            }

            _logger.LogInformation("Processing '{SeriesName}' (AniList: {AniListId}, MAL: {MalId})", seriesName, aniListId, malId);

            Services.AnimeThemesService.AnimeThemesAnime? anime = null;

            if (series.ProviderIds.TryGetValue("AnimeThemes", out var animeThemesSlug) && !string.IsNullOrEmpty(animeThemesSlug))
            {
                _logger.LogInformation("Processing '{SeriesName}' using existing AnimeThemes Slug: {Slug}", seriesName, animeThemesSlug);
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

            if (anime == null)
            {
                _logger.LogWarning("AnimeThemes has no record for '{SeriesName}'", seriesName);
                return;
            }

            if (anime.AnimeThemes == null)
            {
                _logger.LogInformation("No themes found for '{SeriesName}'", seriesName);
                return;
            }

            // Prepare Themes Directory
            var themesPath = Path.Combine(series.Path, "themes");
            if (!_fileSystem.DirectoryExists(themesPath))
            {
                Directory.CreateDirectory(themesPath);
            }

            foreach (var theme in anime.AnimeThemes)
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

                    // Prefer higher resolution
                    var bestVideo = entry.Videos.OrderByDescending(v => v.Resolution).FirstOrDefault();

                    if (bestVideo == null || string.IsNullOrEmpty(bestVideo.Link))
                    {
                        continue;
                    }

                    var filename = bestVideo.Basename;
                    if (string.IsNullOrEmpty(filename))
                    {
                        filename = $"{theme.Slug ?? theme.Type}.webm";
                    }

                    var filePath = Path.Combine(themesPath, filename);

                    if (_fileSystem.FileExists(filePath))
                    {
                        _logger.LogDebug("Theme already exists: {FilePath}", filePath);
                        continue;
                    }

                    // Download
                    _logger.LogInformation("Downloading theme: {Filename} for {SeriesName}", filename, seriesName);
                    try
                    {
                        var client = _httpClientFactory.CreateClient("AnimeThemes");
                        var videoUrl = bestVideo.Link;

                        using var response = await client.GetAsync(new Uri(videoUrl), cancellationToken).ConfigureAwait(false);
                        if (response.IsSuccessStatusCode)
                        {
                            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                            // Using standard IO for file writing to avoid IFileSystem ambiguity if any
                            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                            await stream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            _logger.LogError("Failed to download theme from {Url}. Status: {Status}", videoUrl, response.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error downloading theme for {SeriesName}", seriesName);
                    }
                }
            }
        }
    }
}
