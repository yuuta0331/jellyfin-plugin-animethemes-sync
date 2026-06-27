using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using Emby.Plugin.AnimeThemesSync.ScheduledTasks;
using MediaBrowser.Common.Configuration;
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

    public bool? IncludeAudio { get; set; }

    public bool? IncludeVideo { get; set; }

    public bool? IncludeExtras { get; set; }
}

/// <summary>
/// Request to list AnimeThemes-enabled library items.
/// </summary>
[Route("/AnimeThemesSync/Items", "GET", Summary = "Gets AnimeThemes-enabled library items.")]
public class GetAnimeThemesItems : IReturn<ThemeBrowserItemsPage>
{
    public string? LibraryId { get; set; }
    public int? StartIndex { get; set; }
    public int? Limit { get; set; }
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; }
    public string? SearchTerm { get; set; }
    public string? ItemType { get; set; }
    public string? LinkFilter { get; set; }
    public string? SavedFilter { get; set; }
}

[Route("/AnimeThemesSync/Storage", "GET", Summary = "Gets AnimeThemes Sync storage status.")]
public class GetAnimeThemesStorage : IReturn<AnimeThemesStorageStatus>
{
}

[Route("/AnimeThemesSync/BrowserCache/Rebuild", "POST", Summary = "Starts a Browser cache rebuild.")]
public class RebuildAnimeThemesBrowserCache : IReturn<AnimeThemesMaintenanceResult>
{
}

[Route("/AnimeThemesSync/BrowserCache/Clear", "POST", Summary = "Clears the Browser cache.")]
public class ClearAnimeThemesBrowserCache : IReturn<AnimeThemesMaintenanceResult>
{
}

[Route("/AnimeThemesSync/Extras/ImportLegacyManifests", "POST", Summary = "Imports legacy extras manifests.")]
public class ImportAnimeThemesLegacyExtrasManifests : IReturn<LegacyExtrasImportResult>
{
}

[Route("/AnimeThemesSync/Summary", "GET", Summary = "Gets AnimeThemes Browser local media summary.")]
public class GetAnimeThemesBrowserSummary : IReturn<ThemeBrowserSummary>
{
}

[Route("/AnimeThemesSync/SeasonMappings", "GET", Summary = "Gets AnimeThemes season mapping status.")]
public class GetAnimeThemesSeasonMappings : IReturn<IReadOnlyList<SeasonThemeMappingRow>>
{
}

[Route("/AnimeThemesSync/SeasonFinder", "GET", Summary = "Gets a cached page of Season Finder rows.")]
public class GetAnimeThemesSeasonFinder : IReturn<SeasonFinderItemsPage>
{
    public string? LibraryId { get; set; }

    public int? StartIndex { get; set; }

    public int? Limit { get; set; }

    public string? SearchTerm { get; set; }

    public string? Status { get; set; }

    public string? SortBy { get; set; }

    public string? SortOrder { get; set; }
}

[Route("/AnimeThemesSync/SeasonFinder/Rebuild", "POST", Summary = "Starts a Season Finder cache rebuild.")]
public class RebuildAnimeThemesSeasonFinder : IReturn<AnimeThemesMaintenanceResult>
{
}

[Route("/AnimeThemesSync/Search", "GET", Summary = "Searches AnimeThemes anime candidates.")]
public class SearchAnimeThemesAnime : IReturn<IReadOnlyList<ThemeFinderSearchResult>>
{
    public string Query { get; set; } = string.Empty;

    public int? Year { get; set; }
}

[Route("/AnimeThemesSync/Anime/{Slug}/Themes", "GET", Summary = "Gets AnimeThemes rows for a candidate anime.")]
public class GetAnimeThemesAnimePreview : IReturn<ThemeBrowserItemResult>
{
    public string Slug { get; set; } = string.Empty;
}

[Route("/AnimeThemesSync/SeasonMappings", "POST", Summary = "Saves an AnimeThemes season mapping.")]
public class SaveAnimeThemesSeasonMapping : IReturn<SeasonThemeMappingRow>
{
    public Guid SeasonItemId { get; set; }

    public string? AnimeThemesSlug { get; set; }

    public int? AniListId { get; set; }

    public int? MyAnimeListId { get; set; }

    public bool Locked { get; set; }
}

[Route("/AnimeThemesSync/SeasonMappings/Import", "POST", Summary = "Imports AnimeThemes season mappings.")]
public class ImportAnimeThemesSeasonMappings : IReturn<SeasonThemeMappingImportResult>
{
    public List<ImportSeasonThemeMappingRow> Mappings { get; set; } = [];
}

[Route("/AnimeThemesSync/SeasonMappings/{SeasonItemId}", "DELETE", Summary = "Deletes an AnimeThemes season mapping.")]
public class DeleteAnimeThemesSeasonMapping : IReturn<SeasonThemeMappingRow>
{
    public Guid SeasonItemId { get; set; }
}

[Route("/AnimeThemesSync/ThemeFiles/Delete", "POST", Summary = "Deletes local AnimeThemes files.")]
public class DeleteAnimeThemesFiles : IReturn<ThemeDeleteResult>
{
    public string Scope { get; set; } = "all";
}

[Route("/AnimeThemesSync/ThemeFiles/DeleteFile", "POST", Summary = "Deletes a specific local AnimeThemes file for an item.")]
public class DeleteAnimeThemesFile : IReturn<ThemeDeleteResult>
{
    public Guid ItemId { get; set; }
    public string RowId { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
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

    public bool? IncludeAudio { get; set; }

    public bool? IncludeVideo { get; set; }

    public bool? IncludeExtras { get; set; }
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
        IMediaEncoder mediaEncoder,
        IApplicationPaths applicationPaths)
    {
        _themeDownloader = new ThemeDownloader(libraryManager, fileSystem, logManager, mediaEncoder, applicationPaths);
    }

    /// <summary>
    /// Gets AnimeThemes-enabled library items.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The library items.</returns>
    public object Get(GetAnimeThemesItems request)
    {
        return _themeDownloader.GetBrowserItems(
            request.LibraryId,
            request.StartIndex,
            request.Limit,
            request.SortBy,
            request.SortOrder,
            request.SearchTerm,
            request.ItemType,
            request.LinkFilter,
            request.SavedFilter);
    }

    public object Get(GetAnimeThemesStorage request)
    {
        return _themeDownloader.GetStorageStatus();
    }

    public object Post(RebuildAnimeThemesBrowserCache request)
    {
        return _themeDownloader.StartBrowserCacheRebuild();
    }

    public object Post(ClearAnimeThemesBrowserCache request)
    {
        return _themeDownloader.ClearBrowserCache();
    }

    public object Post(ImportAnimeThemesLegacyExtrasManifests request)
    {
        return _themeDownloader.ImportLegacyExtrasManifests();
    }

    public object Get(GetAnimeThemesBrowserSummary request)
    {
        return _themeDownloader.GetBrowserSummary();
    }

    public object Get(GetAnimeThemesSeasonMappings request)
    {
        return _themeDownloader.GetSeasonThemeMappingsAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public object Get(GetAnimeThemesSeasonFinder request)
    {
        return _themeDownloader.GetSeasonFinderItems(
            request.LibraryId,
            request.StartIndex,
            request.Limit,
            request.SearchTerm,
            request.Status,
            request.SortBy,
            request.SortOrder);
    }

    public object Post(RebuildAnimeThemesSeasonFinder request)
    {
        return _themeDownloader.StartBrowserCacheRebuild();
    }

    public object Get(SearchAnimeThemesAnime request)
    {
        try
        {
            return _themeDownloader.SearchThemeFinderAnimeAsync(request.Query, request.Year, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
    }

    public object Get(GetAnimeThemesAnimePreview request)
    {
        try
        {
            return _themeDownloader.GetAnimeThemePreviewAsync(request.Slug, CancellationToken.None).GetAwaiter().GetResult();
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

    public object Post(SaveAnimeThemesSeasonMapping request)
    {
        try
        {
            return _themeDownloader.SaveSeasonThemeMappingAsync(
                new SaveSeasonThemeMappingRequest
                {
                    SeasonItemId = request.SeasonItemId,
                    AnimeThemesSlug = request.AnimeThemesSlug,
                    AniListId = request.AniListId,
                    MyAnimeListId = request.MyAnimeListId,
                    Locked = request.Locked,
                },
                CancellationToken.None).GetAwaiter().GetResult();
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

    public object Post(ImportAnimeThemesSeasonMappings request)
    {
        try
        {
            return _themeDownloader.ImportSeasonThemeMappingsAsync(
                new ImportSeasonThemeMappingsRequest
                {
                    Mappings = request.Mappings,
                },
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
    }

    public object Delete(DeleteAnimeThemesSeasonMapping request)
    {
        try
        {
            return _themeDownloader.DeleteSeasonThemeMappingAsync(request.SeasonItemId, CancellationToken.None).GetAwaiter().GetResult();
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

    public object Post(DeleteAnimeThemesFiles request)
    {
        try
        {
            return _themeDownloader.DeleteThemeFiles(request.Scope);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
    }

    public object Post(DeleteAnimeThemesFile request)
    {
        try
        {
            return _themeDownloader.DeleteIndividualThemeFileAsync(request.ItemId, request.RowId, request.Target, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (System.IO.FileNotFoundException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
        catch (KeyNotFoundException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message, nameof(request));
        }
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
            return _themeDownloader.DownloadThemeByRowIdAsync(
                request.ItemId,
                request.RowId,
                request.Force,
                null,
                request.IncludeAudio,
                request.IncludeVideo,
                request.IncludeExtras,
                CancellationToken.None).GetAwaiter().GetResult();
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
            (progress, cancellationToken) => _themeDownloader.DownloadThemeByRowIdAsync(
                request.ItemId,
                request.RowId,
                request.Force,
                progress,
                request.IncludeAudio,
                request.IncludeVideo,
                request.IncludeExtras,
                cancellationToken));
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
