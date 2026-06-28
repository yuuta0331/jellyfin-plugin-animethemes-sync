using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Deletes a file with short retries for transient media-server locks.
/// </summary>
internal static class FileDeleteRetryService
{
    private static readonly int[] RetryDelaysMilliseconds = [250, 500, 1000, 2000];

    /// <summary>
    /// Runs a synchronous delete operation and converts persistent filesystem failures into controlled errors.
    /// </summary>
    internal static async Task DeleteAsync(
        Action deleteFile,
        string fileName,
        string hostName,
        Action<IOException, int, int, int>? onRetry,
        CancellationToken cancellationToken,
        Func<int, CancellationToken, Task>? delayAsync = null)
    {
#pragma warning disable CA1510 // The source also targets netstandard2.1, where ThrowIfNull is unavailable.
        if (deleteFile == null)
        {
            throw new ArgumentNullException(nameof(deleteFile));
        }
#pragma warning restore CA1510

        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                deleteFile();
                return;
            }
            catch (IOException ex) when (attempt < RetryDelaysMilliseconds.Length)
            {
                var retryDelay = RetryDelaysMilliseconds[attempt];
                onRetry?.Invoke(ex, retryDelay, attempt + 1, RetryDelaysMilliseconds.Length);
                var delay = delayAsync ?? ((milliseconds, token) => Task.Delay(milliseconds, token));
                await delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    $"The file is currently in use by {hostName} or another process. Stop playback and try again: {fileName}",
                    ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException(
                    $"The file could not be deleted because access was denied: {fileName}",
                    ex);
            }
        }
    }
}
