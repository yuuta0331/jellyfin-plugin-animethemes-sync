using System.Net;
using System.Net.Http.Headers;
using AnimeThemesSync.Shared.Services;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class SegmentedDownloadServiceTests
{
    [Fact]
    public async Task DownloadAsync_CombinesValidatedRangeResponses()
    {
        var data = CreateData();
        using var client = new HttpClient(new RangeHandler(data));
        var path = TemporaryPath();
        var progressValues = new List<double>();

        try
        {
            await SegmentedDownloadService.DownloadAsync(
                client,
                "https://example.test/theme.webm",
                path,
                true,
                4,
                new CollectingProgress(progressValues),
                CancellationToken.None);

            Assert.Equal(data, await File.ReadAllBytesAsync(path));
            Assert.Equal(1, progressValues[^1]);
            AssertNoPartFiles(path);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_FallsBackWhenRangeProbeIsNotSupported()
    {
        var data = CreateData();
        using var client = new HttpClient(new StandardOnlyHandler(data));
        var path = TemporaryPath();

        try
        {
            await SegmentedDownloadService.DownloadAsync(client, "https://example.test/theme.webm", path, true, 4, null, CancellationToken.None);
            Assert.Equal(data, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_CleansPartsAndFallsBackWhenSegmentResponseIsInvalid()
    {
        var data = CreateData();
        using var client = new HttpClient(new InvalidSegmentHandler(data));
        var path = TemporaryPath();

        try
        {
            await SegmentedDownloadService.DownloadAsync(client, "https://example.test/theme.webm", path, true, 4, null, CancellationToken.None);
            Assert.Equal(data, await File.ReadAllBytesAsync(path));
            AssertNoPartFiles(path);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_FallsBackWhenContentRangeIsInvalid()
    {
        var data = CreateData();
        using var client = new HttpClient(new InvalidContentRangeHandler(data));
        var path = TemporaryPath();

        try
        {
            await SegmentedDownloadService.DownloadAsync(client, "https://example.test/theme.webm", path, true, 4, null, CancellationToken.None);
            Assert.Equal(data, await File.ReadAllBytesAsync(path));
            AssertNoPartFiles(path);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_FallsBackWhenSegmentLengthDoesNotMatch()
    {
        var data = CreateData();
        using var client = new HttpClient(new TruncatedSegmentHandler(data));
        var path = TemporaryPath();

        try
        {
            await SegmentedDownloadService.DownloadAsync(client, "https://example.test/theme.webm", path, true, 4, null, CancellationToken.None);
            Assert.Equal(data, await File.ReadAllBytesAsync(path));
            AssertNoPartFiles(path);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_CancellationDeletesDestinationAndPartFiles()
    {
        var data = CreateData();
        using var client = new HttpClient(new CancellingSegmentHandler(data));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var path = TemporaryPath();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                SegmentedDownloadService.DownloadAsync(client, "https://example.test/theme.webm", path, true, 4, null, cancellation.Token));
            Assert.False(File.Exists(path));
            AssertNoPartFiles(path);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_FallsBackImmediatelyOnTransientSegmentStatus()
    {
        var data = CreateData();
        var handler = new TransientSegmentStatusHandler(data);
        using var client = new HttpClient(handler);
        var path = TemporaryPath();

        try
        {
            await SegmentedDownloadService.DownloadAsync(client, "https://example.test/theme.webm", path, true, 4, null, CancellationToken.None);

            Assert.Equal(data, await File.ReadAllBytesAsync(path));
            Assert.Equal(1, handler.StandardRequests);
            Assert.InRange(handler.SegmentRequests, 1, 4);
            AssertNoPartFiles(path);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_FallsBackImmediatelyOnUnexpectedSegmentEof()
    {
        var data = CreateData();
        var handler = new TransientEofHandler(data);
        using var client = new HttpClient(handler);
        var path = TemporaryPath();

        try
        {
            await SegmentedDownloadService.DownloadAsync(client, "https://example.test/theme.webm", path, true, 4, null, CancellationToken.None);

            Assert.Equal(data, await File.ReadAllBytesAsync(path));
            Assert.Equal(1, handler.StandardRequests);
            Assert.True(handler.EofRaised);
            AssertNoPartFiles(path);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_FallsBackAfterTransientSegmentStatusIsExhausted()
    {
        var data = CreateData();
        var handler = new PersistentSegmentStatusHandler(data);
        using var client = new HttpClient(handler);
        var path = TemporaryPath();

        try
        {
            await SegmentedDownloadService.DownloadAsync(client, "https://example.test/theme.webm", path, true, 4, null, CancellationToken.None);

            Assert.Equal(data, await File.ReadAllBytesAsync(path));
            Assert.Equal(1, handler.StandardRequests);
            AssertNoPartFiles(path);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_RetriesTransientStandardStatusAndHonorsRetryAfter()
    {
        var data = CreateData();
        var handler = new TransientStandardStatusHandler(data, failures: 2);
        using var client = new HttpClient(handler);
        var path = TemporaryPath();

        try
        {
            await SegmentedDownloadService.DownloadAsync(client, "https://example.test/theme.webm", path, false, 4, null, CancellationToken.None);

            Assert.Equal(data, await File.ReadAllBytesAsync(path));
            Assert.Equal(3, handler.Requests);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_RetriesTransientStandardEof()
    {
        var data = CreateData();
        var handler = new TransientStandardEofHandler(data);
        using var client = new HttpClient(handler);
        var path = TemporaryPath();

        try
        {
            await SegmentedDownloadService.DownloadAsync(client, "https://example.test/theme.webm", path, false, 4, null, CancellationToken.None);

            Assert.Equal(data, await File.ReadAllBytesAsync(path));
            Assert.Equal(2, handler.Requests);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_LimitsRangeRequestsAcrossConcurrentFiles()
    {
        var data = CreateData();
        var handler = new ConcurrencyTrackingRangeHandler(data);
        using var client = new HttpClient(handler);
        var paths = Enumerable.Range(0, 3).Select(_ => TemporaryPath()).ToArray();

        try
        {
            await Task.WhenAll(paths.Select(path => SegmentedDownloadService.DownloadAsync(
                client,
                "https://example.test/theme.webm",
                path,
                true,
                4,
                SegmentedDownloadService.DefaultMinimumSegmentedBytes,
                4,
                MediaDownloadMode.Interactive,
                600,
                null,
                CancellationToken.None)));

            Assert.Equal(4, handler.MaximumConcurrentRangeRequests);
            foreach (var path in paths)
            {
                Assert.Equal(data, await File.ReadAllBytesAsync(path));
                AssertNoPartFiles(path);
            }
        }
        finally
        {
            foreach (var path in paths)
            {
                DeleteIfExists(path);
            }
        }
    }

    [Fact]
    public async Task DownloadAsync_DeletesDestinationWhenStandardRetriesAreExhausted()
    {
        var handler = new TransientStandardStatusHandler(CreateData(), failures: int.MaxValue);
        using var client = new HttpClient(handler);
        var path = TemporaryPath();

        try
        {
            await Assert.ThrowsAsync<MediaDownloadException>(() =>
                SegmentedDownloadService.DownloadAsync(client, "https://example.test/theme.webm", path, false, 4, null, CancellationToken.None));

            Assert.Equal(3, handler.Requests);
            Assert.False(File.Exists(path));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_DoesNotRetryNonTransientStandardStatus()
    {
        var handler = new StaticStatusHandler(HttpStatusCode.NotFound);
        using var client = new HttpClient(handler);
        var path = TemporaryPath();

        try
        {
            await Assert.ThrowsAsync<MediaDownloadException>(() =>
                SegmentedDownloadService.DownloadAsync(client, "https://example.test/missing.webm", path, false, 4, null, CancellationToken.None));

            Assert.Equal(1, handler.Requests);
            Assert.False(File.Exists(path));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_ScheduledModeUsesSixAttemptsAndLongBackoff()
    {
        SegmentedDownloadService.ResetHostCooldownsForTests();
        var data = CreateData();
        var handler = new ScheduledTransientStatusHandler(data, failures: 5, retryAfter: null);
        var timing = new FakeDownloadTiming();
        using var client = new HttpClient(handler);
        var path = TemporaryPath();

        try
        {
            await SegmentedDownloadService.DownloadCoreAsync(
                client,
                "https://v.animethemes.moe/theme.webm",
                path,
                false,
                2,
                1024,
                2,
                MediaDownloadMode.Scheduled,
                600,
                null,
                timing,
                CancellationToken.None);

            Assert.Equal(6, handler.Requests);
            Assert.Equal(5, timing.Delays.Count);
            Assert.InRange(timing.Delays[0], TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30));
            Assert.InRange(timing.Delays[4], TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(600));
        }
        finally
        {
            SegmentedDownloadService.ResetHostCooldownsForTests();
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DownloadAsync_ScheduledModeHonorsRetryAfterForHostCooldown()
    {
        SegmentedDownloadService.ResetHostCooldownsForTests();
        var data = CreateData();
        var handler = new ScheduledTransientStatusHandler(data, failures: 1, retryAfter: TimeSpan.FromMinutes(2));
        var timing = new FakeDownloadTiming();
        using var client = new HttpClient(handler);
        var path = TemporaryPath();

        try
        {
            await SegmentedDownloadService.DownloadCoreAsync(
                client,
                "https://v.animethemes.moe/theme.webm",
                path,
                false,
                2,
                1024,
                2,
                MediaDownloadMode.Scheduled,
                600,
                null,
                timing,
                CancellationToken.None);

            Assert.Equal(2, handler.Requests);
            Assert.Contains(TimeSpan.FromMinutes(2), timing.Delays);
        }
        finally
        {
            SegmentedDownloadService.ResetHostCooldownsForTests();
            DeleteIfExists(path);
        }
    }

    private static byte[] CreateData()
    {
        var data = new byte[SegmentedDownloadService.DefaultMinimumSegmentedBytes + 4096];
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = (byte)(index % 251);
        }

        return data;
    }

    private static string TemporaryPath()
    {
        return Path.Combine(Path.GetTempPath(), "animethemes-segment-test-" + Guid.NewGuid().ToString("N") + ".part");
    }

    private static void AssertNoPartFiles(string path)
    {
        for (var index = 0; index < 8; index++)
        {
            Assert.False(File.Exists(path + ".part" + index));
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        for (var index = 0; index < 8; index++)
        {
            if (File.Exists(path + ".part" + index))
            {
                File.Delete(path + ".part" + index);
            }
        }
    }

    private static HttpResponseMessage RangeResponse(byte[] data, long from, long to)
    {
        var length = checked((int)(to - from + 1));
        var content = new byte[length];
        Buffer.BlockCopy(data, checked((int)from), content, 0, length);
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(content),
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, to, data.LongLength);
        return response;
    }

    private sealed class CollectingProgress : IProgress<double>
    {
        private readonly List<double> _values;

        public CollectingProgress(List<double> values)
        {
            _values = values;
        }

        public void Report(double value)
        {
            _values.Add(value);
        }
    }

    private sealed class RangeHandler : HttpMessageHandler
    {
        private readonly byte[] _data;

        public RangeHandler(byte[] data)
        {
            _data = data;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.Single();
            return Task.FromResult(range == null
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) }
                : RangeResponse(_data, range.From!.Value, range.To!.Value));
        }
    }

    private sealed class StandardOnlyHandler : HttpMessageHandler
    {
        private readonly byte[] _data;

        public StandardOnlyHandler(byte[] data)
        {
            _data = data;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
        }
    }

    private sealed class InvalidSegmentHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        private int _rangeRequests;

        public InvalidSegmentHandler(byte[] data)
        {
            _data = data;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.Single();
            if (range == null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
            }

            if (Interlocked.Increment(ref _rangeRequests) == 1)
            {
                return Task.FromResult(RangeResponse(_data, 0, 0));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
        }
    }

    private sealed class InvalidContentRangeHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        private int _rangeRequests;

        public InvalidContentRangeHandler(byte[] data)
        {
            _data = data;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.Single();
            if (range == null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
            }

            if (Interlocked.Increment(ref _rangeRequests) == 1)
            {
                return Task.FromResult(RangeResponse(_data, 0, 0));
            }

            var response = RangeResponse(_data, range.From!.Value, range.To!.Value);
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(range.From.Value + 1, range.To.Value, _data.LongLength);
            return Task.FromResult(response);
        }
    }

    private sealed class TruncatedSegmentHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        private int _rangeRequests;

        public TruncatedSegmentHandler(byte[] data)
        {
            _data = data;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.Single();
            if (range == null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
            }

            if (Interlocked.Increment(ref _rangeRequests) == 1)
            {
                return Task.FromResult(RangeResponse(_data, 0, 0));
            }

            var from = range.From!.Value;
            var to = range.To!.Value;
            var expectedLength = checked((int)(to - from + 1));
            var content = new byte[Math.Max(0, expectedLength - 1)];
            Buffer.BlockCopy(_data, checked((int)from), content, 0, content.Length);
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent(content),
            };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, to, _data.LongLength);
            return Task.FromResult(response);
        }
    }

    private sealed class CancellingSegmentHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        private int _rangeRequests;

        public CancellingSegmentHandler(byte[] data)
        {
            _data = data;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.Single();
            if (range != null && Interlocked.Increment(ref _rangeRequests) == 1)
            {
                return RangeResponse(_data, 0, 0);
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation token should stop the request.");
        }
    }

    private sealed class TransientSegmentStatusHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        private int _probeCompleted;
        private int _transientReturned;

        public TransientSegmentStatusHandler(byte[] data)
        {
            _data = data;
        }

        public int SegmentRequests { get; private set; }

        public int StandardRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.Single();
            if (range == null)
            {
                StandardRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
            }

            if (Interlocked.Exchange(ref _probeCompleted, 1) == 0)
            {
                return Task.FromResult(RangeResponse(_data, 0, 0));
            }

            SegmentRequests++;
            if (Interlocked.Exchange(ref _transientReturned, 1) == 0)
            {
                var unavailable = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                unavailable.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return Task.FromResult(unavailable);
            }

            return Task.FromResult(RangeResponse(_data, range.From!.Value, range.To!.Value));
        }
    }

    private sealed class TransientEofHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        private int _probeCompleted;
        private int _eofRaised;

        public TransientEofHandler(byte[] data)
        {
            _data = data;
        }

        public bool EofRaised => Volatile.Read(ref _eofRaised) != 0;

        public int StandardRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.Single();
            if (range == null)
            {
                StandardRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
            }

            if (Interlocked.Exchange(ref _probeCompleted, 1) == 0)
            {
                return Task.FromResult(RangeResponse(_data, 0, 0));
            }

            if (range.From == 0 && Interlocked.Exchange(ref _eofRaised, 1) == 0)
            {
                throw new HttpRequestException("Received an unexpected EOF or 0 bytes from the transport stream.", new IOException("Unexpected EOF."));
            }

            return Task.FromResult(RangeResponse(_data, range.From!.Value, range.To!.Value));
        }
    }

    private sealed class PersistentSegmentStatusHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        private int _probeCompleted;

        public PersistentSegmentStatusHandler(byte[] data)
        {
            _data = data;
        }

        public int StandardRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.Single();
            if (range == null)
            {
                StandardRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
            }

            if (Interlocked.Exchange(ref _probeCompleted, 1) == 0)
            {
                return Task.FromResult(RangeResponse(_data, 0, 0));
            }

            var unavailable = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            unavailable.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(unavailable);
        }
    }

    private sealed class TransientStandardStatusHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        private readonly int _failures;

        public TransientStandardStatusHandler(byte[] data, int failures)
        {
            _data = data;
            _failures = failures;
        }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (Requests <= _failures)
            {
                var unavailable = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                unavailable.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return Task.FromResult(unavailable);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
        }
    }

    private sealed class TransientStandardEofHandler : HttpMessageHandler
    {
        private readonly byte[] _data;

        public TransientStandardEofHandler(byte[] data)
        {
            _data = data;
        }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (Requests == 1)
            {
                throw new HttpRequestException("Received an unexpected EOF or 0 bytes from the transport stream.", new IOException("Unexpected EOF."));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
        }
    }

    private sealed class ConcurrencyTrackingRangeHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        private int _activeRangeRequests;
        private int _maximumConcurrentRangeRequests;

        public ConcurrencyTrackingRangeHandler(byte[] data)
        {
            _data = data;
        }

        public int MaximumConcurrentRangeRequests => Volatile.Read(ref _maximumConcurrentRangeRequests);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.Single();
            if (range == null)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) };
            }

            var active = Interlocked.Increment(ref _activeRangeRequests);
            UpdateMaximum(active);
            try
            {
                await Task.Delay(50, cancellationToken);
                return RangeResponse(_data, range.From!.Value, range.To!.Value);
            }
            finally
            {
                _ = Interlocked.Decrement(ref _activeRangeRequests);
            }
        }

        private void UpdateMaximum(int value)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maximumConcurrentRangeRequests);
                if (value <= current || Interlocked.CompareExchange(ref _maximumConcurrentRangeRequests, value, current) == current)
                {
                    return;
                }
            }
        }
    }

    private sealed class StaticStatusHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public StaticStatusHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }

    private sealed class ScheduledTransientStatusHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        private readonly int _failures;
        private readonly TimeSpan? _retryAfter;

        public ScheduledTransientStatusHandler(byte[] data, int failures, TimeSpan? retryAfter)
        {
            _data = data;
            _failures = failures;
            _retryAfter = retryAfter;
        }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (Requests <= _failures)
            {
                var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                if (_retryAfter.HasValue)
                {
                    response.Headers.RetryAfter = new RetryConditionHeaderValue(_retryAfter.Value);
                }

                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
        }
    }

    private sealed class FakeDownloadTiming : SegmentedDownloadService.IDownloadTiming
    {
        private DateTimeOffset _now = new(2026, 7, 5, 0, 0, 0, TimeSpan.Zero);

        public List<TimeSpan> Delays { get; } = [];

        public Func<DateTimeOffset> UtcNow => () => _now;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            _now += delay;
            return Task.CompletedTask;
        }

        public int Next(int minimumInclusive, int maximumExclusive)
        {
            return minimumInclusive;
        }
    }
}
