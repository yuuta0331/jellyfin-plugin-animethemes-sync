using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AnimeThemesSync.Shared;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AnimeThemesSync.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
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
    /// <param name="animeThemesService">The AnimeThemes service.</param>
    public ThemeDownloader(
        ILibraryManager libraryManager,
        IFileSystem fileSystem,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IMediaEncoder mediaEncoder,
        AnimeThemesService animeThemesService)
    {
        _libraryManager = libraryManager;
        _fileSystem = fileSystem;
        _logger = loggerFactory.CreateLogger<ThemeDownloader>();
        _httpClientFactory = httpClientFactory;
        _mediaEncoder = mediaEncoder;
        _animeThemesService = animeThemesService;
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

        var result = await ProcessItems(items, config, config.ForceRedownload, progress, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Anime Themes Download Task Completed. Downloaded {Count} files.", result.DownloadsCompleted);
    }

    /// <summary>
    /// Downloads themes for a single library item.
    /// </summary>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    /// <param name="forceRedownload">Whether existing files should be replaced.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The execution result.</returns>
    public async Task<ThemeDownloadExecutionResult> DownloadItemByIdAsync(Guid itemId, bool forceRedownload, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
        if (!config.ThemeDownloadingEnabled)
        {
            throw new InvalidOperationException("Theme downloading is disabled in plugin configuration.");
        }

        var item = _libraryManager.GetItemById(itemId) ?? throw new KeyNotFoundException("The requested item was not found.");
        if (item is not Series && item is not Movie)
        {
            throw new InvalidOperationException("Only Series and Movie items are supported.");
        }

        _logger.LogInformation("Starting Anime Themes on-demand download for {ItemName} ({ItemId})...", item.Name, itemId);
        return await ProcessItems(new[] { item }, config, forceRedownload || config.ForceRedownload, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets browser candidates from AnimeThemes-enabled libraries.
    /// </summary>
    /// <returns>The candidate items.</returns>
    public IReadOnlyList<ThemeBrowserLibraryItem> GetBrowserItems()
    {
        return GetEnabledLibraryItems()
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i => new ThemeBrowserLibraryItem(
                i.Id,
                i.Name ?? "Unknown",
                i is Series ? "Series" : "Movie",
                i.ProviderIds.TryGetValue(Constants.AnimeThemesProviderId, out var slug) ? slug : null,
                i.ProviderIds.TryGetValue(Constants.AniListProviderId, out var aniListId) ? aniListId : null,
                i.ProviderIds.TryGetValue(Constants.MyAnimeListProviderId, out var malId) ? malId : null))
            .ToList();
    }

    /// <summary>
    /// Gets AnimeThemes Browser rows for one library item.
    /// </summary>
    /// <param name="itemId">The item identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The browser item result.</returns>
    public async Task<ThemeBrowserItemResult> GetThemeBrowserItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
        var item = _libraryManager.GetItemById(itemId) ?? throw new KeyNotFoundException("The requested item was not found.");
        if (item is not Series && item is not Movie)
        {
            throw new InvalidOperationException("Only Series and Movie items are supported.");
        }

        var anime = await ResolveAnime(item, cancellationToken).ConfigureAwait(false);
        var animeThemesUrl = !string.IsNullOrWhiteSpace(anime?.Slug)
            ? Constants.AnimeThemesWebUrl + "/anime/" + anime.Slug
            : null;

        var rows = anime?.AnimeThemes == null
            ? new List<ThemeBrowserThemeRow>()
            : BuildBrowserRows(item, anime, config);

        return new ThemeBrowserItemResult(
            item.Id,
            item.Name ?? "Unknown",
            item is Series ? "Series" : "Movie",
            anime?.Slug,
            animeThemesUrl,
            rows);
    }

    private async Task<ThemeDownloadExecutionResult> ProcessItems(
        IReadOnlyList<BaseItem> items,
        PluginConfiguration config,
        bool forceRedownload,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        // ── Phase 1: Resolve all items sequentially (API calls are rate-limited) ──
        _logger.LogInformation("=== Phase 1: Resolving themes for {Count} items ===", items.Count);

        var allDownloads = new List<(ThemeFilePlan File, int Volume, string ItemName)>();
        var allExtras = new List<(ThemeExtraPlan Extra, string ItemName)>();
        var cleanupTasks = new List<(string Directory, HashSet<string> DesiredFiles, List<AnimeThemesTheme> Themes)>();

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
                if (config.AllowAdd)
                {
                    foreach (var file in result.MediaFiles)
                    {
                        var volume = file.IsVideo ? videoConfig.Volume : audioConfig.Volume;
                        if (forceRedownload || !_fileSystem.FileExists(file.Path))
                        {
                            allDownloads.Add((file, volume, itemName));
                        }
                    }

                    foreach (var extra in result.ExtraFiles)
                    {
                        if (forceRedownload || !_fileSystem.FileExists(extra.TargetPath))
                        {
                            allExtras.Add((extra, itemName));
                        }
                    }
                }

                // Queue cleanup tasks
                if (config.AllowDelete && result.Themes != null)
                {
                    var themeMusicPath = Path.Combine(item.Path, "theme-music");
                    var backdropsPath = Path.Combine(item.Path, "backdrops");
                    var extrasPath = Path.Combine(item.Path, "extras");
                    cleanupTasks.Add((themeMusicPath, result.MediaFiles.Where(f => !f.IsVideo).Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase), result.Themes));
                    cleanupTasks.Add((backdropsPath, result.MediaFiles.Where(f => f.IsVideo).Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase), result.Themes));
                    cleanupTasks.Add((extrasPath, result.ExtraFiles.Select(f => f.TargetPath).ToHashSet(StringComparer.OrdinalIgnoreCase), result.Themes));
                }
            }

            progress?.Report((double)(i + 1) / items.Count * 50); // Phase 1 = 0-50%
        }

        _logger.LogInformation(
            "Extras configuration: Enabled={ExtrasEnabled}, LinkMode={ExtrasLinkMode}, Planned={PlannedExtras}",
            config.ExtrasEnabled,
            config.ExtrasLinkMode,
            allExtras.Count);

        if (!config.ExtrasEnabled)
        {
            _logger.LogInformation("Browseable OP/ED extras are disabled. Enable \"Create Browseable OP/ED Extras\" to create the extras folder.");
        }

        // ── Phase 2: Download all files in parallel ──
        _logger.LogInformation("=== Phase 2: Downloading {Count} files (MaxConcurrent={Max}) ===", allDownloads.Count, config.MaxConcurrentDownloads);

        var completedDownloads = 0;
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
                            var dir = Path.GetDirectoryName(dl.File.Path);
                            if (dir != null && !_fileSystem.DirectoryExists(dir))
                            {
                                _ = Directory.CreateDirectory(dir);
                            }

                            const int MaxRetries = 3;
                            for (var attempt = 1; attempt <= MaxRetries; attempt++)
                            {
                                try
                                {
                                    _logger.LogDebug("Downloading [{ItemName}] {Filename}...", dl.ItemName, Path.GetFileName(dl.File.Path));
                                    await DownloadFile(dl.File.Url, dl.File.Path, dl.Volume, cancellationToken).ConfigureAwait(false);
                                    _ = Interlocked.Increment(ref completedDownloads);
                                    _logger.LogInformation("Downloaded [{ItemName}] {Filename}", dl.ItemName, Path.GetFileName(dl.File.Path));
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
                                        _logger.LogWarning(ex, "Download attempt {Attempt}/{MaxRetries} failed for {Url}. Retrying...", attempt, MaxRetries, dl.File.Url);
                                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        _logger.LogError(ex, "Download failed after {MaxRetries} attempts for {Url}. Skipping file.", MaxRetries, dl.File.Url);
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

        // ── Extras ──
        var completedExtras = 0;
        var failedExtras = 0;
        foreach (var extra in allExtras)
        {
            try
            {
                var result = ThemeExtrasFileService.EnsureExtraFileDetailed(
                    extra.Extra.SourcePath,
                    extra.Extra.TargetPath,
                    config.ExtrasLinkMode,
                    config.ForceRedownload);

                _logger.LogInformation(
                    "Extras {Action} [{ItemName}] {Filename} (HardLinkVerified={HardLinkVerified}, LinkCount={LinkCount}, FallbackReason={FallbackReason})",
                    result.Action,
                    extra.ItemName,
                    Path.GetFileName(extra.Extra.TargetPath),
                    result.HardLinkVerified,
                    result.LinkCount,
                    result.FallbackReason);
                completedExtras++;
            }
            catch (Exception ex)
            {
                failedExtras++;
                _logger.LogWarning(ex, "Failed to create extras file: {Path}", extra.Extra.TargetPath);
            }
        }

        // ── Cleanup ──
        foreach (var cleanup in cleanupTasks)
        {
            CleanupDirectory(cleanup.Directory, cleanup.DesiredFiles, cleanup.Themes);
        }

        return new ThemeDownloadExecutionResult(items.Count, allDownloads.Count, completedDownloads, allExtras.Count, completedExtras, failedExtras);
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
    private async Task<ThemeOutputPlan?> ResolveItem(
        BaseItem item,
        ThemeConfig audioConfig,
        ThemeConfig videoConfig,
        CancellationToken cancellationToken)
    {
        if (audioConfig.MaxThemes <= 0 && videoConfig.MaxThemes <= 0)
        {
            return null;
        }

        var anime = await ResolveAnime(item, cancellationToken).ConfigureAwait(false);
        if (anime?.AnimeThemes == null)
        {
            _logger.LogWarning("  No themes found for {ItemName}.", item.Name);
            return null;
        }

        var config = Plugin.Instance?.Configuration;
        return ThemeFilePlanner.BuildPlan(anime, item.Path, audioConfig, videoConfig, config?.ExtrasEnabled ?? false);
    }

    private async Task<AnimeThemesAnime?> ResolveAnime(BaseItem item, CancellationToken cancellationToken)
    {
        int? aniListId = null;
        int? malId = null;

        if (item.ProviderIds.TryGetValue(Constants.AniListProviderId, out var aniListIdStr) && int.TryParse(aniListIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aid))
        {
            aniListId = aid;
        }

        if (item.ProviderIds.TryGetValue(Constants.MyAnimeListProviderId, out var malIdStr) && int.TryParse(malIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mid))
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
        if (item.ProviderIds.TryGetValue(Constants.AnimeThemesProviderId, out var animeThemesSlug) && !string.IsNullOrEmpty(animeThemesSlug))
        {
            anime = await _animeThemesService.GetAnimeBySlug(animeThemesSlug, cancellationToken).ConfigureAwait(false);
        }

        if (anime == null)
        {
            if (aniListId.HasValue)
            {
                anime = await _animeThemesService.GetAnimeByExternalId(Constants.AniListSiteKey, aniListId.Value, cancellationToken).ConfigureAwait(false);
            }

            if (anime == null && malId.HasValue)
            {
                anime = await _animeThemesService.GetAnimeByExternalId(Constants.MyAnimeListSiteKey, malId.Value, cancellationToken).ConfigureAwait(false);
            }
        }

        return anime;
    }

    private List<ThemeBrowserThemeRow> BuildBrowserRows(BaseItem item, AnimeThemesAnime anime, PluginConfiguration config)
    {
        var audioConfig = CreateThemeConfig(item, config, isVideo: false);
        var videoConfig = CreateThemeConfig(item, config, isVideo: true);
        var plan = ThemeFilePlanner.BuildPlan(anime, item.Path, audioConfig, videoConfig, config.ExtrasEnabled);

        var videoPlans = plan.MediaFiles
            .Where(f => f.IsVideo)
            .GroupBy(f => f.Url, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var audioPlans = plan.MediaFiles
            .Where(f => !f.IsVideo)
            .GroupBy(f => f.Url, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var extraPlans = plan.ExtraFiles
            .GroupBy(f => f.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return ThemeScoringService.GetScoredCandidates(anime.AnimeThemes!, ignoreOp: false, ignoreEd: false, ignoreOverlaps: false, ignoreCredits: false)
            .OrderBy(GetThemeTypeOrder)
            .ThenBy(c => c.Theme.Sequence ?? int.MaxValue)
            .ThenBy(c => c.Entry.Version ?? 1)
            .ThenBy(c => c.Theme.Slug ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Score)
            .Select(c => BuildBrowserRow(c, anime, videoPlans, audioPlans, extraPlans))
            .ToList();
    }

    private ThemeBrowserThemeRow BuildBrowserRow(
        ScoredCandidate candidate,
        AnimeThemesAnime anime,
        Dictionary<string, ThemeFilePlan> videoPlans,
        Dictionary<string, ThemeFilePlan> audioPlans,
        Dictionary<string, ThemeExtraPlan> extraPlans)
    {
        var videoPlan = !string.IsNullOrWhiteSpace(candidate.Video.Link) &&
                        videoPlans.TryGetValue(candidate.Video.Link, out var vp)
            ? vp
            : null;
        var audioUrl = candidate.Video.Audio?.Link ?? candidate.Video.Link;
        var audioPlan = !string.IsNullOrWhiteSpace(audioUrl) &&
                        audioPlans.TryGetValue(audioUrl, out var ap)
            ? ap
            : null;
        var extraPlan = videoPlan != null && extraPlans.TryGetValue(videoPlan.Path, out var ep) ? ep : null;
        var labels = string.Join(", ", ThemeFilePlanner.BuildLabels(candidate));
        var animeThemesUrl = !string.IsNullOrWhiteSpace(anime.Slug)
            ? Constants.AnimeThemesWebUrl + "/anime/" + anime.Slug
            : null;

        return new ThemeBrowserThemeRow(
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
            videoPlan != null && _fileSystem.FileExists(videoPlan.Path),
            audioPlan?.Path,
            audioPlan != null && _fileSystem.FileExists(audioPlan.Path),
            extraPlan?.TargetPath,
            extraPlan != null && _fileSystem.FileExists(extraPlan.TargetPath),
            animeThemesUrl);
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

    /// <summary>
    /// Cleans up files from a directory that are no longer desired.
    /// </summary>
    private void CleanupDirectory(
        string directory,
        HashSet<string> desiredFiles,
        List<AnimeThemesTheme> themes)
    {
        if (!_fileSystem.DirectoryExists(directory))
        {
            return;
        }

        foreach (var file in _fileSystem.GetFilePaths(directory))
        {
            if (desiredFiles.Contains(file))
            {
                continue;
            }

            if (ThemeFilePlanner.IsPluginOwnedFile(file, themes))
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

        var client = _httpClientFactory.CreateClient(Constants.AnimeThemesHttpClientName);
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
