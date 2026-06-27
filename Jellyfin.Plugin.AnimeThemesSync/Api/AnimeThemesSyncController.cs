using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using Jellyfin.Plugin.AnimeThemesSync.ScheduledTasks;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AnimeThemesSync.Api;

/// <summary>
/// AnimeThemes Sync management API.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("AnimeThemesSync")]
public sealed class AnimeThemesSyncController : ControllerBase
{
    private readonly ThemeDownloader _themeDownloader;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeThemesSyncController"/> class.
    /// </summary>
    /// <param name="themeDownloader">The theme downloader.</param>
    public AnimeThemesSyncController(ThemeDownloader themeDownloader)
    {
        _themeDownloader = themeDownloader;
    }

    /// <summary>
    /// Gets AnimeThemes-enabled library items.
    /// </summary>
    /// <returns>The library items.</returns>
    [HttpGet("Items")]
    [ProducesResponseType(typeof(ThemeBrowserItemsPage), StatusCodes.Status200OK)]
    public ActionResult<ThemeBrowserItemsPage> GetItems(
        [FromQuery] string? libraryId,
        [FromQuery] int? startIndex,
        [FromQuery] int? limit,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder,
        [FromQuery] string? searchTerm,
        [FromQuery] string? itemType,
        [FromQuery] string? linkFilter,
        [FromQuery] string? savedFilter)
    {
        return Ok(_themeDownloader.GetBrowserItems(libraryId, startIndex, limit, sortBy, sortOrder, searchTerm, itemType, linkFilter, savedFilter));
    }

    [HttpGet("Storage")]
    [ProducesResponseType(typeof(AnimeThemesStorageStatus), StatusCodes.Status200OK)]
    public ActionResult<AnimeThemesStorageStatus> GetStorage()
    {
        return Ok(_themeDownloader.GetStorageStatus());
    }

    [HttpPost("BrowserCache/Rebuild")]
    [ProducesResponseType(typeof(AnimeThemesMaintenanceResult), StatusCodes.Status200OK)]
    public ActionResult<AnimeThemesMaintenanceResult> RebuildBrowserCache()
    {
        return Ok(_themeDownloader.StartBrowserCacheRebuild());
    }

    [HttpPost("BrowserCache/Clear")]
    [ProducesResponseType(typeof(AnimeThemesMaintenanceResult), StatusCodes.Status200OK)]
    public ActionResult<AnimeThemesMaintenanceResult> ClearBrowserCache()
    {
        return Ok(_themeDownloader.ClearBrowserCache());
    }

    [HttpPost("Extras/ImportLegacyManifests")]
    [ProducesResponseType(typeof(LegacyExtrasImportResult), StatusCodes.Status200OK)]
    public ActionResult<LegacyExtrasImportResult> ImportLegacyExtrasManifests()
    {
        return Ok(_themeDownloader.ImportLegacyExtrasManifests());
    }

    [HttpGet("Summary")]
    [ProducesResponseType(typeof(ThemeBrowserSummary), StatusCodes.Status200OK)]
    public ActionResult<ThemeBrowserSummary> GetSummary()
    {
        return Ok(_themeDownloader.GetBrowserSummary());
    }

    [HttpGet("SeasonMappings")]
    [ProducesResponseType(typeof(IReadOnlyList<SeasonThemeMappingRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SeasonThemeMappingRow>>> GetSeasonMappings(CancellationToken cancellationToken)
    {
        return Ok(await _themeDownloader.GetSeasonThemeMappingsAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("SeasonFinder")]
    [ProducesResponseType(typeof(SeasonFinderItemsPage), StatusCodes.Status200OK)]
    public ActionResult<SeasonFinderItemsPage> GetSeasonFinder(
        [FromQuery] string? libraryId,
        [FromQuery] int? startIndex,
        [FromQuery] int? limit,
        [FromQuery] string? searchTerm,
        [FromQuery] string? status,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder)
    {
        return Ok(_themeDownloader.GetSeasonFinderItems(libraryId, startIndex, limit, searchTerm, status, sortBy, sortOrder));
    }

    [HttpPost("SeasonFinder/Rebuild")]
    [ProducesResponseType(typeof(AnimeThemesMaintenanceResult), StatusCodes.Status200OK)]
    public ActionResult<AnimeThemesMaintenanceResult> RebuildSeasonFinder()
    {
        return Ok(_themeDownloader.StartBrowserCacheRebuild());
    }

    [HttpGet("Search")]
    [ProducesResponseType(typeof(IReadOnlyList<ThemeFinderSearchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ThemeFinderSearchResult>>> SearchAnime(
        [FromQuery] string query,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Search query is required." });
        }

        return Ok(await _themeDownloader.SearchThemeFinderAnimeAsync(query, year, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("Anime/{slug}/Themes")]
    [ProducesResponseType(typeof(ThemeBrowserItemResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ThemeBrowserItemResult>> GetAnimeThemes(string slug, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _themeDownloader.GetAnimeThemePreviewAsync(slug, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("SeasonMappings")]
    [ProducesResponseType(typeof(SeasonThemeMappingRow), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonThemeMappingRow>> SaveSeasonMapping(
        [FromBody] SaveSeasonThemeMappingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _themeDownloader.SaveSeasonThemeMappingAsync(request, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("SeasonMappings/Import")]
    [ProducesResponseType(typeof(SeasonThemeMappingImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeasonThemeMappingImportResult>> ImportSeasonMappings(
        [FromBody] ImportSeasonThemeMappingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _themeDownloader.ImportSeasonThemeMappingsAsync(request, cancellationToken).ConfigureAwait(false));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("SeasonMappings/{seasonItemId:guid}")]
    [ProducesResponseType(typeof(SeasonThemeMappingRow), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonThemeMappingRow>> DeleteSeasonMapping(Guid seasonItemId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _themeDownloader.DeleteSeasonThemeMappingAsync(seasonItemId, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("ThemeFiles/Delete")]
    [ProducesResponseType(typeof(ThemeDeleteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ThemeDeleteResult> DeleteThemeFiles([FromQuery] string scope)
    {
        try
        {
            return Ok(_themeDownloader.DeleteThemeFiles(scope));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("ThemeFiles/DeleteFile")]
    [ProducesResponseType(typeof(ThemeDeleteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ThemeDeleteResult>> DeleteIndividualThemeFile(
        [FromQuery] Guid itemId,
        [FromQuery] string rowId,
        [FromQuery] string target,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _themeDownloader.DeleteIndividualThemeFileAsync(itemId, rowId, target, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (System.IO.FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets AnimeThemes Browser rows for one item.
    /// </summary>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The browser item result.</returns>
    [HttpGet("Items/{itemId:guid}/Themes")]
    [ProducesResponseType(typeof(ThemeBrowserItemResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ThemeBrowserItemResult>> GetThemes(Guid itemId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _themeDownloader.GetThemeBrowserItemAsync(itemId, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Downloads AnimeThemes media for one item.
    /// </summary>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    /// <param name="force">Whether existing files should be replaced.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The execution result.</returns>
    [HttpPost("Items/{itemId:guid}/DownloadThemes")]
    [ProducesResponseType(typeof(ThemeDownloadExecutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ThemeDownloadExecutionResult>> DownloadThemes(
        Guid itemId,
        [FromQuery] bool force,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _themeDownloader.DownloadItemByIdAsync(itemId, force, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("Jobs/ItemDownload")]
    [ProducesResponseType(typeof(ThemeDownloadJobStartResult), StatusCodes.Status200OK)]
    public ActionResult<ThemeDownloadJobStartResult> StartItemDownloadJob(
        [FromQuery] Guid itemId,
        [FromQuery] bool force)
    {
        var job = ThemeDownloadJobService.Start(
            "Downloading item themes...",
            (progress, cancellationToken) => _themeDownloader.DownloadItemByIdAsync(itemId, force, progress, cancellationToken));
        return Ok(job);
    }

    [HttpPost("Jobs/ThemeDownload")]
    [ProducesResponseType(typeof(ThemeDownloadJobStartResult), StatusCodes.Status200OK)]
    public ActionResult<ThemeDownloadJobStartResult> StartThemeDownloadJob(
        [FromQuery] Guid itemId,
        [FromQuery] string rowId,
        [FromQuery] bool force,
        [FromQuery] bool? includeAudio,
        [FromQuery] bool? includeVideo,
        [FromQuery] bool? includeExtras)
    {
        var job = ThemeDownloadJobService.Start(
            "Downloading theme...",
            (progress, cancellationToken) => _themeDownloader.DownloadThemeByRowIdAsync(
                itemId,
                rowId,
                force,
                progress,
                includeAudio,
                includeVideo,
                includeExtras,
                cancellationToken));
        return Ok(job);
    }

    [HttpGet("Jobs/{jobId}")]
    [ProducesResponseType(typeof(ThemeDownloadJobStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ThemeDownloadJobStatus> GetDownloadJob(string jobId)
    {
        var status = ThemeDownloadJobService.Get(jobId);
        return status == null ? NotFound() : Ok(status);
    }

    /// <summary>
    /// Streams local media for one saved AnimeThemes browser row.
    /// </summary>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    /// <param name="rowId">The Browser row identifier.</param>
    /// <param name="target">The local target: video, audio, or extra.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The local media file.</returns>
    [HttpGet("Items/{itemId:guid}/Themes/{rowId}/LocalMedia")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLocalMedia(
        Guid itemId,
        string rowId,
        [FromQuery] string target,
        CancellationToken cancellationToken)
    {
        try
        {
            var media = await _themeDownloader.GetLocalThemeMediaAsync(itemId, rowId, target, cancellationToken).ConfigureAwait(false);
            return PhysicalFile(media.Path, media.ContentType, enableRangeProcessing: true);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
