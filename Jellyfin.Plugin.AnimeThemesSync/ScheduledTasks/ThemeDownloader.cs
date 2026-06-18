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
using MediaBrowser.Model.Entities;
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
    private readonly AniListService _aniListService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeDownloader"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="mediaEncoder">The media encoder.</param>
    /// <param name="animeThemesService">The AnimeThemes service.</param>
    /// <param name="aniListService">The AniList service.</param>
    public ThemeDownloader(
        ILibraryManager libraryManager,
        IFileSystem fileSystem,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IMediaEncoder mediaEncoder,
        AnimeThemesService animeThemesService,
        AniListService aniListService)
    {
        _libraryManager = libraryManager;
        _fileSystem = fileSystem;
        _logger = loggerFactory.CreateLogger<ThemeDownloader>();
        _httpClientFactory = httpClientFactory;
        _mediaEncoder = mediaEncoder;
        _animeThemesService = animeThemesService;
        _aniListService = aniListService;
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
        return await DownloadItemByIdAsync(itemId, forceRedownload, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ThemeDownloadExecutionResult> DownloadItemByIdAsync(
        Guid itemId,
        bool forceRedownload,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
        if (!config.ThemeDownloadingEnabled)
        {
            throw new InvalidOperationException("Theme downloading is disabled in plugin configuration.");
        }

        var item = _libraryManager.GetItemById(itemId) ?? throw new KeyNotFoundException("The requested item was not found.");
        if (item is not Series && item is not Movie && item is not Season)
        {
            throw new InvalidOperationException("Only Series, Season, and Movie items are supported.");
        }

        _logger.LogInformation("Starting Anime Themes on-demand download for {ItemName} ({ItemId})...", item.Name, itemId);
        return await ProcessItems(new[] { item }, config, forceRedownload || config.ForceRedownload, progress, cancellationToken).ConfigureAwait(false);
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
                i.ProviderIds.TryGetValue(Constants.MyAnimeListProviderId, out var malId) ? malId : null,
                BuildImageUrl(i, ImageType.Primary, "Primary"),
                BuildImageUrl(i, ImageType.Logo, "Logo"),
                BuildImageUrl(i, ImageType.Backdrop, "Backdrop/0"),
                BuildImageUrl(i, ImageType.Thumb, "Thumb")))
            .ToList();
    }

    public ThemeBrowserSummary GetBrowserSummary()
    {
        var items = GetEnabledLibraryItems();
        var videos = 0;
        var songs = 0;
        var extras = 0;
        long bytes = 0;

        foreach (var item in items)
        {
            AccumulateLocalThemeDirectories(item.Path, ref videos, ref songs, ref extras, ref bytes);
            if (item is Series series)
            {
                foreach (var season in GetSeasonItems(series))
                {
                    if (!string.IsNullOrWhiteSpace(season.Path))
                    {
                        AccumulateLocalThemeDirectories(season.Path, ref videos, ref songs, ref extras, ref bytes);
                    }
                }
            }
        }

        return new ThemeBrowserSummary(items.Count, videos, songs, extras, bytes);
    }

    public Task<IReadOnlyList<SeasonThemeMappingRow>> GetSeasonThemeMappingsAsync(CancellationToken cancellationToken)
    {
        var rows = new List<SeasonThemeMappingRow>();
        foreach (var series in GetEnabledLibraryItems().OfType<Series>().OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            var seasons = GetSeasonItems(series);
            foreach (var season in seasons)
            {
                if (string.IsNullOrWhiteSpace(season.Path))
                {
                    continue;
                }

                rows.Add(BuildSeasonMappingRow(series, season));
            }
        }

        return Task.FromResult<IReadOnlyList<SeasonThemeMappingRow>>(rows);
    }

    public async Task<IReadOnlyList<ThemeFinderSearchResult>> SearchThemeFinderAnimeAsync(
        string query,
        int? year,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var candidates = await _animeThemesService.SearchAnimeByTitle(query, year, cancellationToken).ConfigureAwait(false);
        return candidates
            .Where(a => !string.IsNullOrWhiteSpace(a.Slug))
            .GroupBy(a => !string.IsNullOrWhiteSpace(a.Slug) ? a.Slug! : a.Id.ToString(CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(15)
            .Select(a => ToThemeFinderSearchResult(a, ScoreSearchCandidate(a, query, year), GetAnimePrimaryImageUrl(a), query))
            .ToList();
    }

    public async Task<ThemeBrowserItemResult> GetAnimeThemePreviewAsync(string slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new InvalidOperationException("AnimeThemes slug is required.");
        }

        var config = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
        var anime = await _animeThemesService.GetAnimeBySlug(slug.Trim(), cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("The requested AnimeThemes anime was not found.");
        var rows = anime.AnimeThemes == null
            ? new List<ThemeBrowserThemeRow>()
            : BuildBrowserRowsForPath(Path.GetTempPath(), anime, config);
        var animeThemesUrl = !string.IsNullOrWhiteSpace(anime.Slug)
            ? Constants.AnimeThemesWebUrl + "/anime/" + anime.Slug
            : null;

        return new ThemeBrowserItemResult(
            Guid.Empty,
            anime.Name ?? anime.Slug ?? "AnimeThemes",
            "AnimeThemes",
            anime.Slug,
            animeThemesUrl,
            rows);
    }

    public async Task<SeasonThemeMappingRow> SaveSeasonThemeMappingAsync(
        SaveSeasonThemeMappingRequest request,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
        if (request.SeasonItemId == Guid.Empty)
        {
            throw new InvalidOperationException("Season item id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AnimeThemesSlug) && !request.AniListId.HasValue && !request.MyAnimeListId.HasValue)
        {
            throw new InvalidOperationException("At least one AnimeThemes, AniList, or MAL identifier is required.");
        }

        var season = _libraryManager.GetItemById(request.SeasonItemId) as Season
            ?? throw new KeyNotFoundException("The requested season was not found.");
        var series = FindSeriesForSeason(season)
            ?? throw new InvalidOperationException("The parent series for the requested season was not found.");

        config.SeasonThemeMappings ??= [];
        RemoveSeasonMappings(config.SeasonThemeMappings, season);
        config.SeasonThemeMappings.Add(new SeasonThemeMapping
        {
            Enabled = true,
            SeriesItemId = series.Id.ToString("D"),
            SeriesPath = series.Path,
            SeasonItemId = season.Id.ToString("D"),
            SeasonPath = season.Path,
            SeasonNumber = season.IndexNumber,
            AnimeThemesSlug = string.IsNullOrWhiteSpace(request.AnimeThemesSlug) ? null : request.AnimeThemesSlug.Trim(),
            AniListId = request.AniListId,
            MyAnimeListId = request.MyAnimeListId,
            Locked = request.Locked,
        });
        Plugin.Instance.UpdateConfiguration(config);

        return await Task.FromResult(BuildSeasonMappingRow(series, season)).ConfigureAwait(false);
    }

    public async Task<SeasonThemeMappingRow> DeleteSeasonThemeMappingAsync(Guid seasonItemId, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
        var season = _libraryManager.GetItemById(seasonItemId) as Season
            ?? throw new KeyNotFoundException("The requested season was not found.");
        var series = FindSeriesForSeason(season)
            ?? throw new InvalidOperationException("The parent series for the requested season was not found.");

        config.SeasonThemeMappings ??= [];
        RemoveSeasonMappings(config.SeasonThemeMappings, season);
        Plugin.Instance.UpdateConfiguration(config);

        return await Task.FromResult(BuildSeasonMappingRow(series, season)).ConfigureAwait(false);
    }

    private void AccumulateLocalThemeDirectories(string itemPath, ref int videos, ref int songs, ref int extras, ref long bytes)
    {
        AccumulateLocalThemeDirectory(Path.Combine(itemPath, "backdrops"), ref videos, ref bytes);
        AccumulateLocalThemeDirectory(Path.Combine(itemPath, "theme-music"), ref songs, ref bytes);
        AccumulateLocalThemeDirectory(Path.Combine(itemPath, "extras"), ref extras, ref bytes);
    }

    public ThemeDeleteResult DeleteThemeFiles(string scope)
    {
        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "all" : scope.Trim().ToLowerInvariant();
        if (normalizedScope is not "all" and not "audio" and not "video" and not "extras")
        {
            throw new InvalidOperationException("Unsupported delete scope.");
        }

        var filesDeleted = 0;
        long bytesDeleted = 0;
        foreach (var item in GetEnabledLibraryItems())
        {
            var anime = ResolveAnime(item, CancellationToken.None).GetAwaiter().GetResult();
            var themes = anime?.AnimeThemes ?? new List<AnimeThemesTheme>();
            DeleteThemeFilesForPath(item.Path, themes, normalizedScope, ref filesDeleted, ref bytesDeleted);

            if (item is Series series)
            {
                foreach (var season in GetSeasonItems(series))
                {
                    if (string.IsNullOrWhiteSpace(season.Path))
                    {
                        continue;
                    }

                    var seasonAnime = ResolveAnime(season, CancellationToken.None, logMissingIds: false).GetAwaiter().GetResult();
                    DeleteThemeFilesForPath(
                        season.Path,
                        seasonAnime?.AnimeThemes ?? new List<AnimeThemesTheme>(),
                        normalizedScope,
                        ref filesDeleted,
                        ref bytesDeleted);
                }
            }
        }

        _logger.LogInformation("Deleted AnimeThemes local files. Scope={Scope}, Files={Files}, Bytes={Bytes}", normalizedScope, filesDeleted, bytesDeleted);
        return new ThemeDeleteResult(filesDeleted, bytesDeleted);
    }

    private void DeleteThemeFilesForPath(
        string itemPath,
        List<AnimeThemesTheme> themes,
        string normalizedScope,
        ref int filesDeleted,
        ref long bytesDeleted)
    {
        if (normalizedScope is "all" or "audio")
        {
            DeleteLocalThemeDirectory(Path.Combine(itemPath, "theme-music"), themes, ref filesDeleted, ref bytesDeleted);
        }

        if (normalizedScope is "all" or "video")
        {
            DeleteLocalThemeDirectory(Path.Combine(itemPath, "backdrops"), themes, ref filesDeleted, ref bytesDeleted);
            DeleteLocalThemeDirectory(Path.Combine(itemPath, "extras"), themes, ref filesDeleted, ref bytesDeleted);
        }

        if (normalizedScope == "extras")
        {
            DeleteLocalThemeDirectory(Path.Combine(itemPath, "extras"), themes, ref filesDeleted, ref bytesDeleted);
        }
    }

    public async Task<ThemeDownloadExecutionResult> DownloadThemeByRowIdAsync(
        Guid itemId,
        string rowId,
        bool forceRedownload,
        CancellationToken cancellationToken)
    {
        return await DownloadThemeByRowIdAsync(itemId, rowId, forceRedownload, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ThemeDownloadExecutionResult> DownloadThemeByRowIdAsync(
        Guid itemId,
        string rowId,
        bool forceRedownload,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
        if (!config.ThemeDownloadingEnabled)
        {
            throw new InvalidOperationException("Theme downloading is disabled in plugin configuration.");
        }

        if (!config.AllowAdd)
        {
            throw new InvalidOperationException("Adding theme files is disabled in plugin configuration.");
        }

        var item = GetSupportedItem(itemId);
        _logger.LogInformation("Starting Anime Themes theme-row download for {ItemName} ({ItemId}, RowId={RowId})...", item.Name, itemId, rowId);
        progress?.Report(5);
        var selection = await BuildSingleThemeSelectionAsync(item, rowId, cancellationToken).ConfigureAwait(false);
        progress?.Report(20);
        var audioConfig = CreateThemeConfig(item, config, isVideo: false);
        var videoConfig = CreateThemeConfig(item, config, isVideo: true);
        var plan = ThemeFilePlanner.BuildSingleCandidatePlan(
            selection.Anime,
            selection.Candidate,
            selection.Order,
            item.Path,
            includeAudio: true,
            includeVideo: true,
            includeExtras: config.ExtrasEnabled);

        var pendingMedia = plan.MediaFiles
            .Where(file => forceRedownload || config.ForceRedownload || !_fileSystem.FileExists(file.Path))
            .ToList();
        var pendingExtras = plan.ExtraFiles
            .Where(extra => forceRedownload || config.ForceRedownload || !_fileSystem.FileExists(extra.TargetPath))
            .ToList();
        var totalSteps = Math.Max(1, pendingMedia.Count + pendingExtras.Count);
        var finishedSteps = 0;
        var downloadsPlanned = 0;
        var downloadsCompleted = 0;
        foreach (var file in pendingMedia)
        {
            downloadsPlanned++;
            var dir = Path.GetDirectoryName(file.Path);
            if (dir != null && !_fileSystem.DirectoryExists(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            _logger.LogInformation("Downloading AnimeThemes row media [{ItemName}] {Filename}", item.Name, Path.GetFileName(file.Path));
            await DownloadFile(file.Url, file.Path, file.IsVideo ? videoConfig.Volume : audioConfig.Volume, cancellationToken).ConfigureAwait(false);
            downloadsCompleted++;
            finishedSteps++;
            progress?.Report(20 + ((double)finishedSteps / totalSteps * 75));
        }

        var extrasPlanned = 0;
        var extrasCompleted = 0;
        var extraFailures = 0;
        foreach (var extra in pendingExtras)
        {
            extrasPlanned++;
            try
            {
                var result = ThemeExtrasFileService.EnsureExtraFileDetailed(
                    extra.SourcePath,
                    extra.TargetPath,
                    config.ExtrasLinkMode,
                    forceRedownload || config.ForceRedownload);
                if (string.Equals(result.Action, "skipped", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                extrasCompleted++;
                _logger.LogInformation(
                    "Extras {Action} [{ItemName}] {Filename} (HardLinkVerified={HardLinkVerified}, LinkCount={LinkCount}, FallbackReason={FallbackReason})",
                    result.Action,
                    item.Name,
                    Path.GetFileName(extra.TargetPath),
                    result.HardLinkVerified,
                    result.LinkCount,
                    result.FallbackReason);
            }
            catch (Exception ex)
            {
                extraFailures++;
                _logger.LogError(ex, "Failed to create extra for {ItemName}: {Path}", item.Name, extra.TargetPath);
            }

            finishedSteps++;
            progress?.Report(20 + ((double)finishedSteps / totalSteps * 75));
        }

        progress?.Report(100);
        return new ThemeDownloadExecutionResult(1, downloadsPlanned, downloadsCompleted, extrasPlanned, extrasCompleted, extraFailures);
    }

    public async Task<ThemeLocalMediaResult> GetLocalThemeMediaAsync(
        Guid itemId,
        string rowId,
        string target,
        CancellationToken cancellationToken)
    {
        var item = GetSupportedItem(itemId);
        var result = await GetThemeBrowserItemAsync(itemId, cancellationToken).ConfigureAwait(false);
        var row = result.Themes.FirstOrDefault(r => string.Equals(r.RowId, rowId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("The requested theme row was not found.");

        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("Local media target is required.");
        }

        var path = target.ToLowerInvariant() switch
        {
            "video" => row.BackdropExists ? row.BackdropPath : null,
            "audio" => row.ThemeMusicExists ? row.ThemeMusicPath : null,
            "extra" => row.ExtraExists ? row.ExtraPath : null,
            _ => throw new InvalidOperationException("Unsupported local media target.")
        };

        if (string.IsNullOrWhiteSpace(path) || !_fileSystem.FileExists(path))
        {
            throw new FileNotFoundException("The requested local theme media was not found.");
        }

        ValidateLocalMediaPath(item.Path, path);
        var extension = Path.GetExtension(path);
        var contentType = extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            ? "audio/mpeg"
            : "video/webm";
        return new ThemeLocalMediaResult(path, contentType, Path.GetFileName(path));
    }

    private static string? BuildImageUrl(BaseItem item, ImageType imageType, string imagePath)
    {
        return item.HasImage(imageType, 0)
            ? string.Format(CultureInfo.InvariantCulture, "Items/{0}/Images/{1}", item.Id, imagePath)
            : null;
    }

    private void AccumulateLocalThemeDirectory(string directory, ref int count, ref long bytes)
    {
        if (!_fileSystem.DirectoryExists(directory))
        {
            return;
        }

        foreach (var file in _fileSystem.GetFilePaths(directory))
        {
            if (!IsSupportedThemeFile(file))
            {
                continue;
            }

            count++;
            bytes += new FileInfo(file).Length;
        }
    }

    private void DeleteLocalThemeDirectory(
        string directory,
        List<AnimeThemesTheme> themes,
        ref int filesDeleted,
        ref long bytesDeleted)
    {
        if (!_fileSystem.DirectoryExists(directory))
        {
            return;
        }

        foreach (var file in _fileSystem.GetFilePaths(directory))
        {
            if (!IsSupportedThemeFile(file) || !ThemeFilePlanner.IsPluginOwnedFile(file, themes))
            {
                continue;
            }

            var length = new FileInfo(file).Length;
            _fileSystem.DeleteFile(file);
            filesDeleted++;
            bytesDeleted += length;
        }
    }

    private static bool IsSupportedThemeFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase);
    }

    private BaseItem GetSupportedItem(Guid itemId)
    {
        var item = _libraryManager.GetItemById(itemId) ?? throw new KeyNotFoundException("The requested item was not found.");
        if (item is not Series && item is not Movie && item is not Season)
        {
            throw new InvalidOperationException("Only Series, Season, and Movie items are supported.");
        }

        return item;
    }

    private async Task<(AnimeThemesAnime Anime, ScoredCandidate Candidate, int Order)> BuildSingleThemeSelectionAsync(
        BaseItem item,
        string rowId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rowId))
        {
            throw new InvalidOperationException("Theme row id is required.");
        }

        var resolution = await ResolveBrowserAnimeForItemAsync(item, cancellationToken).ConfigureAwait(false);
        var anime = resolution.Anime
            ?? throw new InvalidOperationException("No AnimeThemes resource was found for this item.");
        if (anime.AnimeThemes == null)
        {
            throw new InvalidOperationException("No AnimeThemes themes were found for this item.");
        }

        var candidates = ThemeFilePlanner.GetBrowserCandidates(anime.AnimeThemes);
        for (var i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(ThemeFilePlanner.BuildBrowserRowId(candidates[i]), rowId, StringComparison.OrdinalIgnoreCase))
            {
                return (anime, candidates[i], i + 1);
            }
        }

        throw new KeyNotFoundException("The requested theme row was not found.");
    }

    private static void ValidateLocalMediaPath(string itemPath, string mediaPath)
    {
        var root = Path.GetFullPath(itemPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(mediaPath);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The requested local media path is outside the library item.");
        }

        var extension = Path.GetExtension(fullPath);
        if (!extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The requested local media type is not supported.");
        }

        var relative = fullPath[root.Length..];
        var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault();
        if (!string.Equals(firstSegment, "backdrops", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(firstSegment, "theme-music", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(firstSegment, "extras", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The requested local media path is not managed by AnimeThemes Sync.");
        }
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
        if (item is not Series && item is not Movie && item is not Season)
        {
            throw new InvalidOperationException("Only Series, Season, and Movie items are supported.");
        }

        var resolution = await ResolveBrowserAnimeForItemAsync(item, cancellationToken).ConfigureAwait(false);
        var animeThemesUrl = BuildAnimeThemesUrl(resolution.Anime);
        var rows = BuildBrowserRowsForResolution(item, resolution, config);
        var groups = item is Series series
            ? await BuildBrowserThemeGroupsAsync(series, resolution.Anime, rows, config, cancellationToken).ConfigureAwait(false)
            : new List<ThemeBrowserThemeGroup>
            {
                BuildBrowserThemeGroup(
                    item,
                    item is Season ? "Season" : "Movie",
                    item is Season ? item.IndexNumber : null,
                    resolution,
                    rows,
                    null,
                    null)
            };

        return new ThemeBrowserItemResult(
            item.Id,
            item.Name ?? "Unknown",
            item is Season ? "Season" : item is Series ? "Series" : "Movie",
            resolution.Anime?.Slug,
            animeThemesUrl,
            rows,
            groups);
    }

    private async Task<List<ThemeBrowserThemeGroup>> BuildBrowserThemeGroupsAsync(
        Series series,
        AnimeThemesAnime? seriesAnime,
        List<ThemeBrowserThemeRow> seriesRows,
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        var groups = new List<ThemeBrowserThemeGroup>();
        var seasons = GetSeasonItems(series);
        var seasonsWithPath = seasons.Where(s => !string.IsNullOrWhiteSpace(s.Path)).ToList();
        if (seasonsWithPath.Count == 0)
        {
            groups.Add(BuildBrowserThemeGroup(
                series,
                "Series",
                null,
                new BrowserAnimeResolution(seriesAnime, "Series", "SeriesLevel", false),
                seriesRows,
                null,
                null));
            return groups;
        }

        var automaticSeasonAnime = seriesAnime == null
            ? new Dictionary<Guid, AnimeThemesAnime>()
            : await BuildAutomaticSeasonAnimeMapAsync(series, seasons, seriesAnime, cancellationToken).ConfigureAwait(false);

        foreach (var season in seasonsWithPath)
        {
            var resolution = await ResolveSeasonBrowserAnimeAsync(season, seriesAnime, automaticSeasonAnime, cancellationToken).ConfigureAwait(false);
            var rows = resolution.SameAsSeries ? seriesRows : BuildBrowserRowsForResolution(season, resolution, config);
            groups.Add(BuildBrowserThemeGroup(
                season,
                "Season",
                season.IndexNumber,
                resolution,
                rows,
                rows.Count == 0 && resolution.SameAsSeries ? "Uses series-level themes, but no series-level themes are available." : null,
                resolution.SameAsSeries ? series.Id : null));
        }

        return groups;
    }

    private ThemeBrowserThemeGroup BuildBrowserThemeGroup(
        BaseItem item,
        string type,
        int? seasonNumber,
        BrowserAnimeResolution resolution,
        List<ThemeBrowserThemeRow> rows,
        string? emptyMessage,
        Guid? actionItemId)
    {
        return new ThemeBrowserThemeGroup(
            actionItemId ?? item.Id,
            item.Name ?? type,
            type,
            seasonNumber,
            resolution.Status,
            resolution.Source,
            resolution.SameAsSeries,
            resolution.Anime?.Name,
            resolution.Anime?.Slug,
            BuildAnimeThemesUrl(resolution.Anime),
            BuildImageUrl(item, ImageType.Primary, "Primary"),
            BuildImageUrl(item, ImageType.Backdrop, "Backdrop/0"),
            BuildImageUrl(item, ImageType.Thumb, "Thumb"),
            emptyMessage,
            rows);
    }

    private List<ThemeBrowserThemeRow> BuildBrowserRowsForResolution(
        BaseItem item,
        BrowserAnimeResolution resolution,
        PluginConfiguration config)
    {
        if (resolution.SameAsSeries || resolution.Anime?.AnimeThemes == null)
        {
            return new List<ThemeBrowserThemeRow>();
        }

        return BuildBrowserRows(item, resolution.Anime, config);
    }

    private async Task<BrowserAnimeResolution> ResolveBrowserAnimeForItemAsync(
        BaseItem item,
        CancellationToken cancellationToken)
    {
        if (item is Season season)
        {
            var series = FindSeriesForSeason(season);
            var seriesAnime = series == null
                ? null
                : await ResolveAnime(series, cancellationToken, logMissingIds: false).ConfigureAwait(false);
            var automaticSeasonAnime = series == null || seriesAnime == null
                ? new Dictionary<Guid, AnimeThemesAnime>()
                : await BuildAutomaticSeasonAnimeMapAsync(series, GetSeasonItems(series), seriesAnime, cancellationToken).ConfigureAwait(false);
            return await ResolveSeasonBrowserAnimeAsync(season, seriesAnime, automaticSeasonAnime, cancellationToken).ConfigureAwait(false);
        }

        var anime = await ResolveAnime(item, cancellationToken).ConfigureAwait(false);
        return new BrowserAnimeResolution(
            anime,
            item is Series ? "Series" : "Direct",
            item is Series ? "SeriesLevel" : "ItemProviderIds",
            false);
    }

    private async Task<BrowserAnimeResolution> ResolveSeasonBrowserAnimeAsync(
        Season season,
        AnimeThemesAnime? seriesAnime,
        Dictionary<Guid, AnimeThemesAnime> automaticSeasonAnime,
        CancellationToken cancellationToken)
    {
        var mapping = FindSeasonThemeMapping(season);
        AnimeThemesAnime? anime = null;
        var status = "Unmatched";
        var source = "None";

        if (mapping != null)
        {
            anime = await ResolveAnime(season, cancellationToken, logMissingIds: false).ConfigureAwait(false);
            status = "Manual";
            source = "SeasonThemeMappings";
        }
        else if (HasProviderIdentity(season))
        {
            anime = await ResolveAnime(season, cancellationToken, logMissingIds: false).ConfigureAwait(false);
            if (anime != null)
            {
                status = "Direct";
                source = "SeasonProviderIds";
            }
        }

        if (anime == null && automaticSeasonAnime.TryGetValue(season.Id, out var automaticAnime))
        {
            anime = automaticAnime;
            status = "Auto";
            source = "AniListRelations";
        }

        var sameAsSeries = seriesAnime != null && anime != null && IsSameAnime(seriesAnime, anime);
        if (anime == null && season.IndexNumber == 1 && seriesAnime != null)
        {
            anime = seriesAnime;
            sameAsSeries = true;
            status = "Series";
            source = "SeriesLevel";
        }
        else if (sameAsSeries && status != "Manual")
        {
            status = "Series";
            source = "SeriesLevel";
        }

        return new BrowserAnimeResolution(anime, status, source, sameAsSeries);
    }

    private static string? BuildAnimeThemesUrl(AnimeThemesAnime? anime)
    {
        return !string.IsNullOrWhiteSpace(anime?.Slug)
            ? Constants.AnimeThemesWebUrl + "/anime/" + anime.Slug
            : null;
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

                if (config.AllowDelete && result.Themes != null)
                {
                    cleanupTasks.AddRange(result.CleanupPlans.Select(c => (c.Directory, c.DesiredFiles, c.Themes)));
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
        if (item is Series or Season)
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

        var config = Plugin.Instance?.Configuration;
        var anime = await ResolveAnime(item, cancellationToken).ConfigureAwait(false);
        if (anime == null && item is Season seasonItem)
        {
            var browserResolution = await ResolveBrowserAnimeForItemAsync(seasonItem, cancellationToken).ConfigureAwait(false);
            if (!browserResolution.SameAsSeries)
            {
                anime = browserResolution.Anime;
            }
        }

        var plans = new List<ThemeOutputPlan>();
        if (anime?.AnimeThemes != null)
        {
            plans.Add(ThemeFilePlanner.BuildPlan(anime, item.Path, audioConfig, videoConfig, config?.ExtrasEnabled ?? false));
        }
        else
        {
            _logger.LogWarning("  No series-level themes found for {ItemName}. Checking mapped seasons.", item.Name);
        }

        if (item is not Series series)
        {
            if (plans.Count == 0)
            {
                return null;
            }

            return ThemeFilePlanner.MergePlans(plans);
        }

        var seasons = GetSeasonItems(series);
        var automaticSeasonAnime = anime == null
            ? new Dictionary<Guid, AnimeThemesAnime>()
            : await BuildAutomaticSeasonAnimeMapAsync(series, seasons, anime, cancellationToken).ConfigureAwait(false);

        foreach (var season in seasons)
        {
            if (string.IsNullOrWhiteSpace(season.Path))
            {
                continue;
            }

            var seasonMapping = FindSeasonThemeMapping(season);
            var seasonAnime = await ResolveAnime(season, cancellationToken, logMissingIds: false).ConfigureAwait(false);
            if (seasonAnime == null && automaticSeasonAnime.TryGetValue(season.Id, out var automaticAnime))
            {
                seasonAnime = automaticAnime;
            }

            if (seasonAnime?.AnimeThemes == null)
            {
                continue;
            }

            if (anime != null && IsSameAnime(seasonAnime, anime))
            {
                _logger.LogInformation(
                    "  Skipping season theme output for {SeriesName} / {SeasonName}; it resolves to the series-level AnimeThemes entry.",
                    item.Name,
                    season.Name);
                continue;
            }

            _logger.LogInformation(
                "  Adding season theme output for {SeriesName} / {SeasonName}: {AnimeName}",
                item.Name,
                season.Name,
                seasonAnime.Name ?? seasonAnime.Slug ?? "Unknown");
            var outputPath = ShouldOutputMappedSeasonToSeriesRoot(season, seasonMapping) ? series.Path : season.Path;
            plans.Add(ThemeFilePlanner.BuildPlan(seasonAnime, outputPath, audioConfig, videoConfig, config?.ExtrasEnabled ?? false));
        }

        if (plans.Count == 0)
        {
            return null;
        }

        return ThemeFilePlanner.MergePlans(plans);
    }

    private static bool ShouldOutputMappedSeasonToSeriesRoot(Season season, SeasonThemeMapping? mapping)
    {
        return mapping != null && (!season.IndexNumber.HasValue || season.IndexNumber.Value <= 1);
    }

    private SeasonThemeMappingRow BuildSeasonMappingRow(Series series, Season season)
    {
        var mapping = FindSeasonThemeMapping(season);
        var seasonIds = ExtractItemProviderIds(season);
        var seriesIds = ExtractItemProviderIds(series);
        var seasonSlug = GetItemAnimeThemesSlug(season);
        var seriesSlug = GetItemAnimeThemesSlug(series);

        var status = "Unmatched";
        var source = "None";
        var sameAsSeries = false;
        string? animeName = null;
        string? animeThemesSlug = null;
        int? aniListId = seasonIds.AniListId;
        int? myAnimeListId = seasonIds.MyAnimeListId;

        if (mapping != null)
        {
            status = "Manual";
            source = "SeasonThemeMappings";
            animeThemesSlug = mapping.AnimeThemesSlug ?? seasonSlug;
            aniListId = mapping.AniListId ?? seasonIds.AniListId;
            myAnimeListId = mapping.MyAnimeListId ?? seasonIds.MyAnimeListId;
            animeName = animeThemesSlug ?? season.Name;
        }
        else if (HasProviderIdentity(season))
        {
            status = "Direct";
            source = "SeasonProviderIds";
            animeThemesSlug = seasonSlug;
            animeName = season.Name;
        }
        else if (season.IndexNumber == 1 && HasProviderIdentity(series))
        {
            status = "Series";
            source = "SeriesLevel";
            sameAsSeries = true;
            animeThemesSlug = seriesSlug;
            aniListId = seriesIds.AniListId;
            myAnimeListId = seriesIds.MyAnimeListId;
            animeName = series.Name;
        }

        var animeThemesUrl = !string.IsNullOrWhiteSpace(animeThemesSlug)
            ? Constants.AnimeThemesWebUrl + "/anime/" + animeThemesSlug
            : null;

        return new SeasonThemeMappingRow(
            series.Id,
            series.Name ?? "Unknown",
            series.Path,
            season.Id,
            season.Name ?? "Season",
            season.Path,
            season.IndexNumber,
            status,
            source,
            sameAsSeries,
            animeName,
            null,
            animeThemesSlug,
            animeThemesUrl,
            aniListId,
            myAnimeListId,
            BuildImageUrl(season, ImageType.Primary, "Primary") ?? BuildImageUrl(series, ImageType.Primary, "Primary"));
    }

    private async Task<Dictionary<Guid, AnimeThemesAnime>> BuildAutomaticSeasonAnimeMapAsync(
        Series series,
        List<Season> seasons,
        AnimeThemesAnime seriesAnime,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<Guid, AnimeThemesAnime>();
        var numberedSeasons = seasons
            .Where(s => s.IndexNumber.HasValue && s.IndexNumber.Value > 1)
            .OrderBy(s => s.IndexNumber!.Value)
            .ToList();

        if (numberedSeasons.Count == 0 ||
            !TryGetProviderInt(series, Constants.AniListProviderId, out var seriesAniListId))
        {
            return map;
        }

        var related = await _aniListService.GetRelatedAnimeChainAsync(
            seriesAniListId,
            Math.Min(6, Math.Max(2, seasons.Count)),
            cancellationToken).ConfigureAwait(false);

        var resolved = new List<(AniListRelatedAnime Related, AnimeThemesAnime Anime)>();
        foreach (var candidate in related.Where(IsSeriesFormatCandidate))
        {
            var anime = await ResolveAnimeByExternalIds(candidate.AniListId, candidate.MyAnimeListId, cancellationToken).ConfigureAwait(false);
            if (anime?.AnimeThemes == null || resolved.Any(r => IsSameAnime(r.Anime, anime)))
            {
                continue;
            }

            resolved.Add((candidate, anime));
        }

        var rootIndex = resolved.FindIndex(r => r.Related.AniListId == seriesAniListId || IsSameAnime(r.Anime, seriesAnime));
        if (rootIndex < 0)
        {
            return map;
        }

        foreach (var season in numberedSeasons)
        {
            var candidateIndex = rootIndex + season.IndexNumber!.Value - 1;
            if (candidateIndex >= resolved.Count)
            {
                continue;
            }

            map[season.Id] = resolved[candidateIndex].Anime;
            _logger.LogInformation(
                "  Auto-mapped {SeriesName} / {SeasonName} to AnimeThemes anime {AnimeName} via AniList relations.",
                series.Name,
                season.Name,
                resolved[candidateIndex].Anime.Name ?? resolved[candidateIndex].Anime.Slug ?? "Unknown");
        }

        return map;
    }

    private async Task<SeasonThemeMappingRow> BuildSeasonMappingRowAsync(
        Series series,
        Season season,
        AnimeThemesAnime? seriesAnime,
        Dictionary<Guid, AnimeThemesAnime> automaticSeasonAnime,
        CancellationToken cancellationToken)
    {
        var mapping = FindSeasonThemeMapping(season);
        AnimeThemesAnime? anime = null;
        var status = "Unmatched";
        var source = "None";

        if (mapping != null)
        {
            anime = await ResolveAnime(season, cancellationToken, logMissingIds: false).ConfigureAwait(false);
            status = "Manual";
            source = "SeasonThemeMappings";
        }
        else if (HasProviderIdentity(season))
        {
            anime = await ResolveAnime(season, cancellationToken, logMissingIds: false).ConfigureAwait(false);
            if (anime != null)
            {
                status = "Direct";
                source = "SeasonProviderIds";
            }
        }

        if (anime == null && automaticSeasonAnime.TryGetValue(season.Id, out var automaticAnime))
        {
            anime = automaticAnime;
            status = "Auto";
            source = "AniListRelations";
        }

        var sameAsSeries = seriesAnime != null && anime != null && IsSameAnime(seriesAnime, anime);
        if (anime == null && season.IndexNumber == 1 && seriesAnime != null)
        {
            anime = seriesAnime;
            sameAsSeries = true;
            status = "Series";
            source = "SeriesLevel";
        }
        else if (sameAsSeries && status != "Manual")
        {
            status = "Series";
            source = "SeriesLevel";
        }

        var ids = ExtractAnimeExternalIds(anime);
        var fallbackIds = ExtractItemProviderIds(season);
        var animeThemesUrl = !string.IsNullOrWhiteSpace(anime?.Slug)
            ? Constants.AnimeThemesWebUrl + "/anime/" + anime.Slug
            : null;

        return new SeasonThemeMappingRow(
            series.Id,
            series.Name ?? "Unknown",
            series.Path,
            season.Id,
            season.Name ?? "Season",
            season.Path,
            season.IndexNumber,
            status,
            source,
            sameAsSeries,
            anime?.Name,
            anime?.Id > 0 ? anime.Id : null,
            anime?.Slug,
            animeThemesUrl,
            mapping?.AniListId ?? ids.AniListId ?? fallbackIds.AniListId,
            mapping?.MyAnimeListId ?? ids.MyAnimeListId ?? fallbackIds.MyAnimeListId,
            BuildImageUrl(season, ImageType.Primary, "Primary") ?? BuildImageUrl(series, ImageType.Primary, "Primary"));
    }

    private static string? GetAnimePrimaryImageUrl(AnimeThemesAnime? anime)
    {
        var images = anime?.Images;
        if (images == null || images.Count == 0)
        {
            return null;
        }

        return images.FirstOrDefault(i => string.Equals(i.Facet, "Small Cover", StringComparison.OrdinalIgnoreCase))?.Link
            ?? images.FirstOrDefault(i => string.Equals(i.Facet, "Large Cover", StringComparison.OrdinalIgnoreCase))?.Link
            ?? images.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Link))?.Link;
    }

    private static ThemeFinderSearchResult ToThemeFinderSearchResult(AnimeThemesAnime anime, int score, string? imageUrl, string query)
    {
        var ids = ExtractAnimeExternalIds(anime);
        var match = FindMatchedTitle(anime, query);
        return new ThemeFinderSearchResult(
            anime.Id,
            anime.Name ?? anime.Slug ?? "AnimeThemes",
            anime.Slug,
            anime.Year,
            anime.Season,
            ids.AniListId,
            ids.MyAnimeListId,
            !string.IsNullOrWhiteSpace(anime.Slug) ? Constants.AnimeThemesWebUrl + "/anime/" + anime.Slug : null,
            score,
            imageUrl,
            anime.MediaFormat,
            match.Title,
            match.Type);
    }

    private static int ScoreSearchCandidate(AnimeThemesAnime anime, string query, int? year)
    {
        var normalizedQuery = NormalizeSearchText(query);
        var normalizedSlug = NormalizeSearchText(anime.Slug);
        var normalizedNames = GetAnimeTitleCandidates(anime)
            .Select(NormalizeSearchText)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var score = 0;
        if (normalizedNames.Any(name => name == normalizedQuery))
        {
            score += 100;
        }
        else if (normalizedNames.Any(name =>
            name.Contains(normalizedQuery, StringComparison.Ordinal) ||
            normalizedQuery.Contains(name, StringComparison.Ordinal)))
        {
            score += 70;
        }
        else if (!string.IsNullOrWhiteSpace(normalizedSlug) && normalizedSlug.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            score += 55;
        }

        if (year.HasValue && anime.Year.HasValue)
        {
            var delta = Math.Abs(anime.Year.Value - year.Value);
            score += delta == 0 ? 30 : delta == 1 ? 15 : 0;
        }

        return score;
    }

    private static IEnumerable<string?> GetAnimeTitleCandidates(AnimeThemesAnime anime)
    {
        yield return anime.Name;

        foreach (var synonym in anime.Synonyms ?? [])
        {
            yield return synonym.Text;
        }
    }

    private static (string? Title, string? Type) FindMatchedTitle(AnimeThemesAnime anime, string query)
    {
        var normalizedQuery = NormalizeSearchText(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return (null, null);
        }

        foreach (var synonym in anime.Synonyms ?? [])
        {
            var normalizedTitle = NormalizeSearchText(synonym.Text);
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                continue;
            }

            if (normalizedTitle == normalizedQuery ||
                normalizedTitle.Contains(normalizedQuery, StringComparison.Ordinal) ||
                normalizedQuery.Contains(normalizedTitle, StringComparison.Ordinal))
            {
                return (synonym.Text, synonym.Type);
            }
        }

        return (null, null);
    }

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray();
        return string.Join(" ", new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static (int? AniListId, int? MyAnimeListId) ExtractAnimeExternalIds(AnimeThemesAnime? anime)
    {
        int? aniListId = null;
        int? myAnimeListId = null;
        foreach (var resource in anime?.Resources ?? [])
        {
            if (resource.ExternalId == null || string.IsNullOrWhiteSpace(resource.Site))
            {
                continue;
            }

            if (string.Equals(resource.Site, Constants.AniListSiteKey, StringComparison.OrdinalIgnoreCase))
            {
                aniListId = resource.ExternalId;
            }
            else if (string.Equals(resource.Site, Constants.MyAnimeListSiteKey, StringComparison.OrdinalIgnoreCase))
            {
                myAnimeListId = resource.ExternalId;
            }
        }

        return (aniListId, myAnimeListId);
    }

    private static (int? AniListId, int? MyAnimeListId) ExtractItemProviderIds(BaseItem item)
    {
        int? aniListId = null;
        int? myAnimeListId = null;
        if (item.ProviderIds.TryGetValue(Constants.AniListProviderId, out var aniListRaw) &&
            int.TryParse(aniListRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aid))
        {
            aniListId = aid;
        }

        if (item.ProviderIds.TryGetValue(Constants.MyAnimeListProviderId, out var malRaw) &&
            int.TryParse(malRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mid))
        {
            myAnimeListId = mid;
        }

        return (aniListId, myAnimeListId);
    }

    private static string? GetItemAnimeThemesSlug(BaseItem item)
    {
        return item.ProviderIds.TryGetValue(Constants.AnimeThemesProviderId, out var slug) &&
               !string.IsNullOrWhiteSpace(slug)
            ? slug.Trim()
            : null;
    }

    private static bool HasProviderIdentity(BaseItem item)
    {
        return item.ProviderIds.ContainsKey(Constants.AnimeThemesProviderId) ||
               item.ProviderIds.ContainsKey(Constants.AniListProviderId) ||
               item.ProviderIds.ContainsKey(Constants.MyAnimeListProviderId);
    }

    private Series? FindSeriesForSeason(Season season)
    {
        return GetEnabledLibraryItems()
            .OfType<Series>()
            .FirstOrDefault(series => GetSeasonItems(series).Any(candidate => candidate.Id == season.Id));
    }

    private static void RemoveSeasonMappings(List<SeasonThemeMapping> mappings, Season season)
    {
        var seasonItemId = season.Id.ToString("D");
        var compactSeasonItemId = season.Id.ToString("N");
        var seasonPath = NormalizeMappingPath(season.Path);
        var seriesPath = NormalizeMappingPath(Path.GetDirectoryName(season.Path));
        var seasonNumber = season.IndexNumber;

        mappings.RemoveAll(mapping =>
            MatchesId(mapping.SeasonItemId, seasonItemId, compactSeasonItemId) ||
            MatchesPath(mapping.SeasonPath, seasonPath) ||
            (mapping.SeasonNumber.HasValue &&
             seasonNumber == mapping.SeasonNumber.Value &&
             MatchesPath(mapping.SeriesPath, seriesPath)));
    }

    private async Task<AnimeThemesAnime?> ResolveAnime(
        BaseItem item,
        CancellationToken cancellationToken,
        bool logMissingIds = true)
    {
        var mapping = item is Season ? FindSeasonThemeMapping(item) : null;
        int? aniListId = null;
        int? malId = null;
        string? animeThemesSlug = mapping?.AnimeThemesSlug;

        if (mapping?.AniListId is int mappedAniListId)
        {
            aniListId = mappedAniListId;
        }

        if (mapping?.MyAnimeListId is int mappedMalId)
        {
            malId = mappedMalId;
        }

        if (aniListId == null &&
            item.ProviderIds.TryGetValue(Constants.AniListProviderId, out var aniListIdStr) &&
            int.TryParse(aniListIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aid))
        {
            aniListId = aid;
        }

        if (malId == null &&
            item.ProviderIds.TryGetValue(Constants.MyAnimeListProviderId, out var malIdStr) &&
            int.TryParse(malIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mid))
        {
            malId = mid;
        }

        if (string.IsNullOrWhiteSpace(animeThemesSlug) &&
            item.ProviderIds.TryGetValue(Constants.AnimeThemesProviderId, out var providerSlug) &&
            !string.IsNullOrEmpty(providerSlug))
        {
            animeThemesSlug = providerSlug;
        }

        if (aniListId == null && malId == null &&
            string.IsNullOrWhiteSpace(animeThemesSlug))
        {
            if (logMissingIds)
            {
                _logger.LogWarning("  No AnimeThemes, AniList, or MAL ID found for {ItemName}. Skipping.", item.Name);
            }

            return null;
        }

        // Get AnimeThemes Data
        AnimeThemesAnime? anime = null;
        if (!string.IsNullOrWhiteSpace(animeThemesSlug))
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

    private async Task<AnimeThemesAnime?> ResolveAnimeByExternalIds(
        int? aniListId,
        int? malId,
        CancellationToken cancellationToken)
    {
        AnimeThemesAnime? anime = null;
        if (aniListId.HasValue)
        {
            anime = await _animeThemesService.GetAnimeByExternalId(Constants.AniListSiteKey, aniListId.Value, cancellationToken).ConfigureAwait(false);
        }

        if (anime == null && malId.HasValue)
        {
            anime = await _animeThemesService.GetAnimeByExternalId(Constants.MyAnimeListSiteKey, malId.Value, cancellationToken).ConfigureAwait(false);
        }

        return anime;
    }

    private static bool TryGetProviderInt(BaseItem item, string key, out int value)
    {
        value = 0;
        return item.ProviderIds.TryGetValue(key, out var raw) &&
               int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsSeriesFormatCandidate(AniListRelatedAnime candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Format))
        {
            return true;
        }

        return !string.Equals(candidate.Format, "MOVIE", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(candidate.Format, "OVA", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(candidate.Format, "SPECIAL", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(candidate.Format, "MUSIC", StringComparison.OrdinalIgnoreCase);
    }

    private SeasonThemeMapping? FindSeasonThemeMapping(BaseItem season)
    {
        var mappings = Plugin.Instance?.Configuration?.SeasonThemeMappings;
        if (mappings == null || mappings.Count == 0)
        {
            return null;
        }

        var seasonItemId = season.Id.ToString("D");
        var compactSeasonItemId = season.Id.ToString("N");
        var seasonPath = NormalizeMappingPath(season.Path);
        var seriesPath = NormalizeMappingPath(Path.GetDirectoryName(season.Path));
        var seasonNumber = season.IndexNumber;

        return mappings.FirstOrDefault(mapping =>
            mapping.Enabled &&
            HasThemeIdentity(mapping) &&
            (MatchesId(mapping.SeasonItemId, seasonItemId, compactSeasonItemId) ||
             MatchesPath(mapping.SeasonPath, seasonPath) ||
             (mapping.SeasonNumber.HasValue &&
              seasonNumber == mapping.SeasonNumber.Value &&
              MatchesPath(mapping.SeriesPath, seriesPath))));
    }

    private static bool HasThemeIdentity(SeasonThemeMapping mapping)
    {
        return !string.IsNullOrWhiteSpace(mapping.AnimeThemesSlug) ||
               mapping.AniListId.HasValue ||
               mapping.MyAnimeListId.HasValue;
    }

    private static bool MatchesId(string? configuredId, string itemId, string compactItemId)
    {
        return !string.IsNullOrWhiteSpace(configuredId) &&
               (string.Equals(configuredId.Trim(), itemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(configuredId.Trim(), compactItemId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesPath(string? configuredPath, string itemPath)
    {
        return !string.IsNullOrWhiteSpace(configuredPath) &&
               string.Equals(NormalizeMappingPath(configuredPath), itemPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMappingPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private List<Season> GetSeasonItems(Series series)
    {
        var seasons = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Season },
            Recursive = false,
            Parent = series
        });

        return seasons
            .OfType<Season>()
            .OrderBy(s => s.IndexNumber ?? int.MaxValue)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSameAnime(AnimeThemesAnime left, AnimeThemesAnime right)
    {
        if (left.Id > 0 && right.Id > 0)
        {
            return left.Id == right.Id;
        }

        return !string.IsNullOrWhiteSpace(left.Slug) &&
               !string.IsNullOrWhiteSpace(right.Slug) &&
               string.Equals(left.Slug, right.Slug, StringComparison.OrdinalIgnoreCase);
    }

    private List<ThemeBrowserThemeRow> BuildBrowserRows(BaseItem item, AnimeThemesAnime anime, PluginConfiguration config)
    {
        return BuildBrowserRowsForPath(item.Path, anime, config);
    }

    private List<ThemeBrowserThemeRow> BuildBrowserRowsForPath(string itemPath, AnimeThemesAnime anime, PluginConfiguration config)
    {
        return ThemeFilePlanner.GetBrowserCandidates(anime.AnimeThemes!)
            .Select((c, index) =>
            {
                var order = index + 1;
                var plan = ThemeFilePlanner.BuildSingleCandidatePlan(
                    anime,
                    c,
                    order,
                    itemPath,
                    includeAudio: true,
                    includeVideo: true,
                    includeExtras: config.ExtrasEnabled);
                return BuildBrowserRow(
                    c,
                    order,
                    anime,
                    plan.MediaFiles.FirstOrDefault(f => f.IsVideo),
                    plan.MediaFiles.FirstOrDefault(f => !f.IsVideo),
                    plan.ExtraFiles.FirstOrDefault());
            })
            .ToList();
    }

    private ThemeBrowserThemeRow BuildBrowserRow(
        ScoredCandidate candidate,
        int order,
        AnimeThemesAnime anime,
        ThemeFilePlan? videoPlan,
        ThemeFilePlan? audioPlan,
        ThemeExtraPlan? extraPlan)
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
            videoPlan != null && _fileSystem.FileExists(videoPlan.Path),
            videoPlan != null && _fileSystem.FileExists(videoPlan.Path),
            audioPlan?.Path,
            audioPlan != null && _fileSystem.FileExists(audioPlan.Path),
            audioPlan != null && _fileSystem.FileExists(audioPlan.Path),
            extraPlan?.TargetPath,
            extraPlan != null && _fileSystem.FileExists(extraPlan.TargetPath),
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

    private sealed record BrowserAnimeResolution(
        AnimeThemesAnime? Anime,
        string Status,
        string Source,
        bool SameAsSeries);
}
