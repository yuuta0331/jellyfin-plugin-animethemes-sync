using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeThemesSync.Shared.Services;

public sealed record MediaStagingResolution(string RootPath, string? Warning);

public sealed class MediaStagingWorkspace : IDisposable
{
    private string? _workingDirectory;

    internal MediaStagingWorkspace(string workingDirectory, MediaStagingResolution resolution)
    {
        _workingDirectory = workingDirectory;
        Resolution = resolution;
    }

    public string WorkingDirectory => _workingDirectory ?? throw new ObjectDisposedException(nameof(MediaStagingWorkspace));

    public MediaStagingResolution Resolution { get; }

    public void Dispose()
    {
        var directory = Interlocked.Exchange(ref _workingDirectory, null);
        if (directory != null)
        {
            MediaDownloadStagingService.CleanupWorkspace(directory);
        }
    }
}

public static class MediaDownloadStagingService
{
    private const int PublishAttempts = 3;
    private const int CopyBufferSize = 1024 * 1024;
    private static readonly ConcurrentDictionary<string, byte> ActiveWorkspaces = new(StringComparer.OrdinalIgnoreCase);

    public static MediaStagingWorkspace CreateWorkspace(string? configuredDirectory, string? hostTempDirectory)
    {
        var resolution = ResolveRoot(configuredDirectory, hostTempDirectory);
        CleanupAbandonedWorkspaces(resolution.RootPath, DateTimeOffset.UtcNow.AddHours(-24));
        var workingDirectory = Path.Combine(resolution.RootPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        _ = ActiveWorkspaces.TryAdd(Path.GetFullPath(workingDirectory), 0);
        return new MediaStagingWorkspace(workingDirectory, resolution);
    }

    public static Task PublishAsync(
        string sourcePath,
        string destinationPath,
        int timeoutSeconds,
        IProgress<double>? progress,
        Action<int, Exception, TimeSpan>? retrying,
        CancellationToken cancellationToken)
    {
        return PublishCoreAsync(
            sourcePath,
            destinationPath,
            TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)),
            progress,
            retrying,
            SystemStagingFileOperations.Instance,
            SystemPublishTiming.Instance,
            cancellationToken);
    }

    public static void CleanupAbandonedWorkspaces(string rootPath, DateTimeOffset olderThanUtc)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return;
        }

        string normalizedRoot;
        try
        {
            normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        string[] directories;
        try
        {
            directories = Directory.GetDirectories(normalizedRoot);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        foreach (var directory in directories)
        {
            var normalizedDirectory = Path.GetFullPath(directory);
            if (ActiveWorkspaces.ContainsKey(normalizedDirectory))
            {
                continue;
            }

            try
            {
                if (new DirectoryInfo(normalizedDirectory).LastWriteTimeUtc < olderThanUtc.UtcDateTime)
                {
                    Directory.Delete(normalizedDirectory, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    internal static async Task PublishCoreAsync(
        string sourcePath,
        string destinationPath,
        TimeSpan timeout,
        IProgress<double>? progress,
        Action<int, Exception, TimeSpan>? retrying,
        IStagingFileOperations fileOperations,
        IPublishTiming timing,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException("The destination path must include a directory.", nameof(destinationPath));
        }

        var destinationTempPath = destinationPath + ".part." + Guid.NewGuid().ToString("N");
        Exception? lastException = null;
        var progressState = new MonotonicProgress(progress);
        try
        {
            for (var attempt = 1; attempt <= PublishAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                fileOperations.DeleteFile(destinationTempPath);
                try
                {
                    fileOperations.CreateDirectory(destinationDirectory);
                    using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCancellation.CancelAfter(timeout);
                    await fileOperations.CopyAsync(sourcePath, destinationTempPath, progressState, timeoutCancellation.Token).ConfigureAwait(false);
                    if (fileOperations.GetLength(sourcePath) != fileOperations.GetLength(destinationTempPath))
                    {
                        throw new IOException("The staged media length did not match the published temporary file length.");
                    }

                    fileOperations.MoveReplace(destinationTempPath, destinationPath);
                    progressState.Report(1);
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    lastException = new TimeoutException($"Publishing did not complete within {timeout.TotalSeconds:0} seconds.", ex);
                }
                catch (IOException ex) when (!IsDiskFull(ex))
                {
                    lastException = ex;
                }

                fileOperations.DeleteFile(destinationTempPath);
                if (attempt < PublishAttempts)
                {
                    var delay = attempt == 1 ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(5);
                    if (lastException != null)
                    {
                        retrying?.Invoke(attempt, lastException, delay);
                    }

                    await timing.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            fileOperations.DeleteFile(destinationTempPath);
        }

        if (lastException is TimeoutException timeoutException)
        {
            throw timeoutException;
        }

        throw new IOException("Failed to publish staged media after 3 attempts.", lastException);
    }

    internal static void CleanupWorkspace(string workingDirectory)
    {
        var normalized = Path.GetFullPath(workingDirectory);
        _ = ActiveWorkspaces.TryRemove(normalized, out _);
        try
        {
            if (Directory.Exists(normalized))
            {
                Directory.Delete(normalized, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static MediaStagingResolution ResolveRoot(string? configuredDirectory, string? hostTempDirectory)
    {
        string? warning = null;
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            if (TryPrepareRoot(configuredDirectory, out var configuredRoot, out var configuredError))
            {
                return new MediaStagingResolution(configuredRoot, null);
            }

            warning = $"Configured staging directory was unavailable ({configuredError}); using the media server temporary directory.";
        }

        var hostError = "the path is empty";
        if (!string.IsNullOrWhiteSpace(hostTempDirectory) && TryPrepareRoot(hostTempDirectory, out var hostRoot, out hostError))
        {
            return new MediaStagingResolution(hostRoot, warning);
        }

        if (!string.IsNullOrWhiteSpace(hostTempDirectory))
        {
            warning = $"The media server temporary directory was unavailable ({hostError}); using the operating system temporary directory.";
        }

        if (TryPrepareRoot(Path.GetTempPath(), out var systemRoot, out var systemError))
        {
            return new MediaStagingResolution(systemRoot, warning);
        }

        throw new IOException($"No writable local staging directory is available: {systemError}");
    }

    private static bool TryPrepareRoot(string baseDirectory, out string rootPath, out string error)
    {
        rootPath = string.Empty;
        error = string.Empty;
        try
        {
            if (!Path.IsPathFullyQualified(baseDirectory))
            {
                error = "the path is not absolute";
                return false;
            }

            var fullBasePath = Path.GetFullPath(baseDirectory.Trim());
            if (IsWindowsNetworkPath(fullBasePath))
            {
                error = "network paths cannot be used for local staging";
                return false;
            }

            rootPath = Path.Combine(fullBasePath, "AnimeThemesSync", "downloads");
            Directory.CreateDirectory(rootPath);
            var probePath = Path.Combine(rootPath, ".write-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var probe = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                probe.WriteByte(0);
                probe.Flush(true);
            }
            finally
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            error = ex.Message;
            rootPath = string.Empty;
            return false;
        }
    }

    private static bool IsWindowsNetworkPath(string path)
    {
        if (Path.DirectorySeparatorChar != '\\')
        {
            return false;
        }

        if (path.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            var root = Path.GetPathRoot(path);
            return !string.IsNullOrWhiteSpace(root) && new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool IsDiskFull(IOException exception)
    {
        var nativeCode = exception.HResult & 0xFFFF;
        return nativeCode is 28 or 39 or 112;
    }

    private sealed class MonotonicProgress : IProgress<double>
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

    internal interface IStagingFileOperations
    {
        void CreateDirectory(string path);

        Task CopyAsync(string sourcePath, string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken);

        long GetLength(string path);

        void MoveReplace(string sourcePath, string destinationPath);

        void DeleteFile(string path);
    }

    internal interface IPublishTiming
    {
        Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }

    private sealed class SystemPublishTiming : IPublishTiming
    {
        public static SystemPublishTiming Instance { get; } = new();

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay, cancellationToken);
        }
    }

    private sealed class SystemStagingFileOperations : IStagingFileOperations
    {
        public static SystemStagingFileOperations Instance { get; } = new();

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public async Task CopyAsync(string sourcePath, string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, true);
            using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize, true);
            var length = input.Length;
            var copied = 0L;
            var buffer = new byte[CopyBufferSize];
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
                copied += read;
                if (length > 0)
                {
                    progress?.Report((double)copied / length);
                }
            }

            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public long GetLength(string path)
        {
            return new FileInfo(path).Length;
        }

        public void MoveReplace(string sourcePath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                try
                {
                    File.Replace(sourcePath, destinationPath, null);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Delete(destinationPath);
                    File.Move(sourcePath, destinationPath);
                }
            }
            else
            {
                File.Move(sourcePath, destinationPath);
            }
        }

        public void DeleteFile(string path)
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
    }
}
