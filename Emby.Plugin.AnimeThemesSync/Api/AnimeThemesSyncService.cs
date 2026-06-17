using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
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
/// Request to download one AnimeThemes Browser row for one item.
/// </summary>
[Route("/AnimeThemesSync/Items/{ItemId}/Themes/{RowId}/Download", "POST", Summary = "Downloads one AnimeThemes Browser row for one item.")]
public class DownloadAnimeThemesThemeRow : IReturn<ThemeDownloadExecutionResult>
{
    /// <summary>
    /// Gets or sets the Emby item identifier.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the Browser row identifier.
    /// </summary>
    public string RowId { get; set; } = string.Empty;

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
/// Request to stream local media for one saved AnimeThemes Browser row.
/// </summary>
[Route("/AnimeThemesSync/Items/{ItemId}/Themes/{RowId}/LocalMedia", "GET", Summary = "Streams local AnimeThemes media for one Browser row.")]
public class GetAnimeThemesLocalMedia : IReturn<Stream>
{
    /// <summary>
    /// Gets or sets the Emby item identifier.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the Browser row identifier.
    /// </summary>
    public string RowId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local target: video, audio, or extra.
    /// </summary>
    public string Target { get; set; } = string.Empty;
}

[Route("/AnimeThemesSync/Jobs/ItemDownload", "POST", Summary = "Starts an AnimeThemes item download job.")]
public class StartAnimeThemesItemDownloadJob : IReturn<ThemeDownloadJobStartResult>
{
    public Guid ItemId { get; set; }

    public bool Force { get; set; }
}

[Route("/AnimeThemesSync/Jobs/ThemeDownload", "POST", Summary = "Starts an AnimeThemes theme download job.")]
public class StartAnimeThemesThemeDownloadJob : IReturn<ThemeDownloadJobStartResult>
{
    public Guid ItemId { get; set; }

    public string RowId { get; set; } = string.Empty;

    public bool Force { get; set; }
}

[Route("/AnimeThemesSync/Jobs/{JobId}", "GET", Summary = "Gets an AnimeThemes download job status.")]
public class GetAnimeThemesDownloadJob : IReturn<ThemeDownloadJobStatus>
{
    public string JobId { get; set; } = string.Empty;
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
        catch (KeyNotFoundException ex)
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

    /// <summary>
    /// Downloads one AnimeThemes Browser row for one item.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The execution result.</returns>
    public object Post(DownloadAnimeThemesThemeRow request)
    {
        try
        {
            return _themeDownloader.DownloadThemeByRowIdAsync(request.ItemId, request.RowId, request.Force, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
    }

    public object Post(StartAnimeThemesItemDownloadJob request)
    {
        return ThemeDownloadJobService.Start(
            "Downloading item themes...",
            (progress, cancellationToken) => _themeDownloader.DownloadItemByIdAsync(request.ItemId, request.Force, progress, cancellationToken));
    }

    public object Post(StartAnimeThemesThemeDownloadJob request)
    {
        return ThemeDownloadJobService.Start(
            "Downloading theme...",
            (progress, cancellationToken) => _themeDownloader.DownloadThemeByRowIdAsync(request.ItemId, request.RowId, request.Force, progress, cancellationToken));
    }

    public object Get(GetAnimeThemesDownloadJob request)
    {
        return ThemeDownloadJobService.Get(request.JobId)
            ?? throw new ArgumentException("The requested download job was not found.", nameof(request));
    }

    /// <summary>
    /// Streams saved local AnimeThemes media for one Browser row.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The local media stream.</returns>
    public object Get(GetAnimeThemesLocalMedia request)
    {
        try
        {
            var media = _themeDownloader.GetLocalThemeMediaAsync(request.ItemId, request.RowId, request.Target, CancellationToken.None).GetAwaiter().GetResult();
            return new FileStream(media.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (FileNotFoundException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
        catch (KeyNotFoundException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
    }
}
