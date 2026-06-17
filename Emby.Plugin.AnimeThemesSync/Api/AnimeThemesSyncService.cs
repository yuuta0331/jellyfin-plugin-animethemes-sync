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
