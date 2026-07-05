using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeThemesSync.Shared.Services;

public enum MediaDownloadMode
{
    Interactive,
    Scheduled,
}

public sealed class MediaDownloadException : IOException
{
    public MediaDownloadException(
        string message,
        HttpStatusCode? statusCode,
        bool isTransient,
        TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        IsTransient = isTransient;
        RetryAfter = retryAfter;
    }

    public HttpStatusCode? StatusCode { get; }

    public bool IsTransient { get; }

    public TimeSpan? RetryAfter { get; }
}

public static class SegmentedDownloadService
{
    public const long DefaultMinimumSegmentedBytes = 25 * 1024 * 1024;
    public const int DefaultMaximumConcurrentRangeRequests = 2;
    private const int InteractiveMaximumAttempts = 3;
    private const int ScheduledMaximumAttempts = 6;
    private static readonly AdjustableConcurrencyLimiter RangeRequestLimiter = new();
    private static readonly ConcurrentDictionary<string, DateTimeOffset> HostCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object RandomLock = new();
    private static readonly Random Random = new();

    public static Task DownloadAsync(
        HttpClient client,
        string url,
        string destinationPath,
        bool segmentedDownloadEnabled,
        int requestedSegmentCount,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return DownloadAsync(
            client,
            url,
            destinationPath,
            segmentedDownloadEnabled,
            requestedSegmentCount,
            DefaultMinimumSegmentedBytes,
            DefaultMaximumConcurrentRangeRequests,
            MediaDownloadMode.Interactive,
            600,
            progress,
            cancellationToken);
    }

    public static Task DownloadAsync(
        HttpClient client,
        string url,
        string destinationPath,
        bool segmentedDownloadEnabled,
        int requestedSegmentCount,
        long minimumSegmentedBytes,
        int maximumConcurrentRangeRequests,
        MediaDownloadMode mode,
        int timeoutSeconds,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return DownloadCoreAsync(
            client,
            url,
            destinationPath,
            segmentedDownloadEnabled,
            requestedSegmentCount,
            minimumSegmentedBytes,
            maximumConcurrentRangeRequests,
            mode,
            timeoutSeconds,
            progress,
            SystemDownloadTiming.Instance,
            cancellationToken);
    }

    internal static async Task DownloadCoreAsync(
        HttpClient client,
        string url,
        string destinationPath,
        bool segmentedDownloadEnabled,
        int requestedSegmentCount,
        long minimumSegmentedBytes,
        int maximumConcurrentRangeRequests,
        MediaDownloadMode mode,
        int timeoutSeconds,
        IProgress<double>? progress,
        IDownloadTiming timing,
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
        var segmentCount = Math.Max(2, Math.Min(8, requestedSegmentCount));
        var minimumBytes = Math.Max(1, minimumSegmentedBytes);
        var rangeLimit = Math.Max(1, Math.Min(16, maximumConcurrentRangeRequests));
        var requestTimeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));

        if (segmentedDownloadEnabled)
        {
            var contentLength = await ProbeRangeSupportAsync(client, url, rangeLimit, requestTimeout, cancellationToken).ConfigureAwait(false);
            if (contentLength >= minimumBytes)
            {
                var completed = await DownloadSegmentedAsync(
                    client,
                    url,
                    destinationPath,
                    contentLength.Value,
                    segmentCount,
                    rangeLimit,
                    requestTimeout,
                    progressState,
                    cancellationToken).ConfigureAwait(false);
                if (completed)
                {
                    progressState.Report(1);
                    return;
                }

                CleanupDownloadFiles(destinationPath, segmentCount);
            }
        }

        await DownloadStandardAsync(
            client,
            url,
            destinationPath,
            mode,
            requestTimeout,
            progressState,
            timing,
            cancellationToken).ConfigureAwait(false);
        progressState.Report(1);
    }

    internal static void ResetHostCooldownsForTests()
    {
        HostCooldowns.Clear();
    }

    private static async Task<long?> ProbeRangeSupportAsync(
        HttpClient client,
        string url,
        int rangeLimit,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken)
    {
        using (await RangeRequestLimiter.AcquireAsync(rangeLimit, cancellationToken).ConfigureAwait(false))
        using (var timeout = CreateRequestTimeout(requestTimeout, cancellationToken))
        {
            try
            {
                using var request = CreateRequest(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(0, 0);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.PartialContent)
                {
                    return null;
                }

                var range = response.Content.Headers.ContentRange;
                return range != null && range.HasLength && range.From == 0 && range.To == 0 && range.Length > 0
                    ? range.Length
                    : null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex) when (IsTransientException(ex))
            {
                return null;
            }
        }
    }

    private static async Task DownloadStandardAsync(
        HttpClient client,
        string url,
        string destinationPath,
        MediaDownloadMode mode,
        TimeSpan requestTimeout,
        MonotonicProgress progress,
        IDownloadTiming timing,
        CancellationToken cancellationToken)
    {
        var maximumAttempts = mode == MediaDownloadMode.Scheduled ? ScheduledMaximumAttempts : InteractiveMaximumAttempts;
        Exception? lastException = null;
        HttpStatusCode? lastStatusCode = null;
        string? lastReasonPhrase = null;
        TimeSpan? lastRetryDelay = null;
        var host = GetHost(url);

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (mode == MediaDownloadMode.Scheduled)
            {
                await WaitForHostCooldownAsync(host, timing, cancellationToken).ConfigureAwait(false);
            }

            var retryDelay = GetDefaultRetryDelay(mode, attempt, timing);
            try
            {
                using var timeout = CreateRequestTimeout(requestTimeout, cancellationToken);
                using var request = CreateRequest(HttpMethod.Get, url);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var responseDelay = GetRetryAfter(response, timing.UtcNow);
                    retryDelay = responseDelay ?? retryDelay;
                    retryDelay = ClampRetryDelay(retryDelay, responseDelay.HasValue);
                    if (!IsTransientStatusCode(response.StatusCode))
                    {
                        throw new MediaDownloadException(
                            $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase ?? response.StatusCode.ToString()}).",
                            response.StatusCode,
                            false,
                            responseDelay);
                    }

                    lastException = null;
                    lastStatusCode = response.StatusCode;
                    lastReasonPhrase = response.ReasonPhrase;
                    lastRetryDelay = retryDelay;
                    if (mode == MediaDownloadMode.Scheduled && IsCooldownStatus(response.StatusCode))
                    {
                        ApplyHostCooldown(host, retryDelay, timing.UtcNow);
                    }
                }
                else
                {
                    var contentLength = response.Content.Headers.ContentLength;
                    using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                    await CopyWithProgressAsync(input, output, contentLength, progress, timeout.Token).ConfigureAwait(false);

                    if (contentLength.HasValue && output.Length != contentLength.Value)
                    {
                        throw new IOException($"Downloaded file length {output.Length} did not match the expected length {contentLength.Value}.");
                    }

                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryDelete(destinationPath);
                throw;
            }
            catch (OperationCanceledException ex)
            {
                lastException = new TimeoutException($"The media request did not complete within {requestTimeout.TotalSeconds:0} seconds.", ex);
                lastStatusCode = null;
                lastReasonPhrase = null;
                lastRetryDelay = retryDelay;
                TryDelete(destinationPath);
            }
            catch (MediaDownloadException)
            {
                TryDelete(destinationPath);
                throw;
            }
            catch (Exception ex) when (IsTransientException(ex))
            {
                lastException = ex;
                lastStatusCode = null;
                lastReasonPhrase = null;
                lastRetryDelay = retryDelay;
                TryDelete(destinationPath);
            }
            catch
            {
                TryDelete(destinationPath);
                throw;
            }

            TryDelete(destinationPath);
            if (attempt < maximumAttempts)
            {
                if (!(mode == MediaDownloadMode.Scheduled && lastStatusCode.HasValue && IsCooldownStatus(lastStatusCode.Value)))
                {
                    await timing.DelayAsync(retryDelay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        TryDelete(destinationPath);
        var message = lastException?.Message ??
            $"Response status code does not indicate success: {(int?)lastStatusCode ?? 0} ({lastReasonPhrase ?? lastStatusCode?.ToString() ?? "Unknown"}).";
        throw new MediaDownloadException(message, lastStatusCode, true, lastRetryDelay, lastException);
    }

    private static async Task<bool> DownloadSegmentedAsync(
        HttpClient client,
        string url,
        string destinationPath,
        long contentLength,
        int segmentCount,
        int rangeLimit,
        TimeSpan requestTimeout,
        MonotonicProgress progress,
        CancellationToken cancellationToken)
    {
        var segmentSize = contentLength / segmentCount;
        var downloadedBytes = 0L;
        var segmentFailed = 0;
        var partFiles = new List<string>(segmentCount);
        var tasks = new List<Task<bool>>(segmentCount);

        for (var index = 0; index < segmentCount; index++)
        {
            var start = index * segmentSize;
            var end = index == segmentCount - 1 ? contentLength - 1 : start + segmentSize - 1;
            var partPath = destinationPath + ".part" + index;
            partFiles.Add(partPath);
            tasks.Add(DownloadOneSegmentAsync(partPath, start, end));
        }

        try
        {
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref segmentFailed) != 0 || Array.Exists(results, result => !result))
            {
                return false;
            }

            using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            foreach (var partPath in partFiles)
            {
                using var input = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                await input.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
            }

            return output.Length == contentLength;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            TryDelete(destinationPath);
            return false;
        }
        finally
        {
            foreach (var partPath in partFiles)
            {
                TryDelete(partPath);
            }
        }

        async Task<bool> DownloadOneSegmentAsync(string partPath, long start, long end)
        {
            if (Volatile.Read(ref segmentFailed) != 0)
            {
                return false;
            }

            var completed = await DownloadSegmentAsync(
                client,
                url,
                partPath,
                start,
                end,
                contentLength,
                rangeLimit,
                requestTimeout,
                () => Volatile.Read(ref segmentFailed) != 0,
                bytesRead =>
                {
                    var total = Interlocked.Add(ref downloadedBytes, bytesRead);
                    progress.Report((double)total / contentLength);
                },
                cancellationToken).ConfigureAwait(false);
            if (!completed)
            {
                _ = Interlocked.Exchange(ref segmentFailed, 1);
            }

            return completed;
        }
    }

    private static async Task<bool> DownloadSegmentAsync(
        HttpClient client,
        string url,
        string partPath,
        long start,
        long end,
        long totalLength,
        int rangeLimit,
        TimeSpan requestTimeout,
        Func<bool> siblingFailed,
        Action<int> reportBytes,
        CancellationToken cancellationToken)
    {
        if (siblingFailed())
        {
            return false;
        }

        using (await RangeRequestLimiter.AcquireAsync(rangeLimit, cancellationToken).ConfigureAwait(false))
        {
            if (siblingFailed())
            {
                return false;
            }

            using var timeout = CreateRequestTimeout(requestTimeout, cancellationToken);
            try
            {
                using var request = CreateRequest(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(start, end);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.PartialContent)
                {
                    return false;
                }

                var contentRange = response.Content.Headers.ContentRange;
                if (contentRange == null || contentRange.From != start || contentRange.To != end || contentRange.Length != totalLength)
                {
                    return false;
                }

                using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var output = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                var expectedLength = end - start + 1;
                var buffer = new byte[81920];
                long written = 0;
                while (written < expectedLength)
                {
#if NETSTANDARD2_1
                    var read = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false);
#else
                    var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), timeout.Token).ConfigureAwait(false);
#endif
                    if (read == 0)
                    {
                        break;
                    }

#if NETSTANDARD2_1
                    await output.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false);
#else
                    await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
#endif
                    written += read;
                    reportBytes(read);
                }

                return written == expectedLength;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex) when (IsTransientException(ex))
            {
                return false;
            }
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

    private static CancellationTokenSource CreateRequestTimeout(TimeSpan requestTimeout, CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);
        return timeout;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, new Uri(url));
        request.Headers.TryAddWithoutValidation("User-Agent", Constants.UserAgent);
        return request;
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        var numericStatus = (int)statusCode;
        return statusCode == HttpStatusCode.RequestTimeout || numericStatus == 429 || (numericStatus >= 500 && numericStatus <= 599);
    }

    private static bool IsCooldownStatus(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.ServiceUnavailable || (int)statusCode == 429;
    }

    private static bool IsTransientException(Exception exception)
    {
        return exception is HttpRequestException || exception is IOException || exception is TimeoutException;
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response, Func<DateTimeOffset> utcNow)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (retryAfter?.Date is DateTimeOffset date)
        {
            var delay = date - utcNow();
            return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }

        return null;
    }

    private static TimeSpan ClampRetryDelay(TimeSpan delay, bool fromRetryAfter)
    {
        var maximum = fromRetryAfter ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(10);
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay > maximum ? maximum : delay;
    }

    private static TimeSpan GetDefaultRetryDelay(MediaDownloadMode mode, int attempt, IDownloadTiming timing)
    {
        if (mode == MediaDownloadMode.Interactive)
        {
            return attempt <= 1 ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(5);
        }

        var ranges = new[] { (15, 30), (30, 60), (60, 120), (180, 300), (300, 600) };
        var index = Math.Max(0, Math.Min(ranges.Length - 1, attempt - 1));
        return TimeSpan.FromSeconds(timing.Next(ranges[index].Item1, ranges[index].Item2 + 1));
    }

    private static string GetHost(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
    }

    private static async Task WaitForHostCooldownAsync(string host, IDownloadTiming timing, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        while (HostCooldowns.TryGetValue(host, out var until))
        {
            var delay = until - timing.UtcNow();
            if (delay <= TimeSpan.Zero)
            {
                _ = HostCooldowns.TryRemove(host, out _);
                return;
            }

            await timing.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ApplyHostCooldown(string host, TimeSpan delay, Func<DateTimeOffset> utcNow)
    {
        if (string.IsNullOrWhiteSpace(host) || delay <= TimeSpan.Zero)
        {
            return;
        }

        var until = utcNow() + delay;
        _ = HostCooldowns.AddOrUpdate(host, until, (_, current) => current > until ? current : until);
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

    internal interface IDownloadTiming
    {
        Func<DateTimeOffset> UtcNow { get; }

        Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);

        int Next(int minimumInclusive, int maximumExclusive);
    }

    private sealed class SystemDownloadTiming : IDownloadTiming
    {
        public static SystemDownloadTiming Instance { get; } = new();

        public Func<DateTimeOffset> UtcNow => () => DateTimeOffset.UtcNow;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay, cancellationToken);
        }

        public int Next(int minimumInclusive, int maximumExclusive)
        {
            lock (RandomLock)
            {
                return Random.Next(minimumInclusive, maximumExclusive);
            }
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
