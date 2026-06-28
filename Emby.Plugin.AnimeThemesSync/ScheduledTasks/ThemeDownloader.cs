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
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using Emby.Plugin.AnimeThemesSync.Extensions;
using Emby.Plugin.AnimeThemesSync.Helpers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using Emby.Plugin.AnimeThemesSync.Configuration;

namespace Emby.Plugin.AnimeThemesSync.ScheduledTasks;

/// <summary>
/// Scheduled task to download OP/ED themes.
/// </summary>
public class ThemeDownloader : IScheduledTask
{
    private static readonly object LibraryMonitorSync = new();
    private static Timer? _libraryChangeTimer;
    private static ThemeDownloader? _libraryMonitorDownloader;
    private static int _browserCacheRebuildRunning;
    private readonly AdjustableConcurrencyLimiter _downloadLimiter = new();

    private readonly ILibraryManager _libraryManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly AnimeThemesService _animeThemesService;
    private readonly AniListService _aniListService;
    private readonly AnimeThemesDataStore _dataStore;
    private readonly ISeasonFinderDataStore _seasonFinderStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeDownloader"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="logManager">The log manager.</param>
    /// <param name="mediaEncoder">The media encoder.</param>
    public ThemeDownloader(
        ILibraryManager libraryManager,
        IFileSystem fileSystem,
        ILogManager logManager,
        IMediaEncoder mediaEncoder,
        IApplicationPaths applicationPaths)
    {
        _libraryManager = libraryManager;
        _fileSystem = fileSystem;
        _logger = logManager.GetLogger(nameof(ThemeDownloader));
        _httpClientFactory = new StaticHttpClientFactory();
        _mediaEncoder = mediaEncoder;
        var pathProvider = new EmbyAnimeThemesDataPathProvider(applicationPaths);
        var serverIdentity = new EmbyAnimeThemesServerIdentityProvider();
        _dataStore = new AnimeThemesDataStore(pathProvider, serverIdentity);
        _dataStore.EnsureInitialized();
        _seasonFinderStore = new EmbySeasonFinderDataStore(pathProvider, serverIdentity);
        _seasonFinderStore.EnsureInitialized();
        _seasonFinderStore.MigrateLegacyMappings(Plugin.Instance?.Configuration?.SeasonThemeMappings);
        var aniListLogger = new EmbyLoggerAdapter<AniListService>(new EmbyLoggerAdapter(logManager.GetLogger(nameof(AniListService))));
        var rateLimiterLogger = new EmbyLoggerAdapter(logManager.GetLogger(nameof(RateLimiter)));
        var animeThemesLogger = new EmbyLoggerAdapter<AnimeThemesService>(new EmbyLoggerAdapter(logManager.GetLogger(nameof(AnimeThemesService))));
        var aniListRateLimiter = new RateLimiter(new EmbyLoggerAdapter(logManager.GetLogger("AniListRateLimiter")), Constants.AniListHttpClientName, 90);
        var rateLimiter = new RateLimiter(rateLimiterLogger, Constants.AnimeThemesHttpClientName, 80);
        _aniListService = new AniListService(_httpClientFactory, aniListLogger, aniListRateLimiter);
        _animeThemesService = new AnimeThemesService(_httpClientFactory, animeThemesLogger, rateLimiter, _seasonFinderStore);
        ThemeExtrasManifestService.ConfigureStore(_dataStore);
        ThemeDownloadJobService.Configure(Plugin.Instance?.Configuration?.MaxConcurrentDownloads ?? 2);
        RegisterLibraryMonitor();
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
    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
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
        await RebuildBrowserCacheAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Anime Themes Download Task Completed. Downloaded {Count} files.", result.DownloadsCompleted);
    }

    /// <summary>
    /// Downloads themes for a single library item.
    /// </summary>
    /// <param name="itemId">The Emby item identifier.</param>
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

        EnsureSeasonThemeDownloadsAllowed(item, config);

        _logger.LogInformation("Starting Anime Themes on-demand download for {ItemName} ({ItemId})...", item.Name, itemId);
        var result = await ProcessItems(new[] { item }, config, forceRedownload || config.ForceRedownload, progress, cancellationToken).ConfigureAwait(false);
        RefreshBrowserCacheForItem(item);
        return result;
    }

    /// <summary>
    /// Gets browser candidates from AnimeThemes-enabled libraries.
    /// </summary>
    /// <returns>The candidate items.</returns>
    public ThemeBrowserItemsPage GetBrowserItems(
        string? libraryId,
        int? startIndex,
        int? limit,
        string? sortBy,
        string? sortOrder,
        string? searchTerm,
        string? itemType,
        string? linkFilter,
        string? savedFilter)
    {
        EnsureBrowserCacheRebuildStarted();
        return _dataStore.QueryBrowserItems(libraryId, startIndex, limit, sortBy, sortOrder, searchTerm, itemType, linkFilter, savedFilter);
    }

    public AnimeThemesStorageStatus GetStorageStatus()
    {
        EnsureBrowserCacheRebuildStarted();
        return _dataStore.GetStorageStatus(IsBrowserCacheRebuildRunning) with { SeasonFinder = _seasonFinderStore.GetStorageStatus() };
    }

    public AnimeThemesMaintenanceResult ClearBrowserCache()
    {
        _dataStore.ClearBrowserCache();
        _seasonFinderStore.ClearCache();
        _animeThemesService.ClearSearchCache();
        return new AnimeThemesMaintenanceResult(true, "Browser cache cleared.");
    }

    public AnimeThemesMaintenanceResult StartBrowserCacheRebuild()
    {
        if (Interlocked.CompareExchange(ref _browserCacheRebuildRunning, 1, 0) != 0)
        {
            return new AnimeThemesMaintenanceResult(false, "Browser cache rebuild is already running.");
        }

        _dataStore.SetBrowserCacheRebuildError(null);
        _seasonFinderStore.SetRebuildError(null);
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await RebuildBrowserCacheCoreAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _dataStore.SetBrowserCacheRebuildError(ex.Message);
                    _seasonFinderStore.SetRebuildError(ex.Message);
                    _logger.LogError(ex, "Browser cache rebuild failed.");
                }
                finally
                {
                    Interlocked.Exchange(ref _browserCacheRebuildRunning, 0);
                }
            });
        return new AnimeThemesMaintenanceResult(true, "Browser cache rebuild started.");
    }

    public async Task RebuildBrowserCacheAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _browserCacheRebuildRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _dataStore.SetBrowserCacheRebuildError(null);
            _seasonFinderStore.SetRebuildError(null);
            await RebuildBrowserCacheCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _dataStore.SetBrowserCacheRebuildError(ex.Message);
            _seasonFinderStore.SetRebuildError(ex.Message);
            throw;
        }
        finally
        {
            Interlocked.Exchange(ref _browserCacheRebuildRunning, 0);
        }
    }

    public LegacyExtrasImportResult ImportLegacyExtrasManifests()
    {
        var manifests = 0;
        var files = 0;
        foreach (var item in GetEnabledLibraryItems())
        {
            ImportLegacyExtrasManifestForPath(item.Path, ref manifests, ref files);
            if (item is Series series)
            {
                foreach (var season in GetSeasonItems(series))
                {
                    ImportLegacyExtrasManifestForPath(season.Path, ref manifests, ref files);
                }
            }
        }

        return new LegacyExtrasImportResult(manifests, files);
    }

    private bool IsBrowserCacheRebuildRunning => Volatile.Read(ref _browserCacheRebuildRunning) != 0;

    public void EnsureBrowserCacheRebuildStarted()
    {
        if (!_dataStore.IsBrowserCacheReady() || !_seasonFinderStore.IsCacheReady())
        {
            _ = StartBrowserCacheRebuild();
        }
    }

    private void RegisterLibraryMonitor()
    {
        lock (LibraryMonitorSync)
        {
            if (_libraryMonitorDownloader != null)
            {
                return;
            }

            _libraryMonitorDownloader = this;
            _libraryChangeTimer = new Timer(
                static _ =>
                {
                    ThemeDownloader? downloader;
                    lock (LibraryMonitorSync)
                    {
                        downloader = _libraryMonitorDownloader;
                    }

                    _ = downloader?.StartBrowserCacheRebuild();
                },
                null,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            _libraryManager.ItemAdded += OnLibraryItemChanged;
            _libraryManager.ItemUpdated += OnLibraryItemChanged;
            _libraryManager.ItemRemoved += OnLibraryItemChanged;
            EnsureBrowserCacheRebuildStarted();
        }
    }

    private void OnLibraryItemChanged(object? sender, ItemChangeEventArgs e)
    {
        lock (LibraryMonitorSync)
        {
            _libraryChangeTimer?.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
        }
    }

    private Task RebuildBrowserCacheCoreAsync(CancellationToken cancellationToken)
    {
        var records = new List<BrowserItemRecord>();
        var seasonRecords = new List<SeasonFinderRowRecord>();
        var libraryCounts = new Dictionary<Guid, (string? Name, int Count)>();
        foreach (var entry in GetEnabledLibraryItemsWithLibraries())
        {
            cancellationToken.ThrowIfCancellationRequested();
            records.Add(BuildBrowserItemRecord(entry.Item, entry.LibraryId));
            if (entry.Item is Series series)
            {
                seasonRecords.AddRange(GetSeasonItems(series)
                    .Where(IsSeasonEligibleForThemeMatching)
                    .Where(season => !string.IsNullOrWhiteSpace(season.Path))
                    .Select(season => BuildSeasonFinderRecord(series, season, entry.LibraryId)));
            }
            libraryCounts.TryGetValue(entry.LibraryId, out var current);
            libraryCounts[entry.LibraryId] = (entry.LibraryName, current.Count + 1);
        }

        _dataStore.ReplaceBrowserItems(
            records,
            libraryCounts.Select(pair => (pair.Key.ToString("D"), pair.Value.Name, pair.Value.Count)));
        _seasonFinderStore.ReplaceRows(seasonRecords);
        _logger.LogInformation("Rebuilt AnimeThemes Browser cache. Items={0}, Seasons={1}", records.Count, seasonRecords.Count);
        return Task.CompletedTask;
    }

    private void RefreshBrowserCacheForItem(BaseItem item)
    {
        if (item is Season season)
        {
            item = FindSeriesForSeason(season) ?? item;
        }

        if (item is not Series and not Movie)
        {
            return;
        }

        var libraryId = ResolveLibraryId(item);
        _dataStore.UpsertBrowserItem(BuildBrowserItemRecord(item, libraryId));
    }

    private void ImportLegacyExtrasManifestForPath(string? itemPath, ref int manifests, ref int files)
    {
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return;
        }

        var result = _dataStore.ImportLegacyExtrasManifest(Path.Combine(itemPath, "extras"));
        manifests += result.ManifestsImported;
        files += result.FilesImported;
    }

    private BrowserItemRecord BuildBrowserItemRecord(BaseItem item, Guid? libraryId)
    {
        var (videos, songs, extras, bytes) = CountLocalThemeFilesForBrowserItem(item);
        var directLink = item.ProviderIds.TryGetValue(Constants.AnimeThemesProviderId, out var slug) && !string.IsNullOrWhiteSpace(slug);
        var seasonLinkStatus = GetSeasonLinkStatus(item);
        var linkStatus = directLink ? "Direct" : seasonLinkStatus;
        return new BrowserItemRecord
        {
            ItemId = item.Id.ToString("D"),
            LibraryId = libraryId?.ToString("D"),
            ItemType = item is Series ? "Series" : "Movie",
            Name = item.Name ?? "Unknown",
            SortName = item.SortName ?? item.Name ?? "Unknown",
            ProductionYear = item.ProductionYear,
            AnimeThemesSlug = directLink ? slug : null,
            AniListId = item.ProviderIds.TryGetValue(Constants.AniListProviderId, out var aniListId) ? aniListId : null,
            MyAnimeListId = item.ProviderIds.TryGetValue(Constants.MyAnimeListProviderId, out var malId) ? malId : null,
            LinkStatus = linkStatus,
            PrimaryImageTag = GetImageTag(item, ImageType.Primary),
            LogoImageTag = GetImageTag(item, ImageType.Logo),
            BackdropImageTag = GetImageTag(item, ImageType.Backdrop),
            ThumbImageTag = GetImageTag(item, ImageType.Thumb),
            PrimaryImageUrl = BuildImageUrl(item, ImageType.Primary, "Primary"),
            LogoImageUrl = BuildImageUrl(item, ImageType.Logo, "Logo"),
            BackdropImageUrl = BuildImageUrl(item, ImageType.Backdrop, "Backdrop/0"),
            ThumbImageUrl = BuildImageUrl(item, ImageType.Thumb, "Thumb"),
            ThemeVideoCount = videos,
            ThemeSongCount = songs,
            ThemeExtraCount = extras,
            ThemeBytes = bytes,
            HasLocalThemes = videos + songs + extras > 0,
            DateCreatedUtc = item.DateCreated.ToUniversalTime(),
            LatestEpisodeDateUtc = item is Series series ? GetLatestEpisodeDateCreated(series)?.ToUniversalTime() : null,
            LastRefreshedUtc = DateTimeOffset.UtcNow
        };
    }

    private ThemeBrowserLibraryItem BuildBrowserLibraryItem(BaseItem item)
    {
        var (videos, songs, extras, bytes) = CountLocalThemeFilesForBrowserItem(item);
        var directLink = item.ProviderIds.TryGetValue(Constants.AnimeThemesProviderId, out var slug) && !string.IsNullOrWhiteSpace(slug);
        var seasonLinkStatus = GetSeasonLinkStatus(item);
        var linkStatus = directLink ? "Direct" : seasonLinkStatus;
        return new ThemeBrowserLibraryItem(
            item.Id,
            item.Name ?? "Unknown",
            item is Series ? "Series" : "Movie",
            directLink ? slug : null,
            item.ProviderIds.TryGetValue(Constants.AniListProviderId, out var aniListId) ? aniListId : null,
            item.ProviderIds.TryGetValue(Constants.MyAnimeListProviderId, out var malId) ? malId : null,
            BuildImageUrl(item, ImageType.Primary, "Primary"),
            BuildImageUrl(item, ImageType.Logo, "Logo"),
            BuildImageUrl(item, ImageType.Backdrop, "Backdrop/0"),
            BuildImageUrl(item, ImageType.Thumb, "Thumb"),
            videos,
            songs,
            extras,
            bytes,
            videos + songs + extras > 0,
            item.DateCreated,
            item is Series series ? GetLatestEpisodeDateCreated(series) : null,
            linkStatus,
            directLink,
            string.Equals(seasonLinkStatus, "Manual", StringComparison.OrdinalIgnoreCase));
    }

    private (int Videos, int Songs, int Extras, long Bytes) CountLocalThemeFilesForBrowserItem(BaseItem item)
    {
        var videos = 0;
        var songs = 0;
        var extras = 0;
        long bytes = 0;
        var visitedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var itemTarget = ResolveThemeOutputTarget(item);
        if (itemTarget != null && visitedRoots.Add(itemTarget.OutputRootPath))
        {
            AccumulateLocalThemeDirectories(itemTarget.OutputRootPath, ref videos, ref songs, ref extras, ref bytes);
        }

        if (item is Series series && IsSeasonThemeDownloadsEnabled())
        {
            foreach (var season in GetSeasonItems(series))
            {
                var seasonTarget = ResolveThemeOutputTarget(season, series);
                if (seasonTarget != null && visitedRoots.Add(seasonTarget.OutputRootPath))
                {
                    AccumulateLocalThemeDirectories(seasonTarget.OutputRootPath, ref videos, ref songs, ref extras, ref bytes);
                }
            }
        }

        return (videos, songs, extras, bytes);
    }

    private string GetSeasonLinkStatus(BaseItem item)
    {
        if (item is not Series series)
        {
            return "Unlinked";
        }

        var mappings = _seasonFinderStore.GetSeasonThemeMappings();
        if (mappings.Count == 0)
        {
            return "Unlinked";
        }

        var hasAuto = false;
        foreach (var season in GetSeasonItems(series))
        {
            var mapping = FindSeasonThemeMapping(mappings, series, season);
            if (mapping == null)
            {
                continue;
            }

            if (mapping.Locked)
            {
                return "Manual";
            }

            hasAuto = true;
        }

        return hasAuto ? "Auto" : "Unlinked";
    }

    private DateTimeOffset? GetLatestEpisodeDateCreated(Series series)
    {
        var episodes = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { "Episode" },
            Recursive = true,
            Parent = series
        });

        return episodes
            .Select(episode => (DateTimeOffset?)episode.DateCreated)
            .OrderByDescending(date => date)
            .FirstOrDefault();
    }

    public ThemeBrowserSummary GetBrowserSummary()
    {
        EnsureBrowserCacheRebuildStarted();
        return _dataStore.GetBrowserSummary();
    }

    public Task<IReadOnlyList<SeasonThemeMappingRow>> GetSeasonThemeMappingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureBrowserCacheRebuildStarted();
        return Task.FromResult(_seasonFinderStore.GetAllRows());
    }

    public SeasonFinderItemsPage GetSeasonFinderItems(
        string? libraryId,
        int? startIndex,
        int? limit,
        string? searchTerm,
        string? status,
        string? sortBy,
        string? sortOrder)
    {
        EnsureBrowserCacheRebuildStarted();
        return _seasonFinderStore.QueryRows(libraryId, startIndex, limit, searchTerm, status, sortBy, sortOrder);
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
        var results = candidates
            .Where(a => !string.IsNullOrWhiteSpace(a.Slug))
            .GroupBy(a => !string.IsNullOrWhiteSpace(a.Slug) ? a.Slug! : a.Id.ToString(CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(15)
            .Select(a => ToThemeFinderSearchResult(a, ScoreSearchCandidate(a, query, year), GetAnimePrimaryImageUrl(a), query))
            .ToList();
        return results;
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
            : BuildBrowserRowsForPath(
                new ThemeOutputTarget(Guid.Empty, Guid.Empty, Path.GetTempPath(), ThemeOutputScope.MovieRoot, false),
                anime,
                config,
                null);
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
        _ = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
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

        var mapping = new SeasonThemeMapping
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
        };
        _seasonFinderStore.ApplySeasonThemeMappingChanges(
            [new SeasonThemeMappingChange(BuildSeasonThemeMappingTarget(series, season), mapping, request.Locked ? "Manual" : "Auto")]);
        var result = BuildSeasonMappingRow(series, season);
        _seasonFinderStore.UpsertRow(BuildSeasonFinderRecord(series, season, ResolveLibraryId(series)));
        RefreshBrowserCacheForItem(series);
        return await Task.FromResult(result).ConfigureAwait(false);
    }

    public async Task<SeasonThemeMappingRow> DeleteSeasonThemeMappingAsync(Guid seasonItemId, CancellationToken cancellationToken)
    {
        _ = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
        var season = _libraryManager.GetItemById(seasonItemId) as Season
            ?? throw new KeyNotFoundException("The requested season was not found.");
        var series = FindSeriesForSeason(season)
            ?? throw new InvalidOperationException("The parent series for the requested season was not found.");

        _seasonFinderStore.ApplySeasonThemeMappingChanges(
            [new SeasonThemeMappingChange(BuildSeasonThemeMappingTarget(series, season), null, "Delete")]);
        var result = BuildSeasonMappingRow(series, season);
        _seasonFinderStore.UpsertRow(BuildSeasonFinderRecord(series, season, ResolveLibraryId(series)));
        RefreshBrowserCacheForItem(series);
        return await Task.FromResult(result).ConfigureAwait(false);
    }

    public async Task<SeasonThemeMappingImportResult> ImportSeasonThemeMappingsAsync(
        ImportSeasonThemeMappingsRequest request,
        CancellationToken cancellationToken)
    {
        _ = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
        var rows = request?.Mappings ?? [];
        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        var mappingChanges = new List<SeasonThemeMappingChange>();
        var changedSeasons = new List<(Series Series, Season Season)>();
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.SeasonItemId == Guid.Empty)
            {
                skipped++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.AnimeThemesSlug) && !row.AniListId.HasValue && !row.MyAnimeListId.HasValue)
            {
                skipped++;
                continue;
            }

            var season = _libraryManager.GetItemById(row.SeasonItemId) as Season;
            if (season == null)
            {
                errors.Add($"Season not found: {row.SeasonItemId:D}");
                continue;
            }

            var series = FindSeriesForSeason(season);
            if (series == null)
            {
                errors.Add($"Parent series not found for season: {row.SeasonItemId:D}");
                continue;
            }

            var mapping = new SeasonThemeMapping
            {
                Enabled = true,
                SeriesItemId = series.Id.ToString("D"),
                SeriesPath = series.Path,
                SeasonItemId = season.Id.ToString("D"),
                SeasonPath = season.Path,
                SeasonNumber = season.IndexNumber,
                AnimeThemesSlug = string.IsNullOrWhiteSpace(row.AnimeThemesSlug) ? null : row.AnimeThemesSlug.Trim(),
                AniListId = row.AniListId,
                MyAnimeListId = row.MyAnimeListId,
                Locked = row.Locked ?? true,
            };
            mappingChanges.Add(new SeasonThemeMappingChange(BuildSeasonThemeMappingTarget(series, season), mapping, "Import"));
            changedSeasons.Add((series, season));
            imported++;
        }

        _seasonFinderStore.ApplySeasonThemeMappingChanges(mappingChanges);
        foreach (var changed in changedSeasons)
        {
            _seasonFinderStore.UpsertRow(BuildSeasonFinderRecord(changed.Series, changed.Season, ResolveLibraryId(changed.Series)));
        }

        foreach (var series in changedSeasons.Select(changed => changed.Series).DistinctBy(series => series.Id))
        {
            RefreshBrowserCacheForItem(series);
        }
        return await Task.FromResult(new SeasonThemeMappingImportResult(imported, skipped, errors)).ConfigureAwait(false);
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
        var roots = new Dictionary<string, List<AnimeThemesTheme>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in GetEnabledLibraryItems())
        {
            var anime = ResolveAnime(item, CancellationToken.None).GetAwaiter().GetResult();
            AddDeleteRoot(ResolveThemeOutputTarget(item), anime?.AnimeThemes);

            if (item is Series series && IsSeasonThemeDownloadsEnabled())
            {
                foreach (var season in GetSeasonItems(series))
                {
                    if (!IsSeasonEligibleForThemeMatching(season))
                    {
                        continue;
                    }

                    var seasonAnime = ResolveAnime(season, CancellationToken.None, logMissingIds: false).GetAwaiter().GetResult();
                    AddDeleteRoot(ResolveThemeOutputTarget(season, series), seasonAnime?.AnimeThemes);
                }
            }
        }

        foreach (var root in roots)
        {
            DeleteThemeFilesForPath(root.Key, root.Value, normalizedScope, ref filesDeleted, ref bytesDeleted);
        }

        _logger.LogInformation("Deleted AnimeThemes local files. Scope={Scope}, Files={Files}, Bytes={Bytes}", normalizedScope, filesDeleted, bytesDeleted);
        _ = StartBrowserCacheRebuild();
        return new ThemeDeleteResult(filesDeleted, bytesDeleted);

        void AddDeleteRoot(ThemeOutputTarget? target, List<AnimeThemesTheme>? themes)
        {
            if (target == null)
            {
                return;
            }

            if (!roots.TryGetValue(target.OutputRootPath, out var rootThemes))
            {
                rootThemes = [];
                roots[target.OutputRootPath] = rootThemes;
            }

            if (themes != null)
            {
                rootThemes.AddRange(themes);
            }
        }
    }

    public async Task<ThemeDeleteResult> DeleteIndividualThemeFileAsync(
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

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new FileNotFoundException("The requested local theme media was not found.");
        }

        var outputTarget = ResolveThemeOutputTarget(item)
            ?? throw new InvalidOperationException("The theme output root could not be resolved for this item.");
        ValidateLocalMediaPath(outputTarget.OutputRootPath, path);

        var fileExists = _fileSystem.FileExists(path);
        if (!fileExists)
        {
            throw new FileNotFoundException("The requested local theme media file does not exist on disk.");
        }

        var bytesDeleted = new FileInfo(path).Length;
        await DeleteFileWithRetryAsync(path, cancellationToken).ConfigureAwait(false);
        RefreshBrowserCacheForItem(item);

        _logger.LogInformation("Deleted specific local theme file for {ItemName} ({ItemId}, RowId={RowId}, Target={Target}). File={Path}, Bytes={Bytes}", item.Name, itemId, rowId, target, path, bytesDeleted);
        return new ThemeDeleteResult(1, bytesDeleted);
    }

    private async Task DeleteFileWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        await FileDeleteRetryService.DeleteAsync(
            () => _fileSystem.DeleteFile(path),
            Path.GetFileName(path),
            "Emby",
            (ex, retryDelay, attempt, maxAttempts) =>
                _logger.LogWarning(
                    ex,
                    "Theme file is temporarily locked. Retrying delete in {Delay} ms ({Attempt}/{MaxAttempts}): {Path}",
                    retryDelay,
                    attempt,
                    maxAttempts,
                    path),
            cancellationToken).ConfigureAwait(false);
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
        return await DownloadThemeByRowIdAsync(itemId, rowId, forceRedownload, null, null, null, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ThemeDownloadExecutionResult> DownloadThemeByRowIdAsync(
        Guid itemId,
        string rowId,
        bool forceRedownload,
        IProgress<double>? progress,
        bool? includeAudio,
        bool? includeVideo,
        bool? includeExtras,
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
        EnsureSeasonThemeDownloadsAllowed(item, config);
        _logger.LogInformation("Starting Anime Themes theme-row download for {ItemName} ({ItemId}, RowId={RowId})...", item.Name, itemId, rowId);
        progress?.Report(5);
        var selection = await BuildSingleThemeSelectionAsync(item, rowId, cancellationToken).ConfigureAwait(false);
        var outputTarget = ResolveThemeOutputTarget(item)
            ?? throw new InvalidOperationException("The theme output root could not be resolved for this item.");
        var fileNamePrefix = item is Season && outputTarget.IsRedirected && !selection.SameAsSeries
            ? "Season 01 -"
            : null;
        progress?.Report(20);
        var audioConfig = CreateThemeConfig(item, config, isVideo: false);
        var videoConfig = CreateThemeConfig(item, config, isVideo: true);
        var selectedAudio = includeAudio ?? true;
        var selectedVideo = includeVideo ?? true;
        var selectedExtras = includeExtras ?? config.ExtrasEnabled;
        if (!selectedAudio && !selectedVideo && !selectedExtras)
        {
            throw new InvalidOperationException("At least one theme output must be selected.");
        }

        var plan = ThemeFilePlanner.BuildSingleCandidatePlan(
            selection.Anime,
            selection.Candidate,
            selection.Order,
            outputTarget.OutputRootPath,
            includeAudio: selectedAudio,
            includeVideo: selectedVideo,
            includeExtras: selectedExtras,
            extrasFileNameFormat: config.ExtrasFileNameFormat,
            extrasFileSuffix: config.ExtrasFileSuffix,
            fileNamePrefix: fileNamePrefix,
            outputTarget: outputTarget);

        MigrateExtraFiles(plan.ExtraFiles, forceRedownload || config.ForceRedownload);
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
            var transferProgress = CreateStepProgress(progress, 20, 75, finishedSteps, totalSteps);
            await DownloadFile(file.Url, file.Path, file.IsVideo ? videoConfig.Volume : audioConfig.Volume, file.IsVideo, file.RequiresTranscoding, cancellationToken, transferProgress).ConfigureAwait(false);
            _dataStore.UpsertThemeFile(file.OutputTarget ?? outputTarget, file.ThemeKey, file.IsVideo ? "video" : "audio", file.Path);
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
                ThemeExtraFileResult result;
                if (!string.IsNullOrWhiteSpace(extra.SourcePath))
                {
                    result = ThemeExtrasFileService.EnsureExtraFileDetailed(
                        extra.SourcePath,
                        extra.TargetPath,
                        config.ExtrasLinkMode,
                        forceRedownload || config.ForceRedownload);
                }
                else if (!string.IsNullOrWhiteSpace(extra.DownloadUrl))
                {
                    var transferProgress = CreateStepProgress(progress, 20, 75, finishedSteps, totalSteps);
                    await DownloadFile(extra.DownloadUrl, extra.TargetPath, videoConfig.Volume, isVideo: true, requiresTranscoding: extra.RequiresTranscoding, cancellationToken, transferProgress).ConfigureAwait(false);
                    result = new ThemeExtraFileResult("downloaded");
                }
                else
                {
                    result = new ThemeExtraFileResult("missing-source");
                }

                if (string.Equals(result.Action, "missing-source", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FileNotFoundException("The source theme video for the extra was not found.", extra.SourcePath);
                }

                if (string.Equals(result.Action, "skipped", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ThemeExtrasManifestService.UpdateExtraFile(extra);
                _dataStore.UpsertThemeFile(extra.OutputTarget ?? outputTarget, extra.Key, "extra", extra.TargetPath);
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
        RefreshBrowserCacheForItem(item is Season seasonItem ? FindSeriesForSeason(seasonItem) ?? item : item);
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

        var outputTarget = ResolveThemeOutputTarget(item)
            ?? throw new InvalidOperationException("The theme output root could not be resolved for this item.");
        ValidateLocalMediaPath(outputTarget.OutputRootPath, path);
        var contentType = ThemeFilePlanner.GetMediaContentType(path);
        return new ThemeLocalMediaResult(path, contentType, Path.GetFileName(path));
    }

    private static string? BuildImageUrl(BaseItem item, ImageType imageType, string imagePath)
    {
        return item.HasImage(imageType, 0)
            ? string.Format(CultureInfo.InvariantCulture, "Items/{0}/Images/{1}", item.Id, imagePath)
            : null;
    }

    private static string? GetImageTag(BaseItem item, ImageType imageType)
    {
        return item.HasImage(imageType, 0) ? StringComparer.Ordinal.GetHashCode(item.GetImageInfo(imageType, 0).Path ?? string.Empty).ToString(CultureInfo.InvariantCulture) : null;
    }

    private Guid? ResolveLibraryId(BaseItem item)
    {
        var folder = _libraryManager.GetCollectionFolders(item).FirstOrDefault();
        return folder?.Id;
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
        return ThemeFilePlanner.IsSupportedMediaExtension(Path.GetExtension(path));
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

    private async Task<(AnimeThemesAnime Anime, ScoredCandidate Candidate, int Order, bool SameAsSeries)> BuildSingleThemeSelectionAsync(
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
                return (anime, candidates[i], i + 1, resolution.SameAsSeries);
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

        if (!ThemeFilePlanner.IsSupportedMediaExtension(Path.GetExtension(fullPath)))
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

        if (item is Season && !config.SeasonThemeDownloadsEnabled)
        {
            return new ThemeBrowserItemResult(
                item.Id,
                item.Name ?? "Unknown",
                "Season",
                null,
                null,
                [],
                [
                BuildBrowserThemeGroup(
                    item,
                    "Season",
                    item.IndexNumber,
                    new BrowserAnimeResolution(null, "Disabled", "SeasonThemeDownloadsDisabled", false),
                    [],
                    "Season theme downloads are disabled in plugin configuration.",
                    null,
                    null)
                ]);
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
        var seasons = GetSeasonItems(series)
            .Where(IsSeasonEligibleForThemeMatching)
            .ToList();
        var seasonsWithPath = seasons.Where(s => ResolveThemeOutputTarget(s, series) != null).ToList();
        if (!config.SeasonThemeDownloadsEnabled || seasonsWithPath.Count == 0)
        {
            groups.Add(BuildBrowserThemeGroup(
                series,
                "Series",
                null,
                new BrowserAnimeResolution(seriesAnime, "Series", "SeriesLevel", false),
                seriesRows,
                null,
                null,
                series.Id));
            return groups;
        }

        var representativeSeason = seasonsWithPath.FirstOrDefault(IsSeriesRootSeason);
        if (representativeSeason == null)
        {
            groups.Add(BuildBrowserThemeGroup(
                series,
                "Series",
                null,
                new BrowserAnimeResolution(seriesAnime, "Series", "SeriesLevel", false),
                seriesRows,
                null,
                null,
                series.Id));
        }

        var automaticSeasonAnime = seriesAnime == null
            ? new Dictionary<Guid, AnimeThemesAnime>()
            : await BuildAutomaticSeasonAnimeMapAsync(series, seasons, seriesAnime, cancellationToken).ConfigureAwait(false);

        foreach (var season in seasonsWithPath)
        {
            var resolution = await ResolveSeasonBrowserAnimeAsync(series, season, seriesAnime, automaticSeasonAnime, cancellationToken).ConfigureAwait(false);
            var rows = resolution.SameAsSeries ? seriesRows : BuildBrowserRowsForResolution(season, resolution, config);
            groups.Add(BuildBrowserThemeGroup(
                season,
                "Season",
                season.IndexNumber,
                resolution,
                rows,
                rows.Count == 0 && resolution.SameAsSeries ? "Uses series-level themes, but no series-level themes are available." : null,
                null,
                series.Id));
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
        Guid? actionItemId,
        Guid? seriesItemId)
    {
        return new ThemeBrowserThemeGroup(
            actionItemId ?? item.Id,
            seriesItemId ?? (item is Series ? item.Id : null),
            item is Season ? item.Id : null,
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
        if (resolution.Anime?.AnimeThemes == null)
        {
            return new List<ThemeBrowserThemeRow>();
        }

        return BuildBrowserRows(item, resolution.Anime, config, resolution.SameAsSeries);
    }

    private async Task<BrowserAnimeResolution> ResolveBrowserAnimeForItemAsync(
        BaseItem item,
        CancellationToken cancellationToken)
    {
        if (item is Season season)
        {
            var series = FindSeriesForSeason(season);
            if (series == null)
            {
                return new BrowserAnimeResolution(null, "Unmatched", "NoSeries", false);
            }

            var seriesAnime = await ResolveAnime(series, cancellationToken, logMissingIds: false).ConfigureAwait(false);
            var automaticSeasonAnime = seriesAnime == null
                ? new Dictionary<Guid, AnimeThemesAnime>()
                : await BuildAutomaticSeasonAnimeMapAsync(series, GetSeasonItems(series), seriesAnime, cancellationToken).ConfigureAwait(false);
            return await ResolveSeasonBrowserAnimeAsync(series, season, seriesAnime, automaticSeasonAnime, cancellationToken).ConfigureAwait(false);
        }

        var anime = await ResolveAnime(item, cancellationToken).ConfigureAwait(false);
        return new BrowserAnimeResolution(
            anime,
            item is Series ? "Series" : "Direct",
            item is Series ? "SeriesLevel" : "ItemProviderIds",
            false);
    }

    private async Task<BrowserAnimeResolution> ResolveSeasonBrowserAnimeAsync(
        Series series,
        Season season,
        AnimeThemesAnime? seriesAnime,
        Dictionary<Guid, AnimeThemesAnime> automaticSeasonAnime,
        CancellationToken cancellationToken)
    {
        automaticSeasonAnime.TryGetValue(season.Id, out var automaticAnime);
        var state = BuildSeasonThemeMatchState(series, season, automaticAnime);
        AnimeThemesAnime? anime = null;

        if (state.Status == "Series")
        {
            anime = seriesAnime;
        }
        else if (automaticAnime != null && state.Status == "Auto" && state.Source == "AniListRelations")
        {
            anime = automaticAnime;
        }
        else if (state.HasAnimeIdentity)
        {
            anime = await ResolveAnimeByIdentityAsync(
                state.AnimeThemesSlug,
                state.AniListId,
                state.MyAnimeListId,
                cancellationToken).ConfigureAwait(false);
        }

        var sameAsSeries = state.SameAsSeries ||
            (seriesAnime != null && anime != null && IsSameAnime(seriesAnime, anime));
        if (anime == null && state.Status == "Series")
        {
            anime = seriesAnime;
            sameAsSeries = true;
        }

        return new BrowserAnimeResolution(anime, state.Status, state.Source, sameAsSeries);
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

        var allDownloads = new List<(ThemeFilePlan File, int Volume, string ItemName, ThemeOutputTarget OutputTarget)>();
        var allExtras = new List<(ThemeExtraPlan Extra, string ItemName, ThemeOutputTarget OutputTarget, int VideoVolume)>();
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
                MigrateExtraFiles(result.ExtraFiles, forceRedownload || config.ForceRedownload);
                if (config.AllowAdd)
                {
                    foreach (var file in result.MediaFiles)
                    {
                        var outputTarget = file.OutputTarget ?? ResolveThemeOutputTarget(item);
                        var volume = file.IsVideo ? videoConfig.Volume : audioConfig.Volume;
                        if (outputTarget != null && (forceRedownload || !_fileSystem.FileExists(file.Path)))
                        {
                            allDownloads.Add((file, volume, itemName, outputTarget));
                        }
                    }

                    foreach (var extra in result.ExtraFiles)
                    {
                        var outputTarget = extra.OutputTarget ?? ResolveThemeOutputTarget(item);
                        if (outputTarget != null && (forceRedownload || config.ForceRedownload || !_fileSystem.FileExists(extra.TargetPath)))
                        {
                            allExtras.Add((extra, itemName, outputTarget, videoConfig.Volume));
                        }
                    }
                }

                if (config.AllowDelete && result.Themes != null)
                {
                    cleanupTasks.AddRange(result.CleanupPlans.Select(c => (c.Directory, c.DesiredFiles, c.Themes)));
                }
            }

            progress?.Report((double)(i + 1) / items.Count * 40); // Phase 1 = 0-40%
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
            var downloadTasks = new List<Task>();
            var downloadFractions = new double[allDownloads.Count];
            var downloadProgressLock = new object();
            var downloadIndex = 0;

            foreach (var dl in allDownloads)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
                var currentDownloadIndex = downloadIndex++;

                downloadTasks.Add(DownloadOneAsync());

                async Task DownloadOneAsync()
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
                                var transferProgress = progress == null
                                    ? null
                                    : new InlineProgress(fraction =>
                                    {
                                        lock (downloadProgressLock)
                                        {
                                            downloadFractions[currentDownloadIndex] = Math.Max(downloadFractions[currentDownloadIndex], fraction);
                                            progress.Report(40 + (downloadFractions.Sum() / allDownloads.Count * 45));
                                        }
                                    });
                                await DownloadFile(dl.File.Url, dl.File.Path, dl.Volume, dl.File.IsVideo, dl.File.RequiresTranscoding, cancellationToken, transferProgress).ConfigureAwait(false);
                                _dataStore.UpsertThemeFile(dl.OutputTarget, dl.File.ThemeKey, dl.File.IsVideo ? "video" : "audio", dl.File.Path);
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
                        lock (downloadProgressLock)
                        {
                            downloadFractions[currentDownloadIndex] = 1;
                            progress?.Report(40 + (downloadFractions.Sum() / allDownloads.Count * 45));
                        }
                    }
                }
            }

            await Task.WhenAll(downloadTasks).ConfigureAwait(false);
        }

        progress?.Report(85);

        // ── Extras ──
        var completedExtras = 0;
        var failedExtras = 0;
        for (var extraIndex = 0; extraIndex < allExtras.Count; extraIndex++)
        {
            var extra = allExtras[extraIndex];
            try
            {
                ThemeExtraFileResult result;
                if (!string.IsNullOrWhiteSpace(extra.Extra.SourcePath))
                {
                    result = ThemeExtrasFileService.EnsureExtraFileDetailed(
                        extra.Extra.SourcePath,
                        extra.Extra.TargetPath,
                        config.ExtrasLinkMode,
                        config.ForceRedownload);
                }
                else if (!string.IsNullOrWhiteSpace(extra.Extra.DownloadUrl))
                {
                    var transferProgress = CreateStepProgress(progress, 85, 10, extraIndex, Math.Max(1, allExtras.Count));
                    await DownloadFile(extra.Extra.DownloadUrl, extra.Extra.TargetPath, extra.VideoVolume, isVideo: true, requiresTranscoding: extra.Extra.RequiresTranscoding, cancellationToken, transferProgress).ConfigureAwait(false);
                    result = new ThemeExtraFileResult("downloaded");
                }
                else
                {
                    result = new ThemeExtraFileResult("missing-source");
                }

                if (string.Equals(result.Action, "missing-source", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FileNotFoundException("The source theme video for the extra was not found.", extra.Extra.SourcePath);
                }

                ThemeExtrasManifestService.UpdateExtraFile(extra.Extra);
                _dataStore.UpsertThemeFile(extra.OutputTarget, extra.Extra.Key, "extra", extra.Extra.TargetPath);
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

            progress?.Report(85 + ((double)(extraIndex + 1) / Math.Max(1, allExtras.Count) * 10));
        }

        // ── Cleanup ──
        foreach (var cleanup in cleanupTasks)
        {
            CleanupDirectory(cleanup.Directory, cleanup.DesiredFiles, cleanup.Themes);
        }

        foreach (var item in items)
        {
            RefreshBrowserCacheForItem(item);
        }

        progress?.Report(100);

        return new ThemeDownloadExecutionResult(items.Count, allDownloads.Count, completedDownloads, allExtras.Count, completedExtras, failedExtras);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromDays(7).Ticks
            }
        };
    }

    /// <summary>
    /// Gets all library items from enabled libraries.
    /// </summary>
    private List<BaseItem> GetEnabledLibraryItems()
    {
        return GetEnabledLibraryItemsWithLibraries()
            .Select(i => i.Item)
            .ToList();
    }

    private List<(BaseItem Item, Guid LibraryId, string? LibraryName)> GetEnabledLibraryItemsWithLibraries()
    {
        var root = _libraryManager.RootFolder;
        var enabledFolders = new Dictionary<Guid, string?>();

        foreach (var child in root.GetChildren(new InternalItemsQuery()))
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
                    enabledFolders[folder.Id] = folder.Name;
                }
                else
                {
                    _logger.LogWarning("AnimeThemesSync is NOT enabled for library: {LibraryName}.", folder.Name);
                }
            }
        }

        var items = new List<(BaseItem Item, Guid LibraryId, string? LibraryName)>();
        foreach (var enabledFolder in enabledFolders)
        {
            var folder = _libraryManager.GetItemById(enabledFolder.Key) as Folder;
            if (folder != null)
            {
                var folderItems = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { "Series", "Movie" },
                    Recursive = true,
                    Parent = folder
                });
                items.AddRange(folderItems.Select(item => (item, enabledFolder.Key, enabledFolder.Value)));
            }
        }

        return items;
    }

    /// <summary>
    /// Creates a ThemeConfig for the given item based on the plugin configuration.
    /// </summary>
    private static ThemeConfig CreateThemeConfig(BaseItem item, PluginConfiguration config, bool isVideo)
    {
        config.Normalize();
        var mediaConfig = item is Series or Season ? config.Series : config.Movie;
        var themeConfig = isVideo ? mediaConfig.Video : mediaConfig.Audio;
        return new ThemeConfig
        {
            UseAsTheme = themeConfig.UseAsTheme,
            MaxThemes = themeConfig.MaxThemes,
            Volume = themeConfig.Volume,
            IgnoreOp = themeConfig.IgnoreOp,
            IgnoreEd = themeConfig.IgnoreEd,
            IgnoreOverlaps = themeConfig.IgnoreOverlaps,
            IgnoreCredits = themeConfig.IgnoreCredits,
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
        var sameAsSeries = false;
        if (item is Season && config?.SeasonThemeDownloadsEnabled == false)
        {
            _logger.LogInformation("  Season theme downloads are disabled. Skipping {ItemName}.", item.Name);
            return null;
        }

        if (item is Season seasonItem)
        {
            var browserResolution = await ResolveBrowserAnimeForItemAsync(seasonItem, cancellationToken).ConfigureAwait(false);
            anime = browserResolution.Anime ?? anime;
            sameAsSeries = browserResolution.SameAsSeries;
        }

        var plans = new List<ThemeOutputPlan>();
        if (anime?.AnimeThemes != null)
        {
            var outputTarget = ResolveThemeOutputTarget(item);
            if (outputTarget == null)
            {
                return null;
            }

            var fileNamePrefix = item is Season && outputTarget.IsRedirected && !sameAsSeries ? "Season 01 -" : null;
            plans.Add(ThemeFilePlanner.BuildPlan(
                anime,
                outputTarget.OutputRootPath,
                audioConfig,
                videoConfig,
                config?.ExtrasEnabled ?? false,
                config?.ExtrasFileNameFormat,
                config?.ExtrasFileSuffix ?? ExtrasFileSuffix.Other,
                fileNamePrefix,
                outputTarget));
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

        if (!IsSeasonThemeDownloadsEnabled(config))
        {
            return plans.Count == 0 ? null : ThemeFilePlanner.MergePlans(plans);
        }

        var seasons = GetSeasonItems(series);
        var automaticSeasonAnime = anime == null
            ? new Dictionary<Guid, AnimeThemesAnime>()
            : await BuildAutomaticSeasonAnimeMapAsync(series, seasons, anime, cancellationToken).ConfigureAwait(false);

        foreach (var season in seasons)
        {
            if (!IsSeasonEligibleForThemeMatching(season))
            {
                continue;
            }

            var seasonResolution = await ResolveSeasonBrowserAnimeAsync(series, season, anime, automaticSeasonAnime, cancellationToken).ConfigureAwait(false);
            var seasonAnime = seasonResolution.Anime;

            if (seasonAnime?.AnimeThemes == null)
            {
                continue;
            }

            if (seasonResolution.SameAsSeries)
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
            var outputTarget = ResolveThemeOutputTarget(season, series);
            if (outputTarget == null)
            {
                continue;
            }

            var fileNamePrefix = outputTarget.IsRedirected ? "Season 01 -" : null;
            plans.Add(ThemeFilePlanner.BuildPlan(
                seasonAnime,
                outputTarget.OutputRootPath,
                audioConfig,
                videoConfig,
                config?.ExtrasEnabled ?? false,
                config?.ExtrasFileNameFormat,
                config?.ExtrasFileSuffix ?? ExtrasFileSuffix.Other,
                fileNamePrefix,
                outputTarget));
        }

        if (plans.Count == 0)
        {
            return null;
        }

        return ThemeFilePlanner.MergePlans(plans);
    }

    private static bool IsSeriesRootSeason(Season season)
    {
        return !season.IndexNumber.HasValue || season.IndexNumber.Value == 1;
    }

    private ThemeOutputTarget? ResolveThemeOutputTarget(BaseItem item, Series? knownSeries = null)
    {
        if (string.IsNullOrWhiteSpace(item.Path) && item is not Season)
        {
            _logger.LogWarning("Theme output was skipped for {ItemName}; the item path is empty.", item.Name);
            return null;
        }

        if (item is Season season)
        {
            if (season.IndexNumber == 0 || !IsSeasonEligibleForThemeMatching(season))
            {
                _logger.LogInformation("Theme output was skipped for ineligible season {SeasonName}.", season.Name);
                return null;
            }

            if (IsSeriesRootSeason(season))
            {
                var series = knownSeries ?? FindSeriesForSeason(season);
                if (series == null || string.IsNullOrWhiteSpace(series.Path))
                {
                    _logger.LogWarning(
                        "Theme output was skipped for {SeasonName}; its parent Series output root could not be resolved.",
                        season.Name);
                    return null;
                }

                return new ThemeOutputTarget(season.Id, series.Id, series.Path, ThemeOutputScope.SeriesRoot, true);
            }

            if (string.IsNullOrWhiteSpace(season.Path))
            {
                _logger.LogWarning("Theme output was skipped for {SeasonName}; the season path is empty.", season.Name);
                return null;
            }

            return new ThemeOutputTarget(season.Id, season.Id, season.Path, ThemeOutputScope.SeasonRoot, false);
        }

        return item is Movie
            ? new ThemeOutputTarget(item.Id, item.Id, item.Path, ThemeOutputScope.MovieRoot, false)
            : new ThemeOutputTarget(item.Id, item.Id, item.Path, ThemeOutputScope.SeriesRoot, false);
    }

    private SeasonThemeMappingRow BuildSeasonMappingRow(Series series, Season season)
    {
        var state = BuildSeasonThemeMatchState(series, season, null);

        var animeThemesUrl = !string.IsNullOrWhiteSpace(state.AnimeThemesSlug)
            ? Constants.AnimeThemesWebUrl + "/anime/" + state.AnimeThemesSlug
            : null;

        return new SeasonThemeMappingRow(
            series.Id,
            series.Name ?? "Unknown",
            series.Path,
            season.Id,
            season.Name ?? "Season",
            season.Path,
            season.IndexNumber,
            state.Status,
            state.Source,
            state.SameAsSeries,
            state.AnimeName,
            null,
            state.AnimeThemesSlug,
            animeThemesUrl,
            state.AniListId,
            state.MyAnimeListId,
            BuildImageUrl(season, ImageType.Primary, "Primary") ?? BuildImageUrl(series, ImageType.Primary, "Primary"));
    }

    private SeasonFinderRowRecord BuildSeasonFinderRecord(Series series, Season season, Guid? libraryId)
    {
        var target = ResolveThemeOutputTarget(season, series);
        return new SeasonFinderRowRecord
        {
            LibraryId = libraryId?.ToString("D"),
            Row = BuildSeasonMappingRow(series, season),
            OutputRootItemId = target?.OutputRootItemId.ToString("D"),
            OutputRootPath = target?.OutputRootPath,
            OutputScope = target?.Scope.ToString(),
        };
    }

    private SeasonThemeMatchState BuildSeasonThemeMatchState(
        Series series,
        Season season,
        AnimeThemesAnime? automaticAnime)
    {
        var mapping = FindSeasonThemeMapping(series, season);
        var seasonIds = ExtractItemProviderIds(season);
        var seriesIds = ExtractItemProviderIds(series);
        var seasonSlug = GetItemAnimeThemesSlug(season);
        var seriesSlug = GetItemAnimeThemesSlug(series);

        if (mapping != null)
        {
            var status = mapping.Locked ? "Manual" : "Auto";
            var source = mapping.Locked ? "SeasonThemeMappings" : "AniListRelations";
            var animeThemesSlug = mapping.AnimeThemesSlug ?? seasonSlug;
            return new SeasonThemeMatchState(
                status,
                source,
                false,
                animeThemesSlug ?? season.Name,
                animeThemesSlug,
                mapping.AniListId ?? seasonIds.AniListId,
                mapping.MyAnimeListId ?? seasonIds.MyAnimeListId);
        }

        if (HasProviderIdentity(season))
        {
            return new SeasonThemeMatchState(
                "Direct",
                "SeasonProviderIds",
                false,
                season.Name,
                seasonSlug,
                seasonIds.AniListId,
                seasonIds.MyAnimeListId);
        }

        if (automaticAnime != null)
        {
            var automaticIds = ExtractAnimeExternalIds(automaticAnime);
            return new SeasonThemeMatchState(
                "Auto",
                "AniListRelations",
                false,
                automaticAnime.Name ?? automaticAnime.Slug ?? season.Name,
                automaticAnime.Slug,
                automaticIds.AniListId,
                automaticIds.MyAnimeListId);
        }

        if (season.IndexNumber == 1 && HasProviderIdentity(series))
        {
            return new SeasonThemeMatchState(
                "Series",
                "SeriesLevel",
                true,
                series.Name,
                seriesSlug,
                seriesIds.AniListId,
                seriesIds.MyAnimeListId);
        }

        return new SeasonThemeMatchState("Unmatched", "None", false, null, null, null, null);
    }

    private async Task<Dictionary<Guid, AnimeThemesAnime>> BuildAutomaticSeasonAnimeMapAsync(
        Series series,
        List<Season> seasons,
        AnimeThemesAnime seriesAnime,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<Guid, AnimeThemesAnime>();
        var numberedSeasons = seasons
            .Where(s => IsSeasonEligibleForThemeMatching(s) && s.IndexNumber.HasValue && s.IndexNumber.Value > 1)
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
            SaveAutomaticSeasonThemeMapping(series, season, resolved[candidateIndex].Anime);
            _logger.LogInformation(
                "  Auto-mapped {SeriesName} / {SeasonName} to AnimeThemes anime {AnimeName} via AniList relations.",
                series.Name,
                season.Name,
                resolved[candidateIndex].Anime.Name ?? resolved[candidateIndex].Anime.Slug ?? "Unknown");
        }

        return map;
    }

    private void SaveAutomaticSeasonThemeMapping(Series series, Season season, AnimeThemesAnime anime)
    {
        if (!IsSeasonEligibleForThemeMatching(season))
        {
            return;
        }

        if (Plugin.Instance?.Configuration == null)
        {
            return;
        }

        var mappings = _seasonFinderStore.GetSeasonThemeMappings();
        var existing = FindSeasonThemeMapping(mappings, series, season);
        if (existing?.Locked == true)
        {
            return;
        }

        var ids = ExtractAnimeExternalIds(anime);
        var animeThemesSlug = !string.IsNullOrWhiteSpace(anime.Slug) ? anime.Slug.Trim() : existing?.AnimeThemesSlug;
        var aniListId = ids.AniListId ?? existing?.AniListId;
        var myAnimeListId = ids.MyAnimeListId ?? existing?.MyAnimeListId;
        if (string.IsNullOrWhiteSpace(animeThemesSlug) && !aniListId.HasValue && !myAnimeListId.HasValue)
        {
            return;
        }

        if (existing != null &&
            !existing.Locked &&
            string.Equals(existing.AnimeThemesSlug, animeThemesSlug, StringComparison.OrdinalIgnoreCase) &&
            existing.AniListId == aniListId &&
            existing.MyAnimeListId == myAnimeListId)
        {
            return;
        }

        var mapping = new SeasonThemeMapping
        {
            Enabled = true,
            SeriesItemId = series.Id.ToString("D"),
            SeriesPath = series.Path,
            SeasonItemId = season.Id.ToString("D"),
            SeasonPath = season.Path,
            SeasonNumber = season.IndexNumber,
            AnimeThemesSlug = animeThemesSlug,
            AniListId = aniListId,
            MyAnimeListId = myAnimeListId,
            Locked = false,
        };
        _seasonFinderStore.ApplySeasonThemeMappingChanges(
            [new SeasonThemeMappingChange(BuildSeasonThemeMappingTarget(series, season), mapping, "Auto")]);
        _seasonFinderStore.UpsertRow(BuildSeasonFinderRecord(series, season, ResolveLibraryId(series)));
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

    private static SeasonThemeMappingTarget BuildSeasonThemeMappingTarget(Series series, Season season)
    {
        return new SeasonThemeMappingTarget(
            series.Id.ToString("D"),
            series.Path,
            season.Id.ToString("D"),
            season.Path,
            Path.GetDirectoryName(season.Path),
            season.IndexNumber);
    }

    private async Task<AnimeThemesAnime?> ResolveAnime(
        BaseItem item,
        CancellationToken cancellationToken,
        bool logMissingIds = true)
    {
        var mapping = item is Season season ? FindSeasonThemeMapping(FindSeriesForSeason(season), season) : null;
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

        return await ResolveAnimeByIdentityAsync(animeThemesSlug, aniListId, malId, cancellationToken, item.Name, logMissingIds).ConfigureAwait(false);
    }

    private async Task<AnimeThemesAnime?> ResolveAnimeByIdentityAsync(
        string? animeThemesSlug,
        int? aniListId,
        int? malId,
        CancellationToken cancellationToken,
        string? itemName = null,
        bool logMissingIds = false)
    {
        if (aniListId == null && malId == null && string.IsNullOrWhiteSpace(animeThemesSlug))
        {
            if (logMissingIds)
            {
                _logger.LogWarning("  No AnimeThemes, AniList, or MAL ID found for {ItemName}. Skipping.", itemName ?? "item");
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

    private static bool IsSeasonEligibleForThemeMatching(Season season)
    {
        if (season.IndexNumber == 0)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(season.Name) ||
               season.Name.IndexOf("special", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private SeasonThemeMapping? FindSeasonThemeMapping(BaseItem season)
    {
        return FindSeasonThemeMapping(season is Season typedSeason ? FindSeriesForSeason(typedSeason) : null, season);
    }

    private SeasonThemeMapping? FindSeasonThemeMapping(Series? series, BaseItem season)
    {
        var mappings = _seasonFinderStore.GetSeasonThemeMappings();
        if (mappings.Count == 0)
        {
            return null;
        }

        return FindSeasonThemeMapping(mappings, series, season);
    }

    private static SeasonThemeMapping? FindSeasonThemeMapping(List<SeasonThemeMapping> mappings, Series? series, BaseItem season)
    {
        var seasonItemId = season.Id.ToString("D");
        var compactSeasonItemId = season.Id.ToString("N");
        var seasonPath = NormalizeMappingPath(season.Path);
        var seriesItemId = series?.Id.ToString("D") ?? string.Empty;
        var compactSeriesItemId = series?.Id.ToString("N") ?? string.Empty;
        var seriesPath = NormalizeMappingPath(series?.Path);
        var seasonParentPath = NormalizeMappingPath(Path.GetDirectoryName(season.Path));
        var seasonNumber = season.IndexNumber;

        return mappings
            .Where(mapping => mapping.Enabled && HasThemeIdentity(mapping))
            .Select(mapping => new
            {
                Mapping = mapping,
                Rank = GetSeasonMappingMatchRank(
                    mapping,
                    seasonItemId,
                    compactSeasonItemId,
                    seasonPath,
                    seriesItemId,
                    compactSeriesItemId,
                    seriesPath,
                    seasonParentPath,
                    seasonNumber),
            })
            .Where(candidate => candidate.Rank > 0)
            .OrderByDescending(candidate => candidate.Mapping.Locked)
            .ThenByDescending(candidate => candidate.Rank)
            .Select(candidate => candidate.Mapping)
            .FirstOrDefault();
    }

    private static int GetSeasonMappingMatchRank(
        SeasonThemeMapping mapping,
        string seasonItemId,
        string compactSeasonItemId,
        string seasonPath,
        string seriesItemId,
        string compactSeriesItemId,
        string seriesPath,
        string seasonParentPath,
        int? seasonNumber)
    {
        if (MatchesId(mapping.SeasonItemId, seasonItemId, compactSeasonItemId))
        {
            return 4;
        }

        if (MatchesPath(mapping.SeasonPath, seasonPath))
        {
            return 3;
        }

        if (!mapping.SeasonNumber.HasValue || seasonNumber != mapping.SeasonNumber.Value)
        {
            return 0;
        }

        if (MatchesId(mapping.SeriesItemId, seriesItemId, compactSeriesItemId))
        {
            return 2;
        }

        return MatchesPath(mapping.SeriesPath, seriesPath) || MatchesPath(mapping.SeriesPath, seasonParentPath) ? 1 : 0;
    }

    private static bool IsSeasonThemeDownloadsEnabled()
    {
        return IsSeasonThemeDownloadsEnabled(Plugin.Instance?.Configuration);
    }

    private static bool IsSeasonThemeDownloadsEnabled(PluginConfiguration? config)
    {
        return config?.SeasonThemeDownloadsEnabled != false;
    }

    private static void EnsureSeasonThemeDownloadsAllowed(BaseItem item, PluginConfiguration config)
    {
        if (item is Season && !config.SeasonThemeDownloadsEnabled)
        {
            throw new InvalidOperationException("Season theme downloads are disabled in plugin configuration.");
        }
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
            IncludeItemTypes = new[] { "Season" },
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

    private List<ThemeBrowserThemeRow> BuildBrowserRows(
        BaseItem item,
        AnimeThemesAnime anime,
        PluginConfiguration config,
        bool sameAsSeries = false)
    {
        var outputTarget = ResolveThemeOutputTarget(item);
        if (outputTarget == null)
        {
            return [];
        }

        var fileNamePrefix = item is Season && outputTarget.IsRedirected && !sameAsSeries ? "Season 01 -" : null;
        return BuildBrowserRowsForPath(outputTarget, anime, config, fileNamePrefix);
    }

    private List<ThemeBrowserThemeRow> BuildBrowserRowsForPath(
        ThemeOutputTarget outputTarget,
        AnimeThemesAnime anime,
        PluginConfiguration config,
        string? fileNamePrefix)
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
                    includeExtras: config.ExtrasEnabled,
                    extrasFileNameFormat: config.ExtrasFileNameFormat,
                    extrasFileSuffix: config.ExtrasFileSuffix,
                    fileNamePrefix: fileNamePrefix,
                    outputTarget: outputTarget);
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
    /// Renames existing extras files to the current display-name format when possible.
    /// </summary>
    private void MigrateExtraFiles(IEnumerable<ThemeExtraPlan> extras, bool overwrite)
    {
        foreach (var extra in extras)
        {
            try
            {
                var result = ThemeExtrasManifestService.MigrateExtraFile(extra, overwrite);
                if (string.Equals(result.Action, "renamed", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "Renamed browseable extra to match current naming format: {Filename}",
                        Path.GetFileName(extra.TargetPath));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to migrate browseable extra name: {Path}", extra.TargetPath);
            }
        }
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

        foreach (var file in _fileSystem.GetFilePaths(directory, false))
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
    private async Task DownloadFile(
        string url,
        string path,
        int volume,
        bool isVideo,
        bool requiresTranscoding,
        CancellationToken cancellationToken,
        IProgress<double>? transferProgress = null)
    {
        var config = Plugin.Instance?.Configuration;
        var timeoutSeconds = config?.DownloadTimeoutSeconds > 0 ? config.DownloadTimeoutSeconds : 600;

        var client = _httpClientFactory.CreateClient(Constants.AnimeThemesHttpClientName);
        using var transferCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        transferCancellation.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !_fileSystem.DirectoryExists(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".part";

        try
        {
            using (await _downloadLimiter.AcquireAsync(config?.MaxConcurrentDownloads ?? 1, cancellationToken).ConfigureAwait(false))
            {
                await SegmentedDownloadService.DownloadAsync(
                    client,
                    url,
                    tempPath,
                    config?.SegmentedDownloadEnabled == true,
                    config?.SegmentedDownloadSegments ?? 4,
                    transferProgress,
                    transferCancellation.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && transferCancellation.IsCancellationRequested)
        {
            CleanupTempFile(tempPath);
            throw new TimeoutException($"Download did not complete within {timeoutSeconds} seconds.", ex);
        }
        catch (Exception)
        {
            CleanupTempFile(tempPath);
            throw;
        }
        var needsConversion = requiresTranscoding;
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

    private static InlineProgress? CreateStepProgress(IProgress<double>? progress, double start, double span, int completedSteps, int totalSteps)
    {
        return progress == null
            ? null
            : new InlineProgress(fraction => progress.Report(start + (((completedSteps + fraction) / Math.Max(1, totalSteps)) * span)));
    }

    private sealed class InlineProgress : IProgress<double>
    {
        private readonly Action<double> _report;

        public InlineProgress(Action<double> report)
        {
            _report = report;
        }

        public void Report(double value)
        {
            _report(Math.Max(0, Math.Min(1, value)));
        }
    }

    /// <summary>
    /// Runs ffmpeg to convert format and/or adjust volume.
    /// Input is the temp file (.part), output is the final path.
    /// </summary>
    private async Task FfmpegProcess(string inputPath, string outputPath, int volume, bool isVideo, CancellationToken cancellationToken)
    {
        var encoderPath = FfmpegPathResolver.ResolveEncoderPath(_mediaEncoder);
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
                args = $"-i \"{inputPath}\" -c:v copy -filter:a \"volume={volStr}\" \"{outputPath}\"";
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

    private sealed record SeasonThemeMatchState(
        string Status,
        string Source,
        bool SameAsSeries,
        string? AnimeName,
        string? AnimeThemesSlug,
        int? AniListId,
        int? MyAnimeListId)
    {
        public bool HasAnimeIdentity =>
            !string.IsNullOrWhiteSpace(AnimeThemesSlug) ||
            AniListId.HasValue ||
            MyAnimeListId.HasValue;
    }
}
