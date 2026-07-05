using System;
using System.IO;
using System.Net;
using System.Net.Http;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Classifies a failed scheduled download into a deferred-retry record.
/// Extracted from the host ThemeDownloader classes so both hosts share one
/// implementation.
/// </summary>
public static class DownloadFailureHelper
{
    /// <summary>
    /// Records the failure so later scheduled runs defer the download: 404/410
    /// become a 30-day permanent cooldown, transient provider errors honor the
    /// Retry-After hint (default 10 minutes), plain I/O, HTTP, and timeout errors
    /// get a 10-minute cooldown, and anything else is not recorded (it retries on
    /// the next run).
    /// </summary>
    public static void RecordScheduledFailure(AnimeThemesDataStore dataStore, string url, string destinationPath, Exception exception)
    {
        var now = DateTimeOffset.UtcNow;
        if (exception is MediaDownloadException mediaException)
        {
            var statusCode = mediaException.StatusCode.HasValue ? (int)mediaException.StatusCode.Value : (int?)null;
            if (mediaException.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                dataStore.RecordDownloadFailure(
                    url,
                    destinationPath,
                    DownloadFailureStatuses.PermanentFailed,
                    mediaException.Message,
                    statusCode,
                    now.AddDays(30));
            }
            else if (mediaException.IsTransient)
            {
                var delay = mediaException.RetryAfter ?? TimeSpan.FromMinutes(10);
                dataStore.RecordDownloadFailure(
                    url,
                    destinationPath,
                    DownloadFailureStatuses.TransientFailed,
                    mediaException.Message,
                    statusCode,
                    now.Add(delay));
            }

            return;
        }

        if (exception is IOException or HttpRequestException or TimeoutException)
        {
            dataStore.RecordDownloadFailure(
                url,
                destinationPath,
                DownloadFailureStatuses.TransientFailed,
                exception.Message,
                null,
                now.AddMinutes(10));
        }
    }
}
