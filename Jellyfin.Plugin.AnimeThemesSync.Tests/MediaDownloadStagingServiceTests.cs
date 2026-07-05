using AnimeThemesSync.Shared.Services;
using MediaBrowser.Common.Configuration;
using Moq;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class MediaDownloadStagingServiceTests
{
    [Fact]
    public void JellyfinTempProvider_UsesApplicationPathsTempDirectory()
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(item => item.TempDirectory).Returns("/host/temp");

        var provider = new JellyfinAnimeThemesTempPathProvider(paths.Object);

        Assert.Equal("/host/temp", provider.GetTempDirectory());
    }

    [Fact]
    public void CreateWorkspace_UsesConfiguredPathBeforeHostTemp()
    {
        var configured = TemporaryDirectory();
        var host = TemporaryDirectory();
        try
        {
            string workingDirectory;
            using (var workspace = MediaDownloadStagingService.CreateWorkspace(configured, host))
            {
                workingDirectory = workspace.WorkingDirectory;
                Assert.StartsWith(Path.Combine(configured, "AnimeThemesSync", "downloads"), workingDirectory, StringComparison.OrdinalIgnoreCase);
                Assert.Null(workspace.Resolution.Warning);
                Assert.True(Directory.Exists(workingDirectory));
            }

            Assert.False(Directory.Exists(workingDirectory));
        }
        finally
        {
            DeleteDirectory(configured);
            DeleteDirectory(host);
        }
    }

    [Fact]
    public void CreateWorkspace_BlankConfigurationUsesHostTemp()
    {
        var host = TemporaryDirectory();
        try
        {
            using var workspace = MediaDownloadStagingService.CreateWorkspace(string.Empty, host);

            Assert.StartsWith(Path.Combine(host, "AnimeThemesSync", "downloads"), workspace.WorkingDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.Null(workspace.Resolution.Warning);
        }
        finally
        {
            DeleteDirectory(host);
        }
    }

    [Fact]
    public void CreateWorkspace_InvalidConfigurationFallsBackToHostTemp()
    {
        var host = TemporaryDirectory();
        try
        {
            using var workspace = MediaDownloadStagingService.CreateWorkspace("relative-path", host);

            Assert.StartsWith(Path.Combine(host, "AnimeThemesSync", "downloads"), workspace.WorkingDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(workspace.Resolution.Warning);
        }
        finally
        {
            DeleteDirectory(host);
        }
    }

    [Fact]
    public void CreateWorkspace_InvalidHostTempFallsBackToOperatingSystemTemp()
    {
        using var workspace = MediaDownloadStagingService.CreateWorkspace(null, "relative-host-temp");

        Assert.StartsWith(
            Path.Combine(Path.GetTempPath(), "AnimeThemesSync", "downloads"),
            workspace.WorkingDirectory,
            StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(workspace.Resolution.Warning);
    }

    [Fact]
    public void CreateWorkspace_UncConfigurationDoesNotOverrideHostTemp()
    {
        var host = TemporaryDirectory();
        try
        {
            using var workspace = MediaDownloadStagingService.CreateWorkspace(@"\\server\share\staging", host);

            Assert.StartsWith(Path.Combine(host, "AnimeThemesSync", "downloads"), workspace.WorkingDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(workspace.Resolution.Warning);
        }
        finally
        {
            DeleteDirectory(host);
        }
    }

    [Fact]
    public async Task PublishAsync_ReplacesDestinationAndReportsProgress()
    {
        var directory = TemporaryDirectory();
        var source = Path.Combine(directory, "source.bin");
        var destination = Path.Combine(directory, "output", "file.bin");
        var data = Enumerable.Range(0, 65536).Select(index => (byte)(index % 251)).ToArray();
        await File.WriteAllBytesAsync(source, data);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllTextAsync(destination, "old");
        var progress = new List<double>();

        try
        {
            await MediaDownloadStagingService.PublishAsync(
                source,
                destination,
                30,
                new InlineProgress(progress.Add),
                null,
                CancellationToken.None);

            Assert.Equal(data, await File.ReadAllBytesAsync(destination));
            Assert.Equal(1, progress[^1]);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(destination)!, "file.bin.part.*"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task PublishCoreAsync_RetriesIoFailuresWithShortDelays()
    {
        var operations = new FakeFileOperations { RemainingIoFailures = 2 };
        var timing = new FakePublishTiming();
        var retries = new List<(int Attempt, TimeSpan Delay)>();

        await MediaDownloadStagingService.PublishCoreAsync(
            "source.bin",
            Path.Combine(Path.GetTempPath(), "target", "file.bin"),
            TimeSpan.FromSeconds(30),
            null,
            (attempt, _, delay) => retries.Add((attempt, delay)),
            operations,
            timing,
            CancellationToken.None);

        Assert.Equal(3, operations.CopyAttempts);
        Assert.True(operations.Moved);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) }, timing.Delays);
        Assert.Equal(new[] { 1, 2 }, retries.Select(item => item.Attempt));
    }

    [Fact]
    public async Task PublishCoreAsync_DoesNotRetryAccessDeniedOrCancellation()
    {
        var deniedOperations = new FakeFileOperations { Failure = new UnauthorizedAccessException("denied") };
        var deniedTiming = new FakePublishTiming();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => MediaDownloadStagingService.PublishCoreAsync(
            "source.bin",
            Path.Combine(Path.GetTempPath(), "target", "file.bin"),
            TimeSpan.FromSeconds(30),
            null,
            null,
            deniedOperations,
            deniedTiming,
            CancellationToken.None));
        Assert.Empty(deniedTiming.Delays);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelledOperations = new FakeFileOperations();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MediaDownloadStagingService.PublishCoreAsync(
            "source.bin",
            Path.Combine(Path.GetTempPath(), "target", "file.bin"),
            TimeSpan.FromSeconds(30),
            null,
            null,
            cancelledOperations,
            new FakePublishTiming(),
            cancellation.Token));
        Assert.Equal(0, cancelledOperations.CopyAttempts);
    }

    [Fact]
    public void CleanupAbandonedWorkspaces_RemovesOldDirectoriesButKeepsActiveWorkspace()
    {
        var host = TemporaryDirectory();
        try
        {
            using var workspace = MediaDownloadStagingService.CreateWorkspace(null, host);
            var root = workspace.Resolution.RootPath;
            var stale = Path.Combine(root, "stale");
            Directory.CreateDirectory(stale);
            Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-25));
            Directory.SetLastWriteTimeUtc(workspace.WorkingDirectory, DateTime.UtcNow.AddHours(-25));

            MediaDownloadStagingService.CleanupAbandonedWorkspaces(root, DateTimeOffset.UtcNow.AddHours(-24));

            Assert.False(Directory.Exists(stale));
            Assert.True(Directory.Exists(workspace.WorkingDirectory));
        }
        finally
        {
            DeleteDirectory(host);
        }
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ats-staging-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private sealed class InlineProgress : IProgress<double>
    {
        private readonly Action<double> _report;

        public InlineProgress(Action<double> report)
        {
            _report = report;
        }

        public void Report(double value)
        {
            _report(value);
        }
    }

    private sealed class FakePublishTiming : MediaDownloadStagingService.IPublishTiming
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFileOperations : MediaDownloadStagingService.IStagingFileOperations
    {
        public int RemainingIoFailures { get; set; }

        public Exception? Failure { get; set; }

        public int CopyAttempts { get; private set; }

        public bool Moved { get; private set; }

        public void CreateDirectory(string path)
        {
        }

        public Task CopyAsync(string sourcePath, string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            CopyAttempts++;
            if (Failure != null)
            {
                throw Failure;
            }

            if (RemainingIoFailures-- > 0)
            {
                throw new IOException("network error");
            }

            progress?.Report(1);
            return Task.CompletedTask;
        }

        public long GetLength(string path)
        {
            return 1024;
        }

        public void MoveReplace(string sourcePath, string destinationPath)
        {
            Moved = true;
        }

        public void DeleteFile(string path)
        {
        }
    }
}
