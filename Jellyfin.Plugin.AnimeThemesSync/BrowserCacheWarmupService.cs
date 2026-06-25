using System;
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
/// </summary>
public sealed class BrowserCacheWarmupService : IHostedService, IDisposable
{
    private static readonly TimeSpan LibraryChangeDebounce = TimeSpan.FromSeconds(5);
    private readonly ILibraryManager _libraryManager;
    private readonly ThemeDownloader _themeDownloader;
    private readonly ILogger<BrowserCacheWarmupService> _logger;
    private readonly Timer _libraryChangeTimer;

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
        _libraryManager.ItemRemoved += OnLibraryItemChanged;
        _themeDownloader.EnsureBrowserCacheRebuildStarted();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnLibraryItemChanged;
        _libraryManager.ItemUpdated -= OnLibraryItemChanged;
        _libraryManager.ItemRemoved -= OnLibraryItemChanged;
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
        _libraryChangeTimer.Change(LibraryChangeDebounce, Timeout.InfiniteTimeSpan);
    }

    private void OnDebouncedLibraryChange(object? state)
    {
        _logger.LogDebug("AnimeThemes browser cache refresh scheduled after library change.");
        _ = _themeDownloader.StartBrowserCacheRebuild();
    }
}
