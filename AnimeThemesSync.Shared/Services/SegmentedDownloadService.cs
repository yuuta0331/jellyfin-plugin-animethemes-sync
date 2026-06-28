using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeThemesSync.Shared.Services;

public static class SegmentedDownloadService
{
    public const long DefaultMinimumSegmentedBytes = 2 * 1024 * 1024;

    public static async Task DownloadAsync(
        HttpClient client,
        string url,
        string destinationPath,
        bool segmentedDownloadEnabled,
        int requestedSegmentCount,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
#if NETSTANDARD2_1
        if (client == null)
        {
            throw new ArgumentNullException(nameof(client));
        }
#else
        ArgumentNullException.ThrowIfNull(client);
#endif

        var progressState = new MonotonicProgress(progress);
        if (segmentedDownloadEnabled)
        {
            var contentLength = await ProbeRangeSupportAsync(client, url, cancellationToken).ConfigureAwait(false);
            if (contentLength > DefaultMinimumSegmentedBytes)
            {
                try
                {
                    var segmentCount = Math.Max(2, Math.Min(8, requestedSegmentCount));
                    await DownloadSegmentedAsync(
                        client,
                        url,
                        destinationPath,
                        contentLength.Value,
                        segmentCount,
                        progressState,
                        cancellationToken).ConfigureAwait(false);
                    progressState.Report(1);
                    return;
                }
                catch (OperationCanceledException)
                {
                    CleanupDownloadFiles(destinationPath, requestedSegmentCount);
                    throw;
                }
                catch (Exception)
                {
                    CleanupDownloadFiles(destinationPath, requestedSegmentCount);
                }
            }
        }

        await DownloadStandardAsync(client, url, destinationPath, progressState, cancellationToken).ConfigureAwait(false);
        progressState.Report(1);
    }

    private static async Task<long?> ProbeRangeSupportAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(0, 0);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.PartialContent)
        {
            return null;
        }

        var range = response.Content.Headers.ContentRange;
        if (range == null || !range.HasLength || range.From != 0 || range.To != 0 || range.Length <= 0)
        {
            return null;
        }

        return range.Length;
    }

    private static async Task DownloadStandardAsync(
        HttpClient client,
        string url,
        string destinationPath,
        MonotonicProgress progress,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, url);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var contentLength = response.Content.Headers.ContentLength;
        using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await CopyWithProgressAsync(input, output, contentLength, progress, cancellationToken).ConfigureAwait(false);

        if (contentLength.HasValue && output.Length != contentLength.Value)
        {
            throw new IOException($"Downloaded file length {output.Length} did not match the expected length {contentLength.Value}.");
        }
    }

    private static async Task DownloadSegmentedAsync(
        HttpClient client,
        string url,
        string destinationPath,
        long contentLength,
        int segmentCount,
        MonotonicProgress progress,
        CancellationToken cancellationToken)
    {
        var segmentSize = contentLength / segmentCount;
        var downloadedBytes = 0L;
        var partFiles = new List<string>(segmentCount);
        var tasks = new List<Task>(segmentCount);

        for (var index = 0; index < segmentCount; index++)
        {
            var start = index * segmentSize;
            var end = index == segmentCount - 1 ? contentLength - 1 : start + segmentSize - 1;
            var partPath = destinationPath + ".part" + index;
            partFiles.Add(partPath);
            tasks.Add(DownloadSegmentAsync(
                client,
                url,
                partPath,
                start,
                end,
                contentLength,
                bytesRead =>
                {
                    var total = Interlocked.Add(ref downloadedBytes, bytesRead);
                    progress.Report((double)total / contentLength);
                },
                cancellationToken));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
            using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            foreach (var partPath in partFiles)
            {
                using var input = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                await input.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
            }

            if (output.Length != contentLength)
            {
                throw new IOException($"Combined file length {output.Length} did not match the expected length {contentLength}.");
            }
        }
        finally
        {
            foreach (var partPath in partFiles)
            {
                TryDelete(partPath);
            }
        }
    }

    private static async Task DownloadSegmentAsync(
        HttpClient client,
        string url,
        string partPath,
        long start,
        long end,
        long totalLength,
        Action<int> reportBytes,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(start, end);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.PartialContent)
        {
            throw new HttpRequestException($"Range request returned {(int)response.StatusCode} instead of 206.");
        }

        var contentRange = response.Content.Headers.ContentRange;
        if (contentRange == null || contentRange.From != start || contentRange.To != end || contentRange.Length != totalLength)
        {
            throw new HttpRequestException("Range response contained an invalid Content-Range header.");
        }

        using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var output = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        var expectedLength = end - start + 1;
        var buffer = new byte[81920];
        long written = 0;
        while (true)
        {
#if NETSTANDARD2_1
            var read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
#else
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
#endif
            if (read == 0)
            {
                break;
            }

#if NETSTANDARD2_1
            await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
#else
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
#endif
            written += read;
            reportBytes(read);
        }

        if (written != expectedLength)
        {
            throw new IOException($"Segment length {written} did not match the expected length {expectedLength}.");
        }
    }

    private static async Task CopyWithProgressAsync(
        Stream input,
        Stream output,
        long? contentLength,
        MonotonicProgress progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long totalRead = 0;
        while (true)
        {
#if NETSTANDARD2_1
            var read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
#else
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
#endif
            if (read == 0)
            {
                break;
            }

#if NETSTANDARD2_1
            await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
#else
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
#endif
            totalRead += read;
            if (contentLength > 0)
            {
                progress.Report((double)totalRead / contentLength.Value);
            }
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, new Uri(url));
        request.Headers.TryAddWithoutValidation("User-Agent", Constants.UserAgent);
        return request;
    }

    private static void CleanupDownloadFiles(string destinationPath, int segmentCount)
    {
        TryDelete(destinationPath);
        for (var index = 0; index < Math.Max(2, Math.Min(8, segmentCount)); index++)
        {
            TryDelete(destinationPath + ".part" + index);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class MonotonicProgress
    {
        private readonly object _syncRoot = new();
        private readonly IProgress<double>? _progress;
        private double _lastValue;

        public MonotonicProgress(IProgress<double>? progress)
        {
            _progress = progress;
        }

        public void Report(double value)
        {
            lock (_syncRoot)
            {
                var clamped = Math.Max(0, Math.Min(1, value));
                if (clamped <= _lastValue)
                {
                    return;
                }

                _lastValue = clamped;
                _progress?.Report(clamped);
            }
        }
    }
}
