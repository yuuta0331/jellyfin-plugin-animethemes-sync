using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnimeThemesSync.Shared.Models;
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
}
