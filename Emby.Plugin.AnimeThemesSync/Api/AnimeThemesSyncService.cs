using System;
using System.Collections.Generic;
using System.Threading;
using AnimeThemesSync.Shared.Models;
using Emby.Plugin.AnimeThemesSync.ScheduledTasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;

namespace Emby.Plugin.AnimeThemesSync.Api;

/// <summary>
/// Request to download AnimeThemes media for one item.
/// </summary>
[Route("/AnimeThemesSync/Items/{ItemId}/DownloadThemes", "POST", Summary = "Downloads AnimeThemes media for one item.")]
public class DownloadAnimeThemesForItem : IReturn<ThemeDownloadExecutionResult>
{
    /// <summary>
    /// Gets or sets the Emby item identifier.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether existing files should be replaced.
    /// </summary>
    public bool Force { get; set; }
}

/// <summary>
/// Request to list AnimeThemes-enabled library items.
/// </summary>
[Route("/AnimeThemesSync/Items", "GET", Summary = "Gets AnimeThemes-enabled library items.")]
public class GetAnimeThemesItems : IReturn<IReadOnlyList<ThemeBrowserLibraryItem>>
{
}

/// <summary>
/// Request to get AnimeThemes Browser rows for one item.
/// </summary>
[Route("/AnimeThemesSync/Items/{ItemId}/Themes", "GET", Summary = "Gets AnimeThemes Browser rows for one item.")]
public class GetAnimeThemesForItem : IReturn<ThemeBrowserItemResult>
{
    /// <summary>
    /// Gets or sets the Emby item identifier.
    /// </summary>
    public Guid ItemId { get; set; }
}

/// <summary>
/// AnimeThemes Sync management API.
/// </summary>
public class AnimeThemesSyncService : IService
{
    private readonly ThemeDownloader _themeDownloader;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeThemesSyncService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="logManager">The log manager.</param>
    /// <param name="mediaEncoder">The media encoder.</param>
    public AnimeThemesSyncService(
        ILibraryManager libraryManager,
        IFileSystem fileSystem,
        ILogManager logManager,
        IMediaEncoder mediaEncoder)
    {
        _themeDownloader = new ThemeDownloader(libraryManager, fileSystem, logManager, mediaEncoder);
    }

    /// <summary>
    /// Gets AnimeThemes-enabled library items.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The library items.</returns>
    public object Get(GetAnimeThemesItems request)
    {
        return _themeDownloader.GetBrowserItems();
    }

    /// <summary>
    /// Gets AnimeThemes Browser rows for one item.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The browser item result.</returns>
    public object Get(GetAnimeThemesForItem request)
    {
        try
        {
            return _themeDownloader.GetThemeBrowserItemAsync(request.ItemId, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
    }

    /// <summary>
    /// Downloads AnimeThemes media for one item.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The execution result.</returns>
    public object Post(DownloadAnimeThemesForItem request)
    {
        try
        {
            return _themeDownloader.DownloadItemByIdAsync(request.ItemId, request.Force, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
    }
}
