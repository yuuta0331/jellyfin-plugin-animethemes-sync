using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AnimeThemesSync.ScheduledTasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync;

/// <summary>
/// Keeps the AnimeThemes browser cache warm as the Jellyfin library changes.
/// Added and updated items are applied as differential cache updates; removals
/// fall back to a full rebuild.
/// </summary>
public sealed class BrowserCacheWarmupService : IHostedService, IDisposable
{
    private static readonly TimeSpan LibraryChangeDebounce = TimeSpan.FromSeconds(5);
    private readonly ILibraryManager _libraryManager;
    private readonly ThemeDownloader _themeDownloader;
    private readonly ILogger<BrowserCacheWarmupService> _logger;
    private readonly Timer _libraryChangeTimer;
    private readonly object _pendingSync = new();
    private readonly Dictionary<Guid, BaseItem> _pendingItems = new();
    private bool _pendingRemoval;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserCacheWarmupService"/> class.
    /// </summary>
    public BrowserCacheWarmupService(
        ILibraryManager libraryManager,
        ThemeDownloader themeDownloader,
        ILogger<BrowserCacheWarmupService> logger)
    {
        _libraryManager = libraryManager;
        _themeDownloader = themeDownloader;
        _logger = logger;
        _libraryChangeTimer = new Timer(OnDebouncedLibraryChange, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnLibraryItemChanged;
        _libraryManager.ItemUpdated += OnLibraryItemChanged;
        _libraryManager.ItemRemoved += OnLibraryItemRemoved;
        _themeDownloader.EnsureBrowserCacheRebuildStarted();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnLibraryItemChanged;
        _libraryManager.ItemUpdated -= OnLibraryItemChanged;
        _libraryManager.ItemRemoved -= OnLibraryItemRemoved;
        _libraryChangeTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _libraryChangeTimer.Dispose();
    }

    private void OnLibraryItemChanged(object? sender, ItemChangeEventArgs e)
    {
        lock (_pendingSync)
        {
            if (e.Item != null)
            {
                _pendingItems[e.Item.Id] = e.Item;
            }
            else
            {
                // Without the item we cannot refresh differentially; rebuild instead.
                _pendingRemoval = true;
            }
        }

        _libraryChangeTimer.Change(LibraryChangeDebounce, Timeout.InfiniteTimeSpan);
    }

    private void OnLibraryItemRemoved(object? sender, ItemChangeEventArgs e)
    {
        lock (_pendingSync)
        {
            _pendingRemoval = true;
        }

        _libraryChangeTimer.Change(LibraryChangeDebounce, Timeout.InfiniteTimeSpan);
    }

    private void OnDebouncedLibraryChange(object? state)
    {
        List<BaseItem> changedItems;
        bool anyItemRemoved;
        lock (_pendingSync)
        {
            changedItems = _pendingItems.Values.ToList();
            _pendingItems.Clear();
            anyItemRemoved = _pendingRemoval;
            _pendingRemoval = false;
        }

        _logger.LogDebug(
            "AnimeThemes browser cache update after library change. ChangedItems={Count}, Removal={Removal}",
            changedItems.Count,
            anyItemRemoved);
        _themeDownloader.ApplyLibraryChanges(changedItems, anyItemRemoved);
    }
}
