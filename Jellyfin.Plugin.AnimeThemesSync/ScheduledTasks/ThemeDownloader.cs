using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnimeThemesSync.Shared;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AnimeThemesSync.Configuration;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync.ScheduledTasks;

/// <summary>
/// Scheduled task to download OP/ED themes.
/// </summary>
public sealed class ThemeDownloader : IScheduledTask
{
    private const string BroadcastSeasonProviderKey = "AnimeThemesBroadcastSeason";
    private const string UserOwnedImageFingerprint = "user-owned";
    private static readonly SemaphoreSlim SeasonMetadataSyncGate = new(1, 1);
    private static readonly SemaphoreSlim SeasonMetadataOperationGate = new(1, 1);
    private static readonly SemaphoreSlim SeasonCollectionFinalizeGate = new(1, 1);
    private readonly ILibraryManager _libraryManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ThemeDownloader> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly AnimeThemesService _animeThemesService;
    private readonly AniListService _aniListService;
    private readonly AnimeThemesDataStore _dataStore;
    private readonly ISeasonFinderDataStore _seasonFinderStore;
    private readonly ICollectionManager _collectionManager;
    private readonly IProviderManager _providerManager;
    private readonly Services.SkiaCollectionImageRenderer _collectionImageRenderer = new();
    private int _browserCacheRebuildRunning;
    private int _seasonMetadataSyncRunning;
    private static CancellationTokenSource? _seasonMetadataSyncCancellation;
    private SeasonMetadataSyncStatus _seasonMetadataSyncStatus = new("Idle", 0, 0, null, null, null);
    private readonly Dictionary<string, string> _seasonMetadataRuleErrors = new(StringComparer.OrdinalIgnoreCase);
    private readonly AdjustableConcurrencyLimiter _downloadLimiter = new();

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
    /// <param name="dataStore">The AnimeThemes data store.</param>
    /// <param name="seasonFinderStore">The Season Finder SQLite store.</param>
    /// <param name="collectionManager">The media-server collection manager.</param>
    /// <param name="providerManager">The provider manager used to save generated collection images.</param>
    public ThemeDownloader(
        ILibraryManager libraryManager,
        IFileSystem fileSystem,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IMediaEncoder mediaEncoder,
        AnimeThemesService animeThemesService,
        AniListService aniListService,
        AnimeThemesDataStore dataStore,
        ISeasonFinderDataStore seasonFinderStore,
        ICollectionManager collectionManager,
        IProviderManager providerManager)
    {
        _providerManager = providerManager;
        _libraryManager = libraryManager;
        _fileSystem = fileSystem;
        _logger = loggerFactory.CreateLogger<ThemeDownloader>();
        _httpClientFactory = httpClientFactory;
        _mediaEncoder = mediaEncoder;
        _animeThemesService = animeThemesService;
        _aniListService = aniListService;
        _dataStore = dataStore;
        _seasonFinderStore = seasonFinderStore;
        _collectionManager = collectionManager;
        _seasonFinderStore.MigrateLegacyMappings(Plugin.Instance?.Configuration?.SeasonThemeMappings);
        ThemeExtrasManifestService.ConfigureStore(_dataStore);
        ThemeDownloadJobService.Configure(Plugin.Instance?.Configuration?.MaxConcurrentDownloads ?? 1);
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
        if (config == null)
        {
            return;
        }

        var items = GetEnabledLibraryItems();
        _logger.LogInformation("Found {Count} items to process.", items.Count);

        var result = config.ThemeDownloadingEnabled
            ? await ProcessItems(items, config, config.ForceRedownload, progress, cancellationToken).ConfigureAwait(false)
            : new ThemeDownloadExecutionResult(0, 0, 0, 0, 0, 0);
        await ExecuteSeasonMetadataMaintenanceAsync(progress, cancellationToken).ConfigureAwait(false);
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

        EnsureSeasonThemeDownloadsAllowed(item, config);

        _logger.LogInformation("Starting Anime Themes on-demand download for {ItemName} ({ItemId})...", item.Name, itemId);
        var result = await ProcessItems(new[] { item }, config, forceRedownload || config.ForceRedownload, progress, cancellationToken).ConfigureAwait(false);
        RefreshBrowserCacheForItem(item);
        return result;
    }

    public async Task<IReadOnlyList<ThemeDownloadJobStartResult>> StartItemDownloadBatchAsync(
        Guid itemId,
        bool forceRedownload,
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
        var audioConfig = CreateThemeConfig(item, config, isVideo: false);
        var videoConfig = CreateThemeConfig(item, config, isVideo: true);
        var plan = await ResolveItem(item, audioConfig, videoConfig, cancellationToken).ConfigureAwait(false);
        if (plan == null)
        {
            return Array.Empty<ThemeDownloadJobStartResult>();
        }

        var overwrite = forceRedownload || config.ForceRedownload;
        MigrateExtraFiles(plan.ExtraFiles, overwrite);
        var groups = BuildThemeDownloadGroups(item, plan, config, overwrite);
        if (groups.Count == 0)
        {
            FinalizeThemeDownloadBatch(item, plan, config);
            return Array.Empty<ThemeDownloadJobStartResult>();
        }

        ThemeDownloadJobService.Configure(config.MaxConcurrentDownloads);
        var remainingInitialJobs = groups.Count;
        var startedJobs = new List<ThemeDownloadJobStartResult>(groups.Count);
        foreach (var group in groups)
        {
            var descriptor = new ThemeDownloadJobDescriptor(
                "Theme",
                group.OutputTarget.LogicalItemId,
                group.RowId,
                BuildThemeDownloadJobTitle(group));
            startedJobs.Add(ThemeDownloadJobService.Start(
                descriptor,
                (progress, jobCancellationToken) => ExecuteThemeDownloadGroupAsync(group, config, overwrite, progress, jobCancellationToken),
                () =>
                {
                    if (Interlocked.Decrement(ref remainingInitialJobs) == 0)
                    {
                        try
                        {
                            FinalizeThemeDownloadBatch(item, plan, config);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to finalize the theme download batch for {ItemName}.", item.Name);
                        }
                    }
                }));
        }

        return startedJobs;
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
        string? savedFilter,
        string? broadcastSeason = null)
    {
        EnsureBrowserCacheRebuildStarted();
        return _dataStore.QueryBrowserItems(libraryId, startIndex, limit, sortBy, sortOrder, searchTerm, itemType, linkFilter, savedFilter, broadcastSeason);
    }

    public SeasonMetadataSyncStatus GetSeasonMetadataSyncStatus() => _seasonMetadataSyncStatus;

    public SeasonMetadataSyncStatus StartSeasonMetadataSync(
        bool removeManagedTags = false,
        bool removeManagedCollectionMemberships = false,
        bool forceRefresh = false)
    {
        if (Interlocked.CompareExchange(ref _seasonMetadataSyncRunning, 1, 0) != 0)
        {
            return _seasonMetadataSyncStatus;
        }

        var series = GetEnabledLibraryItems().OfType<Series>().ToList();
        _seasonMetadataSyncStatus = new SeasonMetadataSyncStatus("Running", 0, series.Count, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture), null, null);
        _seasonMetadataSyncCancellation = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            await RunSeasonMetadataMaintenanceReservedAsync(
                series,
                null,
                removeManagedTags,
                removeManagedCollectionMemberships,
                forceRefresh,
                swallowFailure: true,
                _seasonMetadataSyncCancellation.Token).ConfigureAwait(false);
        });
        return _seasonMetadataSyncStatus;
    }

    public SeasonMetadataSyncStatus CancelSeasonMetadataSync()
    {
        _seasonMetadataSyncCancellation?.Cancel();
        if (string.Equals(_seasonMetadataSyncStatus.State, "Running", StringComparison.Ordinal))
        {
            _seasonMetadataSyncStatus = _seasonMetadataSyncStatus with { State = "Cancelling" };
        }

        return _seasonMetadataSyncStatus;
    }

    public async Task ExecuteSeasonMetadataMaintenanceAsync(
        IProgress<double> progress,
        CancellationToken cancellationToken,
        bool forceRefresh = false)
    {
        if (Interlocked.CompareExchange(ref _seasonMetadataSyncRunning, 1, 0) != 0)
        {
            _logger.LogInformation("Season metadata maintenance is already running; skipping duplicate request.");
            return;
        }

        var series = GetEnabledLibraryItems().OfType<Series>().ToList();
        _seasonMetadataSyncStatus = new SeasonMetadataSyncStatus("Running", 0, series.Count, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture), null, null);
        await RunSeasonMetadataMaintenanceReservedAsync(series, progress, false, false, forceRefresh, swallowFailure: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunSeasonMetadataMaintenanceReservedAsync(
        List<Series> series,
        IProgress<double>? progress,
        bool removeManagedTags,
        bool removeManagedCollectionMemberships,
        bool forceRefresh,
        bool swallowFailure,
        CancellationToken cancellationToken)
    {
        var entered = false;
        try
        {
            await SeasonMetadataOperationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            await SynchronizeSeasonMetadataAsync(
                series,
                cancellationToken,
                removeManagedTags,
                removeManagedCollectionMemberships,
                forceRefresh,
                progress).ConfigureAwait(false);
            await RebuildBrowserCacheAsync(cancellationToken).ConfigureAwait(false);
            _seasonMetadataSyncStatus = _seasonMetadataSyncStatus with
            {
                State = _seasonMetadataSyncStatus.Failed > 0 ? "CompletedWithErrors" : "Completed",
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _seasonMetadataSyncStatus = _seasonMetadataSyncStatus with
            {
                State = "Cancelled",
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            };
            if (!swallowFailure)
            {
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Season metadata synchronization failed.");
            _seasonMetadataSyncStatus = _seasonMetadataSyncStatus with
            {
                State = "Failed",
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Error = ex.Message,
            };
            if (!swallowFailure)
            {
                throw;
            }
        }
        finally
        {
            if (entered)
            {
                SeasonMetadataOperationGate.Release();
            }

            Interlocked.Exchange(ref _seasonMetadataSyncRunning, 0);
        }
    }

    public AnimeThemesStorageStatus GetStorageStatus()
    {
        EnsureBrowserCacheRebuildStarted();
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        return _dataStore.GetStorageStatus(IsBrowserCacheRebuildRunning) with
        {
            SeasonFinder = _seasonFinderStore.GetStorageStatus(),
            CacheMaintenance = _seasonFinderStore.GetCacheMaintenanceStatus(config.SeasonMetadataCacheTtlDays, config.ProviderResponseCacheTtlDays),
        };
    }

    public AnimeThemesMaintenanceResult ClearBrowserCache()
    {
        _dataStore.ClearBrowserCache();
        _seasonFinderStore.ClearCache();
        return new AnimeThemesMaintenanceResult(true, "Browser cache cleared.");
    }

    public AnimeThemesMaintenanceResult ClearProviderCache()
    {
        _seasonFinderStore.ClearProviderCache();
        _animeThemesService.ClearProviderCache();
        return new AnimeThemesMaintenanceResult(false, "Provider response cache cleared. Season metadata and Browser data were preserved.");
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

    private async Task RebuildBrowserCacheCoreAsync(CancellationToken cancellationToken)
    {
        var records = new List<BrowserItemRecord>();
        var seasonRecords = new List<SeasonFinderRowRecord>();
        var libraryCounts = new Dictionary<Guid, (string? Name, int Count)>();
        var anySeriesSynchronized = false;
        foreach (var entry in GetEnabledLibraryItemsWithLibraries())
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<BroadcastSeasonValue> broadcastSeasons = [];
            if (entry.Item is Series series)
            {
                var state = _seasonFinderStore.GetSeasonAutomationState(series.Id.ToString("D"));
                broadcastSeasons = GetBroadcastSeasons(state);
                var seasons = GetSeasonItems(series).Where(IsSeasonEligibleForThemeMatching).ToList();
                var snapshot = _seasonFinderStore.GetSeasonMetadataSnapshot(series.Id.ToString("D"));
                if (state.Rules.Count == 0 || !IsSeasonMetadataSnapshotFresh(snapshot, BuildSeasonMetadataFingerprint(series, seasons), seasons, GetSeasonMetadataCacheTtlDays()))
                {
                    broadcastSeasons = await SynchronizeSeriesSeasonMetadataAsync(series, cancellationToken).ConfigureAwait(false);
                    anySeriesSynchronized = true;
                }

                seasonRecords.AddRange(GetSeasonItems(series)
                    .Where(IsSeasonEligibleForThemeMatching)
                    .Where(season => !string.IsNullOrWhiteSpace(season.Path))
                    .Select(season => BuildSeasonFinderRecord(series, season, entry.LibraryId)));
            }
            else if (entry.Item is Movie movie)
            {
                var anime = await ResolveAnime(movie, cancellationToken, logMissingIds: false).ConfigureAwait(false);
                var config = Plugin.Instance?.Configuration;
                var broadcastSeason = config == null || anime == null
                    ? null
                    : SeasonMetadataPlanner.CreateBroadcastSeason(
                        anime.Year,
                        anime.Season,
                        config.TagFormat,
                        config.TagSeasonSpring,
                        config.TagSeasonSummer,
                        config.TagSeasonFall,
                        config.TagSeasonWinter);
                broadcastSeasons = broadcastSeason == null ? [] : [broadcastSeason];
            }

            records.Add(BuildBrowserItemRecord(entry.Item, entry.LibraryId, broadcastSeasons));

            libraryCounts.TryGetValue(entry.LibraryId, out var current);
            libraryCounts[entry.LibraryId] = (entry.LibraryName, current.Count + 1);
        }

        _dataStore.ReplaceBrowserItems(
            records,
            libraryCounts.Select(pair => (pair.Key.ToString("D"), pair.Value.Name, pair.Value.Count)));
        _seasonFinderStore.ReplaceRows(seasonRecords);
        if (anySeriesSynchronized)
        {
            await FinalizeSeasonCollectionsSafelyAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Rebuilt AnimeThemes Browser cache. Items={Count}, Seasons={SeasonCount}", records.Count, seasonRecords.Count);
    }

    private async Task FinalizeSeasonCollectionsSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await FinalizeSeasonCollectionsAsync(false, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Season collection finalization failed after an on-demand season metadata sync.");
        }
    }

    private void RefreshBrowserCacheForItem(BaseItem item)
    {
        var target = ResolveBrowserCacheTarget(item);
        if (target != null)
        {
            _dataStore.UpsertBrowserItem(BuildBrowserTargetRecord(target));
        }
    }

    /// <summary>
    /// Batched variant of <see cref="RefreshBrowserCacheForItem"/> that writes the
    /// cache document once instead of once per item.
    /// </summary>
    private void RefreshBrowserCacheForItems(IEnumerable<BaseItem> items)
    {
        _dataStore.UpsertBrowserItems(ResolveBrowserCacheTargets(items)
            .Select(BuildBrowserTargetRecord)
            .ToList());
    }

    /// <summary>
    /// Applies a debounced batch of library change events to the browser cache.
    /// Additions and updates refresh only the affected items without network calls;
    /// removals and an unbuilt cache fall back to a full rebuild because the caches
    /// have no per-item delete path.
    /// </summary>
    public void ApplyLibraryChanges(IReadOnlyCollection<BaseItem> changedItems, bool anyItemRemoved)
    {
        if (anyItemRemoved || !_dataStore.IsBrowserCacheReady() || !_seasonFinderStore.IsCacheReady())
        {
            _ = StartBrowserCacheRebuild();
            return;
        }

        if (changedItems.Count == 0)
        {
            return;
        }

        try
        {
            var targets = ResolveBrowserCacheTargets(changedItems);
            _dataStore.UpsertBrowserItems(targets.Select(BuildBrowserTargetRecord).ToList());
            foreach (var series in targets.OfType<Series>())
            {
                RefreshSeasonFinderRowsForSeries(series);
            }

            _logger.LogDebug("Applied {Count} changed library items to the AnimeThemes browser cache.", targets.Count);
        }
        catch (Exception ex)
        {
            // Runs on a timer thread; an unhandled exception would take down the host.
            _logger.LogError(ex, "Failed to apply library changes to the AnimeThemes browser cache.");
        }
    }

    private List<BaseItem> ResolveBrowserCacheTargets(IEnumerable<BaseItem> items)
    {
        var targets = new Dictionary<Guid, BaseItem>();
        foreach (var item in items)
        {
            var target = ResolveBrowserCacheTarget(item);
            if (target != null)
            {
                targets[target.Id] = target;
            }
        }

        return [.. targets.Values];
    }

    private BaseItem? ResolveBrowserCacheTarget(BaseItem item)
    {
        if (item is Episode episode)
        {
            item = episode.Series ?? item;
        }

        if (item is Season season)
        {
            item = FindSeriesForSeason(season) ?? item;
        }

        return item is Series or Movie ? item : null;
    }

    private BrowserItemRecord BuildBrowserTargetRecord(BaseItem target)
    {
        var libraryId = ResolveLibraryId(target);
        var broadcastSeasons = target is Series seriesItem
            ? GetBroadcastSeasons(_seasonFinderStore.GetSeasonAutomationState(seriesItem.Id.ToString("D")))
            : [];
        return BuildBrowserItemRecord(target, libraryId, broadcastSeasons);
    }

    private void RefreshSeasonFinderRowsForSeries(Series series)
    {
        var libraryId = ResolveLibraryId(series);
        foreach (var record in GetSeasonItems(series)
                     .Where(IsSeasonEligibleForThemeMatching)
                     .Where(season => !string.IsNullOrWhiteSpace(season.Path))
                     .Select(season => BuildSeasonFinderRecord(series, season, libraryId)))
        {
            _seasonFinderStore.UpsertRow(record);
        }
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

    private BrowserItemRecord BuildBrowserItemRecord(BaseItem item, Guid? libraryId, IReadOnlyList<BroadcastSeasonValue>? broadcastSeasons = null)
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
            DateCreatedUtc = new DateTimeOffset(DateTime.SpecifyKind(item.DateCreated, DateTimeKind.Local)).ToUniversalTime(),
            LatestEpisodeDateUtc = item is Series series ? GetLatestEpisodeDateCreated(series)?.ToUniversalTime() : null,
            LastRefreshedUtc = DateTimeOffset.UtcNow,
            BroadcastSeasons = broadcastSeasons?.ToList() ?? [],
            SeasonSummaries = item is Series browserSeries ? _seasonFinderStore.GetSeasonSummaries(browserSeries.Id.ToString("D")).ToList() : [],
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
            new DateTimeOffset(DateTime.SpecifyKind(item.DateCreated, DateTimeKind.Local)),
            item is Series series ? GetLatestEpisodeDateCreated(series) : null,
            linkStatus,
            directLink,
            string.Equals(seasonLinkStatus, "Manual", StringComparison.OrdinalIgnoreCase),
            item is Series browserSeries
                ? GetBroadcastSeasons(_seasonFinderStore.GetSeasonAutomationState(browserSeries.Id.ToString("D"))).Select(i => i.Key).ToList()
                : null,
            item is Series summarySeries ? _seasonFinderStore.GetSeasonSummaries(summarySeries.Id.ToString("D")) : null);
    }

    private static List<BroadcastSeasonValue> GetBroadcastSeasons(SeasonAutomationState state) =>
        state.Rules
            .Where(i => i.AnimeYear.HasValue && !string.IsNullOrWhiteSpace(i.AnimeSeason) && !string.IsNullOrWhiteSpace(i.BroadcastSeasonKey))
            .Select(i => new BroadcastSeasonValue(i.BroadcastSeasonKey!, i.BroadcastSeasonLabel ?? i.BroadcastSeasonKey!, i.AnimeYear!.Value, i.AnimeSeason!))
            .GroupBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
            .Select(i => i.First())
            .ToList();

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
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            Recursive = true,
            Parent = series
        });

        return episodes
            .Select(episode => (DateTimeOffset?)new DateTimeOffset(DateTime.SpecifyKind(episode.DateCreated, DateTimeKind.Local)))
            .OrderByDescending(date => date)
            .FirstOrDefault();
    }

    public ThemeBrowserSummary GetBrowserSummary()
    {
        EnsureBrowserCacheRebuildStarted();
        return _dataStore.GetBrowserSummary();
    }

    public LocalMediaCleanupTaskStatus StartLocalMediaCleanupScan()
    {
        return LocalMediaCleanupTaskService.StartScan(ScanLocalMediaAsync);
    }

    public LocalMediaCleanupScanPage GetLocalMediaCleanupFiles(
        string scanId,
        int? startIndex,
        int? limit,
        string? status,
        string? sources,
        string? kinds,
        string? library,
        string? searchTerm)
    {
        return LocalMediaCleanupTaskService.GetFiles(scanId, startIndex, limit, status, sources, kinds, library, searchTerm);
    }

    public LocalMediaCleanupTaskStatus StartLocalMediaCleanupDelete(string scanId, IReadOnlyCollection<string> candidateIds)
    {
        return LocalMediaCleanupTaskService.StartDelete(scanId, candidateIds, DeleteScannedLocalMediaAsync);
    }

    public LocalMediaCleanupTaskStatus? GetLocalMediaCleanupTask(string taskId) => LocalMediaCleanupTaskService.GetTask(taskId);

    public LocalMediaCleanupTaskStatus? CancelLocalMediaCleanupTask(string taskId) => LocalMediaCleanupTaskService.Cancel(taskId);

    private async Task<IReadOnlyList<LocalMediaCleanupFile>> ScanLocalMediaAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
        var entries = GetEnabledLibraryItemsWithLibraries();
        var registry = _dataStore.GetThemeFiles()
            .GroupBy(file => Path.GetFullPath(file.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var found = new Dictionary<string, LocalMediaCleanupFile>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[index];
            var audioConfig = CreateThemeConfig(entry.Item, config, isVideo: false);
            var videoConfig = CreateThemeConfig(entry.Item, config, isVideo: true);
            ThemeOutputPlan? plan = null;
            var resolved = false;
            try
            {
                if (audioConfig.MaxThemes <= 0 && videoConfig.MaxThemes <= 0)
                {
                    resolved = true;
                }
                else
                {
                    plan = await ResolveItem(entry.Item, audioConfig, videoConfig, cancellationToken).ConfigureAwait(false);
                    resolved = plan != null;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not evaluate local media for {ItemName}; files will be marked Unknown.", entry.Item.Name);
            }

            var directories = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (plan != null)
            {
                foreach (var cleanup in plan.CleanupPlans)
                {
                    if (!directories.TryGetValue(cleanup.Directory, out var desired))
                    {
                        desired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        directories[cleanup.Directory] = desired;
                    }

                    desired.UnionWith(cleanup.DesiredFiles.Select(Path.GetFullPath));
                }
            }

            AddCleanupDirectories(ResolveThemeOutputTarget(entry.Item)?.OutputRootPath, directories);
            if (entry.Item is Series cleanupSeries)
            {
                foreach (var season in GetSeasonItems(cleanupSeries))
                {
                    AddCleanupDirectories(ResolveThemeOutputTarget(season, cleanupSeries)?.OutputRootPath, directories);
                }
            }

            foreach (var directory in directories)
            {
                if (!_fileSystem.DirectoryExists(directory.Key))
                {
                    continue;
                }

                foreach (var path in _fileSystem.GetFilePaths(directory.Key))
                {
                    if (!IsSupportedThemeFile(path))
                    {
                        continue;
                    }

                    var fullPath = Path.GetFullPath(path);
                    var info = new FileInfo(fullPath);
                    registry.TryGetValue(fullPath, out var tracked);
                    var logicalItem = tracked == null ? entry.Item : _libraryManager.GetItemById(tracked.LogicalItemId) ?? entry.Item;
                    var status = !resolved ? "Unknown" : directory.Value.Contains(fullPath) ? "Desired" : "Undesired";
                    found[fullPath] = new LocalMediaCleanupFile(
                        Guid.NewGuid().ToString("N"),
                        logicalItem.Id,
                        logicalItem.Name ?? entry.Item.Name ?? "Unknown",
                        logicalItem is Season ? "Season" : logicalItem is Series ? "Series" : "Movie",
                        entry.LibraryName,
                        fullPath,
                        Path.GetFileName(fullPath),
                        CleanupFileKind(directory.Key),
                        status,
                        tracked?.Source ?? "Untracked",
                        info.Exists ? info.Length : 0,
                        new DateTimeOffset(DateTime.SpecifyKind(info.LastWriteTimeUtc, DateTimeKind.Utc)));
                }
            }

            progress.Report((double)(index + 1) / Math.Max(1, entries.Count) * 100);
        }

        return found.Values.ToList();
    }

    private async Task<LocalMediaCleanupDeleteResult> DeleteScannedLocalMediaAsync(
        IReadOnlyList<LocalMediaCleanupFile> files,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var roots = GetCleanupOutputRoots();
        var deletedPaths = new List<string>();
        var deleted = 0;
        var skipped = 0;
        var failed = 0;
        long bytes = 0;
        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[index];
            try
            {
                var path = Path.GetFullPath(file.Path);
                if (!IsWithinCleanupRoots(path, roots) || !_fileSystem.FileExists(path))
                {
                    skipped++;
                    continue;
                }

                var info = new FileInfo(path);
                var lastWrite = new DateTimeOffset(DateTime.SpecifyKind(info.LastWriteTimeUtc, DateTimeKind.Utc));
                if (info.Length != file.Size || lastWrite != file.LastWriteTimeUtc)
                {
                    skipped++;
                    continue;
                }

                await DeleteFileWithRetryAsync(path, cancellationToken).ConfigureAwait(false);
                deleted++;
                bytes += file.Size;
                deletedPaths.Add(path);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(ex, "Failed to delete cleanup candidate {Path}.", file.Path);
            }
            finally
            {
                progress.Report((double)(index + 1) / files.Count * 100);
            }
        }

        _dataStore.RemoveThemeFilesByPaths(deletedPaths);
        _ = StartBrowserCacheRebuild();
        return new LocalMediaCleanupDeleteResult(deleted, bytes, skipped, failed);
    }

    private static void AddCleanupDirectories(string? root, Dictionary<string, HashSet<string>> directories)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        Add(Path.Combine(root, "theme-music"));
        Add(Path.Combine(root, "backdrops"));
        Add(Path.Combine(root, "extras"));

        void Add(string directory)
        {
            if (!directories.ContainsKey(directory))
            {
                directories[directory] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static string CleanupFileKind(string directory)
    {
        var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(name, "theme-music", StringComparison.OrdinalIgnoreCase) ? "Audio" :
            string.Equals(name, "extras", StringComparison.OrdinalIgnoreCase) ? "Extra" : "Video";
    }

    private List<string> GetCleanupOutputRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in GetEnabledLibraryItems())
        {
            var target = ResolveThemeOutputTarget(item);
            if (target != null)
            {
                roots.Add(Path.GetFullPath(target.OutputRootPath));
            }

            if (item is Series series)
            {
                foreach (var season in GetSeasonItems(series))
                {
                    var seasonTarget = ResolveThemeOutputTarget(season, series);
                    if (seasonTarget != null)
                    {
                        roots.Add(Path.GetFullPath(seasonTarget.OutputRootPath));
                    }
                }
            }
        }

        return roots.ToList();
    }

    private static bool IsWithinCleanupRoots(string path, IEnumerable<string> roots)
    {
        return roots.Any(root => path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
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
        int? seasonNumber,
        string? sortBy,
        string? sortOrder)
    {
        EnsureBrowserCacheRebuildStarted();
        return _seasonFinderStore.QueryRows(libraryId, startIndex, limit, searchTerm, status, seasonNumber, sortBy, sortOrder);
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
        await SynchronizeSeriesSeasonMetadataAsync(series, cancellationToken).ConfigureAwait(false);
        await FinalizeSeasonCollectionsSafelyAsync(cancellationToken).ConfigureAwait(false);
        RefreshBrowserCacheForItem(series);
        return result;
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
        await SynchronizeSeriesSeasonMetadataAsync(series, cancellationToken).ConfigureAwait(false);
        await FinalizeSeasonCollectionsSafelyAsync(cancellationToken).ConfigureAwait(false);
        RefreshBrowserCacheForItem(series);
        return result;
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
            await SynchronizeSeriesSeasonMetadataAsync(series, cancellationToken).ConfigureAwait(false);
            RefreshBrowserCacheForItem(series);
        }

        if (changedSeasons.Count > 0)
        {
            await FinalizeSeasonCollectionsSafelyAsync(cancellationToken).ConfigureAwait(false);
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
            "Jellyfin",
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
            _dataStore.UpsertThemeFile(file.OutputTarget ?? outputTarget, file.ThemeKey, file.IsVideo ? "video" : "audio", file.Path, "BrowserManual");
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
                _dataStore.UpsertThemeFile(extra.OutputTarget ?? outputTarget, extra.Key, "extra", extra.TargetPath, "BrowserManual");
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
                                _dataStore.UpsertThemeFile(dl.OutputTarget, dl.File.ThemeKey, dl.File.IsVideo ? "video" : "audio", dl.File.Path, "Scheduled");
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
                _dataStore.UpsertThemeFile(extra.OutputTarget, extra.Extra.Key, "extra", extra.Extra.TargetPath, "Scheduled");
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
        RefreshBrowserCacheForItems(items);

        progress?.Report(100);

        return new ThemeDownloadExecutionResult(items.Count, allDownloads.Count, completedDownloads, allExtras.Count, completedExtras, failedExtras);
    }

    private List<PlannedThemeDownloadGroup> BuildThemeDownloadGroups(
        BaseItem sourceItem,
        ThemeOutputPlan plan,
        PluginConfiguration config,
        bool overwrite)
    {
        var groups = new Dictionary<string, PlannedThemeDownloadGroup>(StringComparer.OrdinalIgnoreCase);
        if (!config.AllowAdd)
        {
            return [];
        }

        foreach (var file in plan.MediaFiles)
        {
            var outputTarget = file.OutputTarget ?? ResolveThemeOutputTarget(sourceItem);
            if (outputTarget == null || string.IsNullOrWhiteSpace(file.SourceRowId) || (!overwrite && _fileSystem.FileExists(file.Path)))
            {
                continue;
            }

            var group = GetOrCreateGroup(outputTarget, file.SourceRowId, file.DisplayTitle);
            group.MediaFiles.Add(file);
        }

        foreach (var extra in plan.ExtraFiles)
        {
            var outputTarget = extra.OutputTarget ?? ResolveThemeOutputTarget(sourceItem);
            if (outputTarget == null || string.IsNullOrWhiteSpace(extra.Key) || (!overwrite && _fileSystem.FileExists(extra.TargetPath)))
            {
                continue;
            }

            var group = GetOrCreateGroup(outputTarget, extra.Key, extra.DisplayTitle);
            group.ExtraFiles.Add(extra);
        }

        return groups.Values
            .OrderBy(group => group.OutputTarget.LogicalItemId)
            .ThenBy(group => group.DisplayTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PlannedThemeDownloadGroup GetOrCreateGroup(ThemeOutputTarget outputTarget, string rowId, string displayTitle)
        {
            var key = outputTarget.LogicalItemId.ToString("N", CultureInfo.InvariantCulture) + ":" + rowId;
            if (!groups.TryGetValue(key, out var group))
            {
                group = new PlannedThemeDownloadGroup(outputTarget, rowId, displayTitle);
                groups[key] = group;
            }

            return group;
        }
    }

    private string BuildThemeDownloadJobTitle(PlannedThemeDownloadGroup group)
    {
        var targetItem = _libraryManager.GetItemById(group.OutputTarget.LogicalItemId);
        var itemTitle = targetItem?.Name ?? "Unknown";
        if (targetItem is Season season)
        {
            var series = FindSeriesForSeason(season);
            if (!string.IsNullOrWhiteSpace(series?.Name))
            {
                itemTitle = series.Name + " / " + itemTitle;
            }
        }

        return string.IsNullOrWhiteSpace(group.DisplayTitle) ? itemTitle : itemTitle + " · " + group.DisplayTitle;
    }

    private async Task<ThemeDownloadExecutionResult> ExecuteThemeDownloadGroupAsync(
        PlannedThemeDownloadGroup group,
        PluginConfiguration config,
        bool overwrite,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var targetItem = _libraryManager.GetItemById(group.OutputTarget.LogicalItemId);
        var audioVolume = CreateThemeConfig(targetItem ?? throw new KeyNotFoundException("The download target item was not found."), config, isVideo: false).Volume;
        var videoVolume = CreateThemeConfig(targetItem, config, isVideo: true).Volume;
        var mediaFiles = group.MediaFiles.Where(file => overwrite || !_fileSystem.FileExists(file.Path)).ToList();
        var extraFiles = group.ExtraFiles.Where(extra => overwrite || !_fileSystem.FileExists(extra.TargetPath)).ToList();
        var totalSteps = Math.Max(1, mediaFiles.Count + extraFiles.Count);
        var completedSteps = 0;

        foreach (var file in mediaFiles)
        {
            var directory = Path.GetDirectoryName(file.Path);
            if (directory != null && !_fileSystem.DirectoryExists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            var transferProgress = CreateStepProgress(progress, 0, 100, completedSteps, totalSteps);
            await DownloadPlannedFileWithRetryAsync(
                file,
                file.IsVideo ? videoVolume : audioVolume,
                transferProgress,
                cancellationToken).ConfigureAwait(false);
            _dataStore.UpsertThemeFile(group.OutputTarget, file.ThemeKey, file.IsVideo ? "video" : "audio", file.Path, "BrowserManual");
            completedSteps++;
            progress.Report((double)completedSteps / totalSteps * 100);
        }

        foreach (var extra in extraFiles)
        {
            ThemeExtraFileResult result;
            if (!string.IsNullOrWhiteSpace(extra.SourcePath))
            {
                result = ThemeExtrasFileService.EnsureExtraFileDetailed(
                    extra.SourcePath,
                    extra.TargetPath,
                    config.ExtrasLinkMode,
                    overwrite);
            }
            else if (!string.IsNullOrWhiteSpace(extra.DownloadUrl))
            {
                var transferProgress = CreateStepProgress(progress, 0, 100, completedSteps, totalSteps);
                await DownloadFile(
                    extra.DownloadUrl,
                    extra.TargetPath,
                    videoVolume,
                    isVideo: true,
                    requiresTranscoding: extra.RequiresTranscoding,
                    cancellationToken,
                    transferProgress).ConfigureAwait(false);
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

            ThemeExtrasManifestService.UpdateExtraFile(extra);
            _dataStore.UpsertThemeFile(group.OutputTarget, extra.Key, "extra", extra.TargetPath, "BrowserManual");
            completedSteps++;
            progress.Report((double)completedSteps / totalSteps * 100);
        }

        RefreshBrowserCacheForItem(targetItem is Season season ? FindSeriesForSeason(season) ?? targetItem : targetItem);
        return new ThemeDownloadExecutionResult(1, mediaFiles.Count, mediaFiles.Count, extraFiles.Count, extraFiles.Count, 0);
    }

    private async Task DownloadPlannedFileWithRetryAsync(
        ThemeFilePlan file,
        int volume,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        const int MaxAttempts = 3;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await DownloadFile(file.Url, file.Path, volume, file.IsVideo, file.RequiresTranscoding, cancellationToken, progress).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(ex, "Download attempt {Attempt}/{MaxAttempts} failed for {Url}. Retrying...", attempt, MaxAttempts, file.Url);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void FinalizeThemeDownloadBatch(BaseItem item, ThemeOutputPlan plan, PluginConfiguration config)
    {
        RefreshBrowserCacheForItem(item is Season season ? FindSeriesForSeason(season) ?? item : item);
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
        return GetEnabledLibraryItemsWithLibraries()
            .Select(i => i.Item)
            .ToList();
    }

    private async Task SynchronizeSeasonMetadataAsync(
        List<Series> seriesItems,
        CancellationToken cancellationToken,
        bool removeManagedTags = false,
        bool removeManagedCollectionMemberships = false,
        bool forceRefresh = false,
        IProgress<double>? progress = null)
    {
        for (var index = 0; index < seriesItems.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await SynchronizeSeriesSeasonMetadataAsync(
                    seriesItems[index],
                    cancellationToken,
                    removeManagedTags,
                    removeManagedCollectionMemberships,
                    forceRefresh).ConfigureAwait(false);
                if (string.Equals(_seasonMetadataSyncStatus.State, "Running", StringComparison.Ordinal))
                {
                    _seasonMetadataSyncStatus = _seasonMetadataSyncStatus with { Succeeded = _seasonMetadataSyncStatus.Succeeded + 1 };
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Season metadata synchronization failed for {SeriesName}.", seriesItems[index].Name);
                AddSeasonMetadataSyncError(seriesItems[index], null, "Series", ex);
            }

            if (string.Equals(_seasonMetadataSyncStatus.State, "Running", StringComparison.Ordinal))
            {
                _seasonMetadataSyncStatus = _seasonMetadataSyncStatus with { Processed = index + 1 };
            }

            progress?.Report(seriesItems.Count == 0 ? 100 : (index + 1) * 100d / seriesItems.Count);
        }

        await FinalizeSeasonCollectionsAsync(removeManagedCollectionMemberships, cancellationToken).ConfigureAwait(false);
    }

    private async Task FinalizeSeasonCollectionsAsync(bool removeManagedCollectionMemberships, CancellationToken cancellationToken)
    {
        await SeasonCollectionFinalizeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await FinalizeSeasonCollectionsCoreAsync(removeManagedCollectionMemberships, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SeasonCollectionFinalizeGate.Release();
        }
    }

    private async Task FinalizeSeasonCollectionsCoreAsync(bool removeManagedCollectionMemberships, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            return;
        }

        List<BoxSet> managed;
        IReadOnlyList<ManagedSeasonCollectionAssetState> assetStates;
        try
        {
            managed = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                Recursive = true,
            }).OfType<BoxSet>()
                .Where(i => i.ProviderIds != null && i.ProviderIds.ContainsKey(BroadcastSeasonProviderKey))
                .ToList();
            assetStates = _seasonFinderStore.GetCollectionAssetStates();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Season collection finalization could not enumerate managed collections.");
            return;
        }

        var states = assetStates.ToDictionary(i => i.CollectionKey, StringComparer.OrdinalIgnoreCase);
        var liveCollectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var collection in managed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!collection.ProviderIds.TryGetValue(BroadcastSeasonProviderKey, out var collectionKey) || string.IsNullOrWhiteSpace(collectionKey))
            {
                continue;
            }

            liveCollectionKeys.Add(collectionKey);
            states.TryGetValue(collectionKey, out var state);
            state ??= new ManagedSeasonCollectionAssetState { CollectionKey = collectionKey };
            state.CollectionItemId = collection.Id.ToString("D");
            try
            {
                var shouldLock = config.SeasonCollectionsEnabled && config.SeasonCollectionLockEnabled && !removeManagedCollectionMemberships;
                var shouldUnlock = (!config.SeasonCollectionLockEnabled || removeManagedCollectionMemberships) && state.LockAppliedByPlugin;
                var lockChanged = false;
                if (shouldLock && !collection.IsLocked)
                {
                    collection.IsLocked = true;
                    state.LockAppliedByPlugin = true;
                    lockChanged = true;
                }
                else if (shouldUnlock)
                {
                    if (collection.IsLocked)
                    {
                        collection.IsLocked = false;
                        lockChanged = true;
                    }

                    state.LockAppliedByPlugin = false;
                }

                if (lockChanged)
                {
                    var parent = collection.GetParent()
                        ?? await _collectionManager.GetCollectionsFolder(true).ConfigureAwait(false)
                        ?? _libraryManager.RootFolder;
                    await _libraryManager.UpdateItemAsync(collection, parent, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                }

                if (removeManagedCollectionMemberships && collection.GetLinkedChildren().Count == 0)
                {
                    _libraryManager.DeleteItem(collection, new DeleteOptions { DeleteFileLocation = true });
                    _seasonFinderStore.DeleteCollectionAssetState(collectionKey);
                    liveCollectionKeys.Remove(collectionKey);
                    _logger.LogInformation("Deleted empty plugin-created season collection {CollectionName}.", collection.Name);
                    continue;
                }

                await GenerateSeasonCollectionImagesAsync(collection, config, state, cancellationToken).ConfigureAwait(false);
                state.LastError = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Season collection finalization failed for {CollectionName}.", collection.Name);
                state.LastError = ex.GetBaseException().Message;
            }

            state.UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            _seasonFinderStore.UpsertCollectionAssetState(state);
        }

        if (removeManagedCollectionMemberships)
        {
            foreach (var staleState in assetStates.Where(i => !liveCollectionKeys.Contains(i.CollectionKey)))
            {
                _seasonFinderStore.DeleteCollectionAssetState(staleState.CollectionKey);
            }
        }
    }

    private async Task GenerateSeasonCollectionImagesAsync(
        BoxSet collection,
        PluginConfiguration config,
        ManagedSeasonCollectionAssetState state,
        CancellationToken cancellationToken)
    {
        if (!config.SeasonCollectionsEnabled || !config.SeasonCollectionImagesEnabled)
        {
            return;
        }

        var memberArt = GetCollectionMemberArt(collection);
        if (memberArt.Count == 0)
        {
            _logger.LogDebug("No usable member artwork for collection {CollectionName}; skipping image generation.", collection.Name);
            return;
        }

        var backdropOverlay = config.SeasonCollectionBackdropOverlayEnabled
            ? FormattableString.Invariant($"{config.SeasonCollectionBackdropOverlayOpacity}:{config.SeasonCollectionBackdropOverlayColor}")
            : "off";
        var backdropOpacity = config.SeasonCollectionBackdropOverlayEnabled ? config.SeasonCollectionBackdropOverlayOpacity : 0;
        var canvasSettings = FormattableString.Invariant(
            $"{(int)config.SeasonCollectionPosterFillMode}:{(int)config.SeasonCollectionLandscapeSourceMode}:{config.SeasonCollectionCanvasColor}:{config.SeasonCollectionCanvasOpacity}:{(int)config.SeasonCollectionPosterFillLandscapeType}:{(int)config.SeasonCollectionCanvasLandscapeType}");
        var fillMode = config.SeasonCollectionPosterFillMode;

        var primarySources = CollectionImageLayoutEngine.SelectPosterCanvasSources(
            memberArt,
            fillMode,
            config.SeasonCollectionPosterFillLandscapeType);

        var landscapeCanvasSources = CollectionImageLayoutEngine.SelectLandscapeCanvasSources(
            memberArt,
            config.SeasonCollectionLandscapeSourceMode,
            config.SeasonCollectionCanvasLandscapeType);
        var preferredCanvasKind = landscapeCanvasSources.Count > 0 ? landscapeCanvasSources[0].Kind : CollectionImageSourceKind.Landscape;
        var thumbCap = preferredCanvasKind == CollectionImageSourceKind.Poster
            ? CollectionImageLayoutEngine.PosterTilesMaxOnLandscapeCanvas
            : CollectionImageLayoutEngine.ThumbMaxImages;
        var backdropCap = preferredCanvasKind == CollectionImageSourceKind.Poster
            ? CollectionImageLayoutEngine.PosterTilesMaxOnLandscapeCanvas
            : CollectionImageLayoutEngine.BackdropMaxImages;
        var thumbSources = landscapeCanvasSources.Take(thumbCap).ToList();
        var backdropSources = landscapeCanvasSources.Take(backdropCap).ToList();

        var changed = false;
        if (primarySources.Count > 0)
        {
            changed |= await GenerateCollectionImageSlotAsync(
                collection,
                state,
                ImageType.Primary,
                1000,
                1500,
                primarySources,
                sources => CollectionImageLayoutEngine.ComputePosterCanvasLayout(
                    1000,
                    1500,
                    sources.Count(source => source.Kind == CollectionImageSourceKind.Poster),
                    sources.Any(source => source.Kind == CollectionImageSourceKind.Landscape),
                    fillMode),
                "none",
                null,
                0,
                canvasSettings,
                config,
                cancellationToken).ConfigureAwait(false);
        }

        if (landscapeCanvasSources.Count > 0)
        {
            changed |= await GenerateCollectionImageSlotAsync(
                collection,
                state,
                ImageType.Thumb,
                1280,
                720,
                thumbSources,
                sources => CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1280, 720, sources),
                "none",
                null,
                0,
                canvasSettings,
                config,
                cancellationToken).ConfigureAwait(false);
            changed |= await GenerateCollectionImageSlotAsync(
                collection,
                state,
                ImageType.Backdrop,
                1920,
                1080,
                backdropSources,
                sources => CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, sources),
                backdropOverlay,
                config.SeasonCollectionBackdropOverlayColor,
                backdropOpacity,
                canvasSettings,
                config,
                cancellationToken).ConfigureAwait(false);
        }

        if (changed)
        {
            state.LastGeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }
    }

    private async Task<bool> GenerateCollectionImageSlotAsync(
        BoxSet collection,
        ManagedSeasonCollectionAssetState state,
        ImageType imageType,
        int width,
        int height,
        List<CollectionImageSource> sources,
        Func<IReadOnlyList<CollectionImageLayoutSource>, CollectionImageLayoutResult> layoutForSources,
        string overlaySettings,
        string? overlayColor,
        int overlayOpacityPercent,
        string canvasSettings,
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        var (storedFingerprint, storedIdentity) = GetImageSlotState(state, imageType);
        var fingerprint = CollectionImageFingerprint.Compute(imageType.ToString(), width, height, overlaySettings, canvasSettings, sources);
        var slotImages = GetCollectionImageSlotEntries(collection, imageType);
        if (string.Equals(storedFingerprint, UserOwnedImageFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        var trackedImage = FindTrackedCollectionImage(slotImages, storedIdentity);
        if (string.Equals(fingerprint, storedFingerprint, StringComparison.Ordinal) && trackedImage != null)
        {
            return false;
        }

        if (slotImages.Count > 0 && (storedIdentity == null || trackedImage == null))
        {
            SetImageSlotState(state, imageType, UserOwnedImageFingerprint, null);
            return true;
        }

        CollectionImageRenderResult? rendered;
        try
        {
            rendered = _collectionImageRenderer.Render(
                sources.Select(i => new CollectionImageRenderSource(i.Path, i.Kind)).ToList(),
                width,
                height,
                layoutForSources,
                overlayColor,
                overlayOpacityPercent,
                config.SeasonCollectionCanvasColor,
                config.SeasonCollectionCanvasOpacity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rendering the {ImageType} image failed for collection {CollectionName}.", imageType, collection.Name);
            return false;
        }

        if (rendered == null)
        {
            return false;
        }

        slotImages = GetCollectionImageSlotEntries(collection, imageType);
        trackedImage = FindTrackedCollectionImage(slotImages, storedIdentity);
        if (storedIdentity != null && trackedImage == null && slotImages.Count > 0)
        {
            SetImageSlotState(state, imageType, UserOwnedImageFingerprint, null);
            return true;
        }

        if (storedIdentity == null && slotImages.Count > 0)
        {
            SetImageSlotState(state, imageType, UserOwnedImageFingerprint, null);
            return true;
        }

        if (trackedImage != null)
        {
            try
            {
                await collection.DeleteImageAsync(imageType, trackedImage.Index).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deleting the previous generated {ImageType} image failed for collection {CollectionName}; the replacement was not saved.", imageType, collection.Name);
                return false;
            }
        }

        var imagesBeforeSave = GetCollectionImageSlotEntries(collection, imageType);

        using (var stream = new MemoryStream(rendered.Value.Data))
        {
            await _providerManager.SaveImage(collection, stream, rendered.Value.MimeType, imageType, null, cancellationToken).ConfigureAwait(false);
        }

        var parent = collection.GetParent()
            ?? await _collectionManager.GetCollectionsFolder(true).ConfigureAwait(false)
            ?? _libraryManager.RootFolder;
        await _libraryManager.UpdateItemAsync(collection, parent, ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);
        var writtenImage = FindWrittenCollectionImage(imagesBeforeSave, GetCollectionImageSlotEntries(collection, imageType));
        if (writtenImage?.Identity == null)
        {
            SetImageSlotState(state, imageType, UserOwnedImageFingerprint, null);
            _logger.LogWarning("Generated the {ImageType} image for collection {CollectionName}, but its written file could not be identified; the slot will no longer be replaced automatically.", imageType, collection.Name);
            return true;
        }

        SetImageSlotState(state, imageType, fingerprint, writtenImage.Identity);
        _logger.LogInformation("Generated the {ImageType} image for collection {CollectionName} from {SourceCount} member images.", imageType, collection.Name, sources.Count);
        return true;
    }

    private static List<CollectionImageSlotEntry> GetCollectionImageSlotEntries(BaseItem collection, ImageType imageType)
    {
        return collection.GetImages(imageType)
            .Select((image, index) => new CollectionImageSlotEntry(index, image.Path, GetImageFileIdentity(image.Path)))
            .ToList();
    }

    private static CollectionImageSlotEntry? FindTrackedCollectionImage(IReadOnlyList<CollectionImageSlotEntry> images, string? storedIdentity)
    {
        return storedIdentity == null
            ? null
            : images.FirstOrDefault(image => string.Equals(image.Identity, storedIdentity, StringComparison.Ordinal));
    }

    private static CollectionImageSlotEntry? FindWrittenCollectionImage(
        IReadOnlyList<CollectionImageSlotEntry> before,
        IReadOnlyList<CollectionImageSlotEntry> after)
    {
        return after.FirstOrDefault(image => !before.Any(previous =>
            string.Equals(previous.Path, image.Path, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(previous.Identity, image.Identity, StringComparison.Ordinal)));
    }

    private sealed record CollectionImageSlotEntry(int Index, string? Path, string? Identity);

    private static (string? Fingerprint, string? WrittenFileIdentity) GetImageSlotState(ManagedSeasonCollectionAssetState state, ImageType imageType)
    {
        return imageType switch
        {
            ImageType.Primary => (state.PrimaryFingerprint, state.PrimaryWrittenFileIdentity),
            ImageType.Thumb => (state.ThumbFingerprint, state.ThumbWrittenFileIdentity),
            _ => (state.BackdropFingerprint, state.BackdropWrittenFileIdentity),
        };
    }

    private static void SetImageSlotState(ManagedSeasonCollectionAssetState state, ImageType imageType, string? fingerprint, string? writtenFileIdentity)
    {
        switch (imageType)
        {
            case ImageType.Primary:
                state.PrimaryFingerprint = fingerprint;
                state.PrimaryWrittenFileIdentity = writtenFileIdentity;
                break;
            case ImageType.Thumb:
                state.ThumbFingerprint = fingerprint;
                state.ThumbWrittenFileIdentity = writtenFileIdentity;
                break;
            default:
                state.BackdropFingerprint = fingerprint;
                state.BackdropWrittenFileIdentity = writtenFileIdentity;
                break;
        }
    }

    private static string? GetImageFileIdentity(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var file = new FileInfo(path);
            return file.Exists
                ? FormattableString.Invariant($"{file.Length}:{file.LastWriteTimeUtc.Ticks}")
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private List<CollectionMemberArtwork> GetCollectionMemberArt(BoxSet collection)
    {
        var members = collection.GetLinkedChildren()
            .Where(i => i != null)
            .OrderBy(i => i.PremiereDate ?? DateTime.MaxValue)
            .ThenBy(i => i.SortName, StringComparer.Ordinal)
            .ThenBy(i => i.Id)
            .ToList();
        var art = new List<CollectionMemberArtwork>();
        var seenPosters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenThumbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenBackdrops = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in members)
        {
            var posterInfo = member.GetImageInfo(ImageType.Primary, 0);
            var thumbInfo = member.GetImageInfo(ImageType.Thumb, 0);
            var backdropInfo = member.GetImageInfo(ImageType.Backdrop, 0);
            if (member is Season season && season.Series != null)
            {
                if (posterInfo == null || !posterInfo.IsLocalFile)
                {
                    posterInfo = season.Series.GetImageInfo(ImageType.Primary, 0);
                }

                if (thumbInfo == null || !thumbInfo.IsLocalFile)
                {
                    thumbInfo = season.Series.GetImageInfo(ImageType.Thumb, 0);
                }

                if (backdropInfo == null || !backdropInfo.IsLocalFile)
                {
                    backdropInfo = season.Series.GetImageInfo(ImageType.Backdrop, 0);
                }
            }

            var entry = new CollectionMemberArtwork(
                BuildImageSource(posterInfo, CollectionImageSourceKind.Poster, seenPosters),
                BuildImageSource(thumbInfo, CollectionImageSourceKind.Landscape, seenThumbs),
                BuildImageSource(backdropInfo, CollectionImageSourceKind.Landscape, seenBackdrops));
            if (entry.Poster != null || entry.Thumb != null || entry.Backdrop != null)
            {
                art.Add(entry);
            }
        }

        return art;
    }

    private static CollectionImageSource? BuildImageSource(ItemImageInfo? info, CollectionImageSourceKind kind, HashSet<string> seenPaths)
    {
        if (info == null || !info.IsLocalFile || string.IsNullOrWhiteSpace(info.Path))
        {
            return null;
        }

        try
        {
            var file = new FileInfo(info.Path);
            if (file.Exists && seenPaths.Add(file.FullName))
            {
                return new CollectionImageSource(file.FullName, file.Length, file.LastWriteTimeUtc.Ticks, kind);
            }
        }
        catch (Exception)
        {
            // Unreadable artwork paths are simply skipped.
        }

        return null;
    }

    private async Task<IReadOnlyList<BroadcastSeasonValue>> SynchronizeSeriesSeasonMetadataAsync(
        Series series,
        CancellationToken cancellationToken,
        bool removeManagedTags = false,
        bool removeManagedCollectionMemberships = false,
        bool forceRefresh = false)
    {
        await SeasonMetadataSyncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await SynchronizeSeriesSeasonMetadataCoreAsync(
                series,
                removeManagedTags,
                removeManagedCollectionMemberships,
                forceRefresh,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SeasonMetadataSyncGate.Release();
        }
    }

    private async Task<IReadOnlyList<BroadcastSeasonValue>> SynchronizeSeriesSeasonMetadataCoreAsync(
        Series series,
        bool removeManagedTags,
        bool removeManagedCollectionMemberships,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration
            ?? throw new InvalidOperationException("AnimeThemes Sync configuration is unavailable.");
        _seasonMetadataRuleErrors.Clear();
        var previousAutomation = _seasonFinderStore.GetSeasonAutomationState(series.Id.ToString("D"));
        var previous = BuildLegacySeasonMetadataState(
            previousAutomation,
            _dataStore.GetSeasonMetadataState(series.Id.ToString("D")));
        var seasons = GetSeasonItems(series).Where(IsSeasonEligibleForThemeMatching).ToList();
        var fingerprint = BuildSeasonMetadataFingerprint(series, seasons);
        var persistedSnapshot = _seasonFinderStore.GetSeasonMetadataSnapshot(series.Id.ToString("D"));
        var snapshot = persistedSnapshot
            ?? CreateSnapshotFromAutomationState(series, seasons, previousAutomation, fingerprint, config.SeasonMetadataCacheTtlDays);
        if (snapshot != null && persistedSnapshot == null)
        {
            _seasonFinderStore.SaveSeasonMetadataSnapshot(snapshot);
        }

        if (forceRefresh || !IsSeasonMetadataSnapshotFresh(snapshot, fingerprint, seasons, config.SeasonMetadataCacheTtlDays))
        {
            var stale = snapshot != null &&
                string.Equals(snapshot.InputFingerprint, fingerprint, StringComparison.Ordinal) &&
                IsSeasonMetadataSnapshotComplete(snapshot, seasons)
                ? snapshot
                : null;
            var seriesAnime = await ResolveAnime(series, cancellationToken, logMissingIds: false).ConfigureAwait(false);
            var automaticSeasonAnime = seriesAnime == null
                ? new Dictionary<Guid, AnimeThemesAnime>()
                : await BuildAutomaticSeasonAnimeMapAsync(series, seasons, seriesAnime, cancellationToken).ConfigureAwait(false);
            var rows = new List<SeasonMetadataRow>();
            var refreshFailed = false;
            foreach (var season in seasons)
            {
                automaticSeasonAnime.TryGetValue(season.Id, out var automaticAnime);
                var matchState = BuildSeasonThemeMatchState(series, season, automaticAnime);
                var resolution = await ResolveSeasonBrowserAnimeAsync(series, season, seriesAnime, automaticSeasonAnime, cancellationToken).ConfigureAwait(false);
                if (resolution.Anime == null && !string.Equals(matchState.Status, "Unmatched", StringComparison.OrdinalIgnoreCase))
                {
                    refreshFailed = true;
                    break;
                }

                var resolvedIds = ExtractAnimeExternalIds(resolution.Anime);
                rows.Add(new SeasonMetadataRow
                {
                    SeasonItemId = season.Id.ToString("D"),
                    SeasonName = season.Name ?? $"Season {season.IndexNumber}",
                    SeasonNumber = season.IndexNumber,
                    Status = matchState.Status,
                    Source = matchState.Source,
                    SameAsSeries = resolution.SameAsSeries,
                    AnimeThemesSlug = resolution.Anime?.Slug ?? matchState.AnimeThemesSlug,
                    AniListId = resolvedIds.AniListId ?? matchState.AniListId,
                    MyAnimeListId = resolvedIds.MyAnimeListId ?? matchState.MyAnimeListId,
                    AnimeYear = resolution.Anime?.Year,
                    AnimeSeason = resolution.Anime?.Season,
                });
            }

            if (refreshFailed)
            {
                const string RefreshError = "One or more identified seasons could not be resolved from the providers.";
                if (stale == null)
                {
                    var failedAt = DateTimeOffset.UtcNow;
                    _seasonFinderStore.SaveSeasonMetadataSnapshot(new SeasonMetadataSnapshot
                    {
                        SeriesItemId = series.Id.ToString("D"), SeriesName = series.Name, InputFingerprint = fingerprint,
                        ResolvedAtUtc = failedAt.ToString("O", CultureInfo.InvariantCulture),
                        ExpiresAtUtc = failedAt.ToString("O", CultureInfo.InvariantCulture), LastError = RefreshError,
                    });
                    _logger.LogWarning("Season metadata refresh failed for {SeriesName}; preserving previous automation state.", series.Name);
                    return previous.BroadcastSeasons;
                }

                stale.LastError = RefreshError;
                _seasonFinderStore.SaveSeasonMetadataSnapshot(stale);
                _logger.LogWarning("Using stale season metadata for {SeriesName} after provider refresh failed.", series.Name);
                snapshot = stale;
            }
            else
            {
                var now = DateTimeOffset.UtcNow;
                snapshot = new SeasonMetadataSnapshot
                {
                    SeriesItemId = series.Id.ToString("D"),
                    SeriesName = series.Name,
                    InputFingerprint = BuildSeasonMetadataFingerprint(series, seasons),
                    ResolvedAtUtc = now.ToString("O", CultureInfo.InvariantCulture),
                    ExpiresAtUtc = now.AddDays(config.SeasonMetadataCacheTtlDays).ToString("O", CultureInfo.InvariantCulture),
                    LastError = null,
                    Seasons = rows,
                };
                _seasonFinderStore.SaveSeasonMetadataSnapshot(snapshot);
            }
        }

        var resolved = BuildResolvedSeasonMetadata(seasons, snapshot!, config);

        var desiredTags = new Dictionary<Guid, HashSet<string>>();
        var generatedTags = new Dictionary<Guid, HashSet<string>>();
        foreach (var entry in resolved)
        {
            var tags = SeasonMetadataPlanner.BuildTags(entry.BroadcastSeason);
            AddTags(generatedTags, series.Id, tags);
            AddTags(generatedTags, entry.Season.Id, tags);
            if (config.TagsEnabled)
            {
                if (SeasonMetadataPlanner.AppliesToSeries(config.SeasonTagTarget))
                {
                    AddTags(desiredTags, series.Id, tags);
                }

                if (SeasonMetadataPlanner.AppliesToSeason(config.SeasonTagTarget))
                {
                    AddTags(desiredTags, entry.Season.Id, tags);
                }
            }
        }

        var managedTags = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var tagItems = new Dictionary<Guid, BaseItem> { [series.Id] = series };
        foreach (var season in seasons)
        {
            tagItems[season.Id] = season;
        }

        foreach (var item in tagItems.Values)
        {
            previous.ManagedTags.TryGetValue(item.Id.ToString("D"), out var oldManagedTags);
            if (!config.TagsEnabled && !removeManagedTags)
            {
                if (oldManagedTags?.Count > 0)
                {
                    managedTags[item.Id.ToString("D")] = oldManagedTags.ToList();
                }

                continue;
            }

            desiredTags.TryGetValue(item.Id, out var itemDesiredTags);
            generatedTags.TryGetValue(item.Id, out var itemGeneratedTags);
            try
            {
                var newlyManaged = await ReconcileTagsAsync(
                    item,
                    item is Season ? series : item.GetParent(),
                    oldManagedTags ?? [],
                    itemDesiredTags ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    itemGeneratedTags ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    cancellationToken).ConfigureAwait(false);
                if (newlyManaged.Count > 0)
                {
                    managedTags[item.Id.ToString("D")] = newlyManaged;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Season tag synchronization failed for {SeriesName} / {ItemName}.", series.Name, item.Name);
                AddSeasonMetadataSyncError(series, item is Season ? "id:" + item.Id.ToString("D") : null, "Tag", ex);
                if (oldManagedTags?.Count > 0)
                {
                    managedTags[item.Id.ToString("D")] = oldManagedTags.ToList();
                }
            }
        }

        var memberships = await ReconcileCollectionsAsync(
            series,
            resolved,
            previous.CollectionMemberships,
            config,
            removeManagedCollectionMemberships,
            cancellationToken).ConfigureAwait(false);
        var broadcastSeasons = resolved
            .Select(i => i.BroadcastSeason)
            .GroupBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
            .Select(i => i.First())
            .ToList();
        var automationState = BuildSeasonAutomationState(
            series,
            resolved,
            snapshot!.Seasons,
            managedTags,
            memberships,
            previousAutomation);
        PreserveDisabledAutomationState(
            automationState,
            previousAutomation,
            preserveTags: !config.TagsEnabled && !removeManagedTags,
            preserveCollections: !config.SeasonCollectionsEnabled && !removeManagedCollectionMemberships);
        _seasonFinderStore.SaveSeasonAutomationState(automationState);
        RefreshBrowserCacheForItem(series);
        return broadcastSeasons;

        static void AddTags(Dictionary<Guid, HashSet<string>> target, Guid itemId, IReadOnlyList<string> tags)
        {
            if (!target.TryGetValue(itemId, out var values))
            {
                values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                target[itemId] = values;
            }

            values.UnionWith(tags.Where(i => !string.IsNullOrWhiteSpace(i)));
        }
    }

    private static bool IsSeasonMetadataSnapshotFresh(SeasonMetadataSnapshot? snapshot, string fingerprint, IReadOnlyList<Season> seasons, int ttlDays)
    {
        return snapshot != null &&
            string.Equals(snapshot.InputFingerprint, fingerprint, StringComparison.Ordinal) &&
            IsSeasonMetadataSnapshotComplete(snapshot, seasons) &&
            DateTimeOffset.TryParse(
                snapshot.ResolvedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var resolved) &&
            resolved.AddDays(Math.Clamp(ttlDays, 1, 365)) > DateTimeOffset.UtcNow;
    }

    private static int GetSeasonMetadataCacheTtlDays() => Math.Clamp(Plugin.Instance?.Configuration?.SeasonMetadataCacheTtlDays ?? 30, 1, 365);

    private static bool IsSeasonMetadataSnapshotComplete(SeasonMetadataSnapshot snapshot, IReadOnlyList<Season> seasons)
    {
        return snapshot.Seasons.Count == seasons.Count &&
            seasons.All(season => snapshot.Seasons.Any(row => string.Equals(row.SeasonItemId, season.Id.ToString("D"), StringComparison.OrdinalIgnoreCase)));
    }

    private string BuildSeasonMetadataFingerprint(Series series, IReadOnlyList<Season> seasons)
    {
        var values = new List<string> { "v1", series.Id.ToString("D"), GetItemAnimeThemesSlug(series) ?? string.Empty };
        var seriesIds = ExtractItemProviderIds(series);
        values.Add(seriesIds.AniListId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        values.Add(seriesIds.MyAnimeListId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        var mappings = _seasonFinderStore.GetSeasonThemeMappings();
        foreach (var season in seasons.OrderBy(i => i.Id))
        {
            var ids = ExtractItemProviderIds(season);
            var mapping = FindSeasonThemeMapping(mappings, series, season);
            values.Add(string.Join(
                "|",
                season.Id.ToString("D"),
                season.IndexNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                GetItemAnimeThemesSlug(season) ?? string.Empty,
                ids.AniListId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                ids.MyAnimeListId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                mapping?.Enabled == true ? "1" : "0",
                mapping?.Locked == true ? "1" : "0",
                mapping?.AnimeThemesSlug ?? string.Empty,
                mapping?.AniListId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                mapping?.MyAnimeListId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values))));
    }

    private static SeasonMetadataSnapshot? CreateSnapshotFromAutomationState(
        Series series,
        List<Season> seasons,
        SeasonAutomationState automation,
        string fingerprint,
        int ttlDays)
    {
        if (seasons.Count == 0 || automation.Rules.Count != seasons.Count ||
            seasons.Any(season => automation.Rules.All(rule => !string.Equals(rule.SeasonItemId, season.Id.ToString("D"), StringComparison.OrdinalIgnoreCase))))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        return new SeasonMetadataSnapshot
        {
            SeriesItemId = series.Id.ToString("D"),
            SeriesName = series.Name,
            InputFingerprint = fingerprint,
            ResolvedAtUtc = now.ToString("O", CultureInfo.InvariantCulture),
            ExpiresAtUtc = now.AddDays(Math.Clamp(ttlDays, 1, 365)).ToString("O", CultureInfo.InvariantCulture),
            Seasons = automation.Rules.Select(rule => new SeasonMetadataRow
            {
                SeasonItemId = rule.SeasonItemId,
                SeasonName = rule.SeasonName,
                SeasonNumber = rule.SeasonNumber,
                Status = string.Equals(rule.Source, "SeasonThemeMappings", StringComparison.OrdinalIgnoreCase) ? "Manual" : "Auto",
                Source = rule.Source,
                SameAsSeries = string.Equals(rule.Source, "SeriesLevel", StringComparison.OrdinalIgnoreCase),
                AnimeThemesSlug = rule.AnimeThemesSlug,
                AniListId = rule.AniListId,
                MyAnimeListId = rule.MyAnimeListId,
                AnimeYear = rule.AnimeYear,
                AnimeSeason = rule.AnimeSeason,
            }).ToList(),
        };
    }

    private static List<(Season Season, BroadcastSeasonValue BroadcastSeason)> BuildResolvedSeasonMetadata(
        IReadOnlyList<Season> seasons,
        SeasonMetadataSnapshot snapshot,
        PluginConfiguration config)
    {
        var resolved = new List<(Season Season, BroadcastSeasonValue BroadcastSeason)>();
        foreach (var season in seasons)
        {
            var row = snapshot.Seasons.FirstOrDefault(i => string.Equals(i.SeasonItemId, season.Id.ToString("D"), StringComparison.OrdinalIgnoreCase));
            if (row?.AnimeYear is not int year || string.IsNullOrWhiteSpace(row.AnimeSeason))
            {
                continue;
            }

            var broadcastSeason = SeasonMetadataPlanner.CreateBroadcastSeason(
                year,
                row.AnimeSeason,
                config.TagFormat,
                config.TagSeasonSpring,
                config.TagSeasonSummer,
                config.TagSeasonFall,
                config.TagSeasonWinter);
            if (broadcastSeason != null)
            {
                resolved.Add((season, broadcastSeason));
            }
        }

        return resolved;
    }

    private SeasonAutomationState BuildSeasonAutomationState(
        Series series,
        IReadOnlyList<(Season Season, BroadcastSeasonValue BroadcastSeason)> resolved,
        IReadOnlyList<SeasonMetadataRow> metadataRows,
        IReadOnlyDictionary<string, List<string>> managedTags,
        IReadOnlyList<SeasonCollectionMembershipState> memberships,
        SeasonAutomationState previous)
    {
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var state = new SeasonAutomationState { SeriesItemId = series.Id.ToString("D") };
        foreach (var entry in resolved)
        {
            var metadata = metadataRows.First(i => string.Equals(i.SeasonItemId, entry.Season.Id.ToString("D"), StringComparison.OrdinalIgnoreCase));
            var ruleKey = "id:" + entry.Season.Id.ToString("D");
            _seasonMetadataRuleErrors.TryGetValue(ruleKey, out var error);
            state.Rules.Add(new SeasonAutomationRuleRecord
            {
                RuleKey = ruleKey, SeriesItemId = series.Id.ToString("D"), SeasonItemId = entry.Season.Id.ToString("D"),
                SeasonName = entry.Season.Name ?? $"Season {entry.Season.IndexNumber}", SeasonNumber = entry.Season.IndexNumber,
                AnimeThemesSlug = metadata.AnimeThemesSlug, AniListId = metadata.AniListId, MyAnimeListId = metadata.MyAnimeListId,
                AnimeYear = entry.BroadcastSeason.Year, AnimeSeason = entry.BroadcastSeason.Season,
                BroadcastSeasonKey = entry.BroadcastSeason.Key, BroadcastSeasonLabel = entry.BroadcastSeason.Label,
                Source = metadata.Source, ResolvedAtUtc = now, LastError = error, UpdatedAtUtc = now,
            });
        }

        foreach (var pair in managedTags)
        {
            foreach (var entry in resolved.Where(entry =>
                         string.Equals(pair.Key, series.Id.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(pair.Key, entry.Season.Id.ToString("D"), StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var tag in pair.Value.Intersect(SeasonMetadataPlanner.BuildTags(entry.BroadcastSeason), StringComparer.OrdinalIgnoreCase))
                {
                    state.Tags.Add(new SeasonAutomationTagRecord
                    {
                        RuleKey = "id:" + entry.Season.Id.ToString("D"), TargetItemId = pair.Key,
                        TargetItemType = string.Equals(pair.Key, series.Id.ToString("D"), StringComparison.OrdinalIgnoreCase) ? "Series" : "Season",
                        TagName = tag, Source = "SeasonMetadata", AddedByPlugin = true,
                        LastAppliedAtUtc = now, UpdatedAtUtc = now,
                    });
                }
            }
        }

        foreach (var membership in memberships)
        {
            var entry = resolved.FirstOrDefault(i =>
                string.Equals(i.BroadcastSeason.Key, membership.BroadcastSeasonKey, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(i.Season.Id.ToString("D"), membership.ItemId, StringComparison.OrdinalIgnoreCase) || i.Season.IndexNumber == 1));
            if (entry.Season == null)
            {
                continue;
            }

            state.Collections.Add(new ManagedSeasonCollectionRecord
            {
                CollectionKey = membership.BroadcastSeasonKey, CollectionName = membership.CollectionName,
                CollectionItemId = membership.CollectionId,
                IsPluginCreated = IsPluginManagedCollection(membership.CollectionId, membership.BroadcastSeasonKey) ||
                    previous.Collections.FirstOrDefault(i => string.Equals(i.CollectionKey, membership.BroadcastSeasonKey, StringComparison.OrdinalIgnoreCase))?.IsPluginCreated == true,
                UpdatedAtUtc = now,
            });
            state.CollectionMembers.Add(new ManagedSeasonCollectionMemberRecord
            {
                CollectionKey = membership.BroadcastSeasonKey, RuleKey = "id:" + entry.Season.Id.ToString("D"),
                TargetItemId = membership.ItemId,
                TargetItemType = string.Equals(membership.ItemId, series.Id.ToString("D"), StringComparison.OrdinalIgnoreCase) ? "Series" : "Season",
                AddedByPlugin = membership.AddedByPlugin, LastAppliedAtUtc = now, UpdatedAtUtc = now,
            });
        }

        state.Collections = state.Collections.GroupBy(i => i.CollectionKey, StringComparer.OrdinalIgnoreCase).Select(i => i.First()).ToList();
        foreach (var entry in resolved)
        {
            var ruleKey = "id:" + entry.Season.Id.ToString("D");
            if (!_seasonMetadataRuleErrors.TryGetValue(ruleKey, out var error))
            {
                continue;
            }

            var collectionSeason = SeasonMetadataPlanner.CreateBroadcastSeason(
                entry.BroadcastSeason.Year,
                entry.BroadcastSeason.Season,
                Plugin.Instance?.Configuration?.SeasonCollectionFormat ?? "{Season} {Year}",
                Plugin.Instance?.Configuration?.TagSeasonSpring ?? "Spring",
                Plugin.Instance?.Configuration?.TagSeasonSummer ?? "Summer",
                Plugin.Instance?.Configuration?.TagSeasonFall ?? "Fall",
                Plugin.Instance?.Configuration?.TagSeasonWinter ?? "Winter");
            if (collectionSeason != null && state.Collections.All(i => !string.Equals(i.CollectionKey, collectionSeason.Key, StringComparison.OrdinalIgnoreCase)))
            {
                var prior = previous.Collections.FirstOrDefault(i => string.Equals(i.CollectionKey, collectionSeason.Key, StringComparison.OrdinalIgnoreCase));
                state.Collections.Add(new ManagedSeasonCollectionRecord
                {
                    CollectionKey = collectionSeason.Key, CollectionName = collectionSeason.Label,
                    CollectionItemId = prior?.CollectionItemId, IsPluginCreated = prior?.IsPluginCreated == true,
                    LastError = error, UpdatedAtUtc = now,
                });
            }
        }

        return state;
    }

    private bool IsPluginManagedCollection(string collectionId, string collectionKey)
    {
        return Guid.TryParse(collectionId, out var id) &&
            _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                Recursive = true,
            }).OfType<BoxSet>().Any(i => i.Id == id && i.ProviderIds != null &&
                i.ProviderIds.TryGetValue(BroadcastSeasonProviderKey, out var key) && string.Equals(key, collectionKey, StringComparison.OrdinalIgnoreCase));
    }

    private static SeasonMetadataState BuildLegacySeasonMetadataState(SeasonAutomationState automation, SeasonMetadataState? legacy)
    {
        if (automation.Rules.Count == 0)
        {
            return legacy ?? new SeasonMetadataState { SeriesItemId = automation.SeriesItemId };
        }

        var collections = automation.Collections.ToDictionary(i => i.CollectionKey, StringComparer.OrdinalIgnoreCase);
        return new SeasonMetadataState
        {
            SeriesItemId = automation.SeriesItemId,
            ManagedTags = automation.Tags.Where(i => i.AddedByPlugin).GroupBy(i => i.TargetItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(i => i.Key, i => i.Select(t => t.TagName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase),
            CollectionMemberships = automation.CollectionMembers.Select(member =>
            {
                collections.TryGetValue(member.CollectionKey, out var collection);
                return new SeasonCollectionMembershipState
                {
                    BroadcastSeasonKey = member.CollectionKey, CollectionId = collection?.CollectionItemId ?? string.Empty,
                    CollectionName = collection?.CollectionName ?? member.CollectionKey, ItemId = member.TargetItemId,
                    AddedByPlugin = member.AddedByPlugin,
                };
            }).ToList(),
            BroadcastSeasons = automation.Rules.Where(i => i.AnimeYear.HasValue && !string.IsNullOrWhiteSpace(i.AnimeSeason))
                .Select(i => new BroadcastSeasonValue(i.BroadcastSeasonKey ?? string.Empty, i.BroadcastSeasonLabel ?? string.Empty, i.AnimeYear!.Value, i.AnimeSeason!))
                .GroupBy(i => i.Key, StringComparer.OrdinalIgnoreCase).Select(i => i.First()).ToList(),
        };
    }

    private static void PreserveDisabledAutomationState(
        SeasonAutomationState current,
        SeasonAutomationState previous,
        bool preserveTags,
        bool preserveCollections)
    {
        if (preserveTags)
        {
            current.Tags.AddRange(previous.Tags);
        }

        if (preserveCollections)
        {
            current.Collections.AddRange(previous.Collections);
            current.CollectionMembers.AddRange(previous.CollectionMembers);
        }

        var referencedRuleKeys = current.Tags.Select(i => i.RuleKey)
            .Concat(current.CollectionMembers.Select(i => i.RuleKey))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        current.Rules.AddRange(previous.Rules.Where(i => referencedRuleKeys.Contains(i.RuleKey)));
        current.Rules = current.Rules.GroupBy(i => i.RuleKey, StringComparer.OrdinalIgnoreCase).Select(i => i.First()).ToList();
        current.Tags = current.Tags.GroupBy(i => i.RuleKey + "|" + i.TargetItemId + "|" + i.TagName, StringComparer.OrdinalIgnoreCase).Select(i => i.First()).ToList();
        current.Collections = current.Collections.GroupBy(i => i.CollectionKey, StringComparer.OrdinalIgnoreCase).Select(i => i.First()).ToList();
        current.CollectionMembers = current.CollectionMembers.GroupBy(i => i.CollectionKey + "|" + i.TargetItemId, StringComparer.OrdinalIgnoreCase).Select(i => i.First()).ToList();
    }

    private async Task<List<string>> ReconcileTagsAsync(
        BaseItem item,
        BaseItem? parent,
        IReadOnlyList<string> oldManagedTags,
        HashSet<string> desiredTags,
        HashSet<string> generatedTags,
        CancellationToken cancellationToken)
    {
        var current = (item.Tags ?? []).ToList();
        var changed = false;
        foreach (var oldTag in oldManagedTags.Where(i => !desiredTags.Contains(i)))
        {
            changed |= current.RemoveAll(i => string.Equals(i, oldTag, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        foreach (var generatedTag in generatedTags.Where(i => !desiredTags.Contains(i)))
        {
            changed |= current.RemoveAll(i => string.Equals(i, generatedTag, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        var managed = new List<string>();
        foreach (var tag in desiredTags)
        {
            var existed = current.Any(i => string.Equals(i, tag, StringComparison.OrdinalIgnoreCase));
            if (!existed)
            {
                current.Add(tag);
                changed = true;
                managed.Add(tag);
            }
            else
            {
                managed.Add(tag);
            }
        }

        if (changed)
        {
            item.Tags = current.ToArray();
            var updateParent = parent ?? item.GetParent() ?? _libraryManager.RootFolder;
            await _libraryManager.UpdateItemAsync(item, updateParent, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        }

        return managed;
    }

    private async Task<List<SeasonCollectionMembershipState>> ReconcileCollectionsAsync(
        Series series,
        IReadOnlyList<(Season Season, BroadcastSeasonValue BroadcastSeason)> resolved,
        IReadOnlyList<SeasonCollectionMembershipState> previous,
        PluginConfiguration config,
        bool removeManagedCollectionMemberships,
        CancellationToken cancellationToken)
    {
        var next = !config.SeasonCollectionsEnabled && !removeManagedCollectionMemberships
            ? previous.ToList()
            : new List<SeasonCollectionMembershipState>();
        if (config.SeasonCollectionsEnabled)
        {
            foreach (var entry in resolved)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                var collectionSeason = SeasonMetadataPlanner.CreateBroadcastSeason(
                    entry.BroadcastSeason.Year,
                    entry.BroadcastSeason.Season,
                    config.SeasonCollectionFormat,
                    config.TagSeasonSpring,
                    config.TagSeasonSummer,
                    config.TagSeasonFall,
                    config.TagSeasonWinter);
                if (collectionSeason == null)
                {
                    continue;
                }

                BaseItem member = SeasonMetadataPlanner.UsesSeriesForCollection(entry.Season.IndexNumber, config.SeasonOneCollectionUseSeries) ? series : entry.Season;
                var old = previous.FirstOrDefault(i =>
                    string.Equals(i.BroadcastSeasonKey, collectionSeason.Key, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(i.ItemId, member.Id.ToString("D"), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(i.CollectionName, collectionSeason.Label, StringComparison.OrdinalIgnoreCase));
                var collection = await GetOrCreateSeasonCollectionAsync(collectionSeason, cancellationToken).ConfigureAwait(false);
                var contains = collection.ContainsLinkedChildByItemId(member.Id);
                var addedByPlugin = old?.AddedByPlugin == true;
                if (!contains)
                {
                    await _collectionManager.AddToCollectionAsync(collection.Id, new[] { member.Id }).ConfigureAwait(false);
                    addedByPlugin = true;
                }

                next.Add(new SeasonCollectionMembershipState
                {
                    BroadcastSeasonKey = collectionSeason.Key,
                    CollectionId = collection.Id.ToString("D"),
                    CollectionName = collection.Name ?? collectionSeason.Label,
                    ItemId = member.Id.ToString("D"),
                    AddedByPlugin = addedByPlugin,
                });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Season collection synchronization failed for {SeriesName} / {SeasonName}.", series.Name, entry.Season.Name);
                    AddSeasonMetadataSyncError(series, "id:" + entry.Season.Id.ToString("D"), "Collection", ex);
                    next.AddRange(previous.Where(i =>
                        string.Equals(i.BroadcastSeasonKey, entry.BroadcastSeason.Key, StringComparison.OrdinalIgnoreCase) &&
                        (string.Equals(i.ItemId, entry.Season.Id.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
                         (entry.Season.IndexNumber == 1 && string.Equals(i.ItemId, series.Id.ToString("D"), StringComparison.OrdinalIgnoreCase)))));
                }
            }
        }

        var collections = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.BoxSet },
            Recursive = true,
        }).OfType<BoxSet>().ToList();
        foreach (var stale in previous.Where(old => (config.SeasonCollectionsEnabled || removeManagedCollectionMemberships) && old.AddedByPlugin && !next.Any(current =>
                     string.Equals(current.CollectionId, old.CollectionId, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(current.ItemId, old.ItemId, StringComparison.OrdinalIgnoreCase))))
        {
            if (Guid.TryParse(stale.CollectionId, out var collectionId) &&
                collections.Any(i => i.Id == collectionId) &&
                Guid.TryParse(stale.ItemId, out var itemId) &&
                _libraryManager.GetItemById(itemId) != null)
            {
                await _collectionManager.RemoveFromCollectionAsync(collectionId, new[] { itemId }).ConfigureAwait(false);
            }
        }

        return next
            .GroupBy(i => i.CollectionId + "|" + i.ItemId, StringComparer.OrdinalIgnoreCase)
            .Select(i => i.First())
            .ToList();
    }

    private async Task<BoxSet> GetOrCreateSeasonCollectionAsync(BroadcastSeasonValue broadcastSeason, CancellationToken cancellationToken)
    {
        var collections = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.BoxSet },
            Recursive = true,
        }).OfType<BoxSet>().ToList();
        var managed = collections.FirstOrDefault(i =>
            i.ProviderIds.TryGetValue(BroadcastSeasonProviderKey, out var key) && string.Equals(key, broadcastSeason.Key, StringComparison.OrdinalIgnoreCase));
        if (managed != null)
        {
            if (!string.Equals(managed.Name, broadcastSeason.Label, StringComparison.Ordinal))
            {
                managed.Name = broadcastSeason.Label;
                var parent = managed.GetParent()
                    ?? await _collectionManager.GetCollectionsFolder(true).ConfigureAwait(false)
                    ?? _libraryManager.RootFolder;
                await _libraryManager.UpdateItemAsync(managed, parent, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
            }

            return managed;
        }

        var existing = collections.FirstOrDefault(i => string.Equals(i.Name, broadcastSeason.Label, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing;
        }

        var config = Plugin.Instance?.Configuration;
        var lockOnCreate = config?.SeasonCollectionLockEnabled ?? false;
        var created = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
        {
            Name = broadcastSeason.Label,
            IsLocked = lockOnCreate,
            ProviderIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [BroadcastSeasonProviderKey] = broadcastSeason.Key },
            ItemIdList = Array.Empty<string>(),
        }).ConfigureAwait(false);
        if (created == null)
        {
            collections = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                Recursive = true,
            }).OfType<BoxSet>().ToList();
            created = collections.FirstOrDefault(i =>
                    i.ProviderIds != null &&
                    i.ProviderIds.TryGetValue(BroadcastSeasonProviderKey, out var key) &&
                    string.Equals(key, broadcastSeason.Key, StringComparison.OrdinalIgnoreCase))
                ?? collections.FirstOrDefault(i => string.Equals(i.Name, broadcastSeason.Label, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Jellyfin did not return or persist collection '{broadcastSeason.Label}'.");
        }

        if (lockOnCreate)
        {
            RecordCollectionLockApplied(broadcastSeason.Key, created.Id.ToString("D"));
        }

        return created;
    }

    private void RecordCollectionLockApplied(string collectionKey, string collectionItemId)
    {
        var state = _seasonFinderStore.GetCollectionAssetStates()
                .FirstOrDefault(i => string.Equals(i.CollectionKey, collectionKey, StringComparison.OrdinalIgnoreCase))
            ?? new ManagedSeasonCollectionAssetState { CollectionKey = collectionKey };
        state.CollectionItemId = collectionItemId;
        state.LockAppliedByPlugin = true;
        state.UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        _seasonFinderStore.UpsertCollectionAssetState(state);
    }

    private void AddSeasonMetadataSyncError(Series series, string? ruleKey, string stage, Exception exception)
    {
        if (!string.IsNullOrWhiteSpace(ruleKey))
        {
            _seasonMetadataRuleErrors[ruleKey] = stage + ": " + exception.GetBaseException().Message;
        }

        if (!string.Equals(_seasonMetadataSyncStatus.State, "Running", StringComparison.Ordinal))
        {
            return;
        }

        var errors = (_seasonMetadataSyncStatus.Errors ?? []).Take(19).ToList();
        errors.Add(new SeasonMetadataSyncError(
            series.Id.ToString("D"),
            series.Name,
            ruleKey,
            stage,
            exception.GetBaseException().Message));
        _seasonMetadataSyncStatus = _seasonMetadataSyncStatus with
        {
            Failed = _seasonMetadataSyncStatus.Failed + 1,
            Error = errors[0].Message,
            Errors = errors,
        };
    }

    private List<(BaseItem Item, Guid LibraryId, string? LibraryName)> GetEnabledLibraryItemsWithLibraries()
    {
        var root = _libraryManager.RootFolder;
        var enabledFolders = new Dictionary<Guid, string?>();

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
                    enabledFolders[folder.Id] = folder.Name;
                }
                else
                {
                    _logger.LogWarning("AnimeThemesSync is NOT enabled for library: {LibraryName}.", folder.Name);
                }
            }
        }

        var items = new List<(BaseItem Item, Guid LibraryId, string? LibraryName)>();
        foreach (var folder in enabledFolders)
        {
            var library = _libraryManager.GetItemById(folder.Key) as Folder;
            if (library != null)
            {
                var folderItems = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series, BaseItemKind.Movie },
                    Recursive = true,
                    Parent = library
                });
                items.AddRange(folderItems.Select(item => (item, folder.Key, folder.Value)));
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

    private sealed class PlannedThemeDownloadGroup
    {
        public PlannedThemeDownloadGroup(ThemeOutputTarget outputTarget, string rowId, string displayTitle)
        {
            OutputTarget = outputTarget;
            RowId = rowId;
            DisplayTitle = displayTitle;
        }

        public ThemeOutputTarget OutputTarget { get; }

        public string RowId { get; }

        public string DisplayTitle { get; }

        public List<ThemeFilePlan> MediaFiles { get; } = [];

        public List<ThemeExtraPlan> ExtraFiles { get; } = [];
    }

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
