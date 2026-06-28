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
}
