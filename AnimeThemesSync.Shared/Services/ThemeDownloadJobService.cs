using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

public enum ThemeDownloadJobRemovalResult
{
    Removed,
    NotFound,
    Active,
}

public enum ThemeDownloadJobRetryResult
{
    Retried,
    NotFound,
    NotFailed,
}

public static class ThemeDownloadJobService
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, MutableJobStatus> Jobs = new(StringComparer.Ordinal);
    private static readonly Queue<string> PendingQueue = new();
    private static int _runningCount;
    private static int _maxConcurrentDownloads = 2;
    private static TimeSpan _terminalRetention = TimeSpan.FromMinutes(30);
    private static int _maxTerminalHistory = 100;

    public static void Configure(int maxConcurrentDownloads)
    {
        List<MutableJobStatus> jobsToStart;
        lock (SyncRoot)
        {
            _maxConcurrentDownloads = maxConcurrentDownloads > 0 ? maxConcurrentDownloads : 2;
            PruneTerminalJobsLocked(DateTimeOffset.UtcNow);
            jobsToStart = PumpQueueLocked();
        }

        StartJobs(jobsToStart);
    }

    public static ThemeDownloadJobStartResult Start(
        ThemeDownloadJobDescriptor descriptor,
        Func<IProgress<double>, CancellationToken, Task<ThemeDownloadExecutionResult>> action,
        System.Action? onFirstTerminal = null)
    {
#if NETSTANDARD2_1
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }
#else
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(action);
#endif
        var displayTitle = string.IsNullOrWhiteSpace(descriptor.DisplayTitle)
            ? descriptor.JobType + " download"
            : descriptor.DisplayTitle.Trim();
        if (displayTitle.Length > 200)
        {
            displayTitle = displayTitle.Substring(0, 200);
        }

        descriptor = descriptor with { DisplayTitle = displayTitle };

        var jobId = Guid.NewGuid().ToString("N");
        var cts = new CancellationTokenSource();
        var status = new MutableJobStatus(jobId, descriptor, action, cts, onFirstTerminal);
        List<MutableJobStatus> jobsToStart;

        lock (SyncRoot)
        {
            PruneTerminalJobsLocked(DateTimeOffset.UtcNow);
            Jobs[jobId] = status;
            PendingQueue.Enqueue(jobId);
            jobsToStart = PumpQueueLocked();
        }

        StartJobs(jobsToStart);

        lock (SyncRoot)
        {
            return status.ToStartResult();
        }
    }

    public static ThemeDownloadJobStartResult Start(
        string message,
        Func<IProgress<double>, CancellationToken, Task<ThemeDownloadExecutionResult>> action)
    {
        return Start(new ThemeDownloadJobDescriptor("Download", null, null, message), action);
    }

    public static void Enqueue(
        string message,
        Func<IProgress<double>, CancellationToken, Task<ThemeDownloadExecutionResult>> action)
    {
        Start(message, action);
    }

    public static ThemeDownloadJobStatus? Get(string jobId)
    {
        lock (SyncRoot)
        {
            PruneTerminalJobsLocked(DateTimeOffset.UtcNow);
            if (!Jobs.TryGetValue(jobId, out var status))
            {
                return null;
            }

            return status.ToImmutable(GetQueuePositionsLocked());
        }
    }

    public static IEnumerable<ThemeDownloadJobStatus> GetAll()
    {
        lock (SyncRoot)
        {
            PruneTerminalJobsLocked(DateTimeOffset.UtcNow);
            var queuePositions = GetQueuePositionsLocked();
            return Jobs.Values
                .OrderBy(j => StatusSortOrder(j.Status))
                .ThenBy(j => queuePositions.TryGetValue(j.JobId, out var position) ? position : int.MaxValue)
                .ThenByDescending(j => j.CreatedAt)
                .Select(j => j.ToImmutable(queuePositions))
                .ToList();
        }
    }

    public static ThemeDownloadJobStatus? Cancel(string jobId)
    {
        CancellationTokenSource? cancellationTokenSource = null;
        System.Action? terminalCallback = null;
        ThemeDownloadJobStatus? result;
        List<MutableJobStatus> jobsToStart;

        lock (SyncRoot)
        {
            PruneTerminalJobsLocked(DateTimeOffset.UtcNow);
            if (!Jobs.TryGetValue(jobId, out var status))
            {
                return null;
            }

            if (string.Equals(status.Status, "Pending", StringComparison.Ordinal))
            {
                status.Status = "Cancelled";
                status.Message = "Cancelled";
                status.FinishedAt = DateTimeOffset.UtcNow;
                status.Action = null;
                cancellationTokenSource = status.CancellationTokenSource;
                status.CancellationTokenSource = null;
                terminalCallback = status.TerminalCallback;
                status.TerminalCallback = null;
            }
            else if (string.Equals(status.Status, "Running", StringComparison.Ordinal))
            {
                status.Status = "Cancelling";
                status.Message = "Cancelling...";
                cancellationTokenSource = status.CancellationTokenSource;
            }

            jobsToStart = PumpQueueLocked();
            result = status.ToImmutable(GetQueuePositionsLocked());
        }

        cancellationTokenSource?.Cancel();
        if (result?.Status == "Cancelled")
        {
            cancellationTokenSource?.Dispose();
            InvokeTerminalCallback(terminalCallback);
        }

        StartJobs(jobsToStart);
        return result;
    }

    public static ThemeDownloadJobRemovalResult RemoveTerminal(string jobId)
    {
        lock (SyncRoot)
        {
            PruneTerminalJobsLocked(DateTimeOffset.UtcNow);
            if (!Jobs.TryGetValue(jobId, out var status))
            {
                return ThemeDownloadJobRemovalResult.NotFound;
            }

            if (!status.FinishedAt.HasValue)
            {
                return ThemeDownloadJobRemovalResult.Active;
            }

            Jobs.Remove(jobId);
            return ThemeDownloadJobRemovalResult.Removed;
        }
    }

    public static ThemeDownloadJobRetryResult RetryFailed(string jobId, out ThemeDownloadJobStatus? result)
    {
        List<MutableJobStatus> jobsToStart;
        lock (SyncRoot)
        {
            PruneTerminalJobsLocked(DateTimeOffset.UtcNow);
            if (!Jobs.TryGetValue(jobId, out var status))
            {
                result = null;
                return ThemeDownloadJobRetryResult.NotFound;
            }

            if (!string.Equals(status.Status, "Failed", StringComparison.Ordinal) || status.Action == null)
            {
                result = status.ToImmutable(GetQueuePositionsLocked());
                return ThemeDownloadJobRetryResult.NotFailed;
            }

            status.Status = "Pending";
            status.Progress = 0;
            status.Message = "Queued";
            status.Result = null;
            status.Error = null;
            status.StartedAt = null;
            status.FinishedAt = null;
            status.CancellationTokenSource = new CancellationTokenSource();
            PendingQueue.Enqueue(jobId);
            jobsToStart = PumpQueueLocked();
            result = status.ToImmutable(GetQueuePositionsLocked());
        }

        StartJobs(jobsToStart);
        return ThemeDownloadJobRetryResult.Retried;
    }

    public static int RemoveFinishedHistory()
    {
        lock (SyncRoot)
        {
            PruneTerminalJobsLocked(DateTimeOffset.UtcNow);
            var ids = Jobs.Values
                .Where(job => string.Equals(job.Status, "Completed", StringComparison.Ordinal) ||
                              string.Equals(job.Status, "Cancelled", StringComparison.Ordinal))
                .Select(job => job.JobId)
                .ToList();
            foreach (var jobId in ids)
            {
                Jobs.Remove(jobId);
            }

            return ids.Count;
        }
    }

    internal static void ResetForTests()
    {
        List<CancellationTokenSource> tokens;
        lock (SyncRoot)
        {
            tokens = Jobs.Values
                .Select(j => j.CancellationTokenSource)
                .Where(cts => cts != null)
                .Cast<CancellationTokenSource>()
                .ToList();
            Jobs.Clear();
            PendingQueue.Clear();
            _runningCount = 0;
            _maxConcurrentDownloads = 2;
            _terminalRetention = TimeSpan.FromMinutes(30);
            _maxTerminalHistory = 100;
        }

        foreach (var token in tokens)
        {
            token.Cancel();
            token.Dispose();
        }
    }

    internal static void ConfigureRetentionForTests(TimeSpan retention, int maxTerminalHistory)
    {
        lock (SyncRoot)
        {
            _terminalRetention = retention;
            _maxTerminalHistory = maxTerminalHistory;
        }
    }

    private static List<MutableJobStatus> PumpQueueLocked()
    {
        var jobsToStart = new List<MutableJobStatus>();
        while (_runningCount < _maxConcurrentDownloads && PendingQueue.Count > 0)
        {
            var jobId = PendingQueue.Dequeue();
            if (!Jobs.TryGetValue(jobId, out var job) || !string.Equals(job.Status, "Pending", StringComparison.Ordinal))
            {
                continue;
            }

            job.Status = "Running";
            job.Message = "Starting...";
            job.StartedAt = DateTimeOffset.UtcNow;
            _runningCount++;
            jobsToStart.Add(job);
        }

        return jobsToStart;
    }

    private static void StartJobs(IEnumerable<MutableJobStatus> jobs)
    {
        foreach (var job in jobs)
        {
            _ = Task.Run(() => ExecuteJobAsync(job));
        }
    }

    private static async Task ExecuteJobAsync(MutableJobStatus job)
    {
        var action = job.Action;
        var cancellationTokenSource = job.CancellationTokenSource;
        if (action == null || cancellationTokenSource == null)
        {
            return;
        }

        try
        {
            var progress = new InlineProgress(value =>
            {
                lock (SyncRoot)
                {
                    if (!string.Equals(job.Status, "Running", StringComparison.Ordinal))
                    {
                        return;
                    }

                    job.Progress = Math.Max(0, Math.Min(100, value));
                    job.Message = value >= 100 ? "Finalizing..." : "Downloading...";
                }
            });
            var executionResult = await action(progress, cancellationTokenSource.Token).ConfigureAwait(false);
            CompleteJob(job, "Completed", "Completed", executionResult, null);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            CompleteJob(job, "Cancelled", "Cancelled", null, null);
        }
        catch (Exception ex)
        {
            CompleteJob(job, "Failed", "Failed", null, ex.Message);
        }
    }

    private static void CompleteJob(
        MutableJobStatus job,
        string status,
        string message,
        ThemeDownloadExecutionResult? result,
        string? error)
    {
        CancellationTokenSource? cancellationTokenSource;
        System.Action? terminalCallback;
        List<MutableJobStatus> jobsToStart;
        lock (SyncRoot)
        {
            job.Status = status;
            job.Message = message;
            job.Result = result;
            job.Error = error;
            job.Progress = string.Equals(status, "Completed", StringComparison.Ordinal) ? 100 : job.Progress;
            job.FinishedAt = DateTimeOffset.UtcNow;
            if (!string.Equals(status, "Failed", StringComparison.Ordinal))
            {
                job.Action = null;
            }

            cancellationTokenSource = job.CancellationTokenSource;
            job.CancellationTokenSource = null;
            terminalCallback = job.TerminalCallback;
            job.TerminalCallback = null;
            _runningCount = Math.Max(0, _runningCount - 1);
            PruneTerminalJobsLocked(DateTimeOffset.UtcNow);
            jobsToStart = PumpQueueLocked();
        }

        cancellationTokenSource?.Dispose();
        InvokeTerminalCallback(terminalCallback);
        StartJobs(jobsToStart);
    }

    private static void InvokeTerminalCallback(System.Action? callback)
    {
        try
        {
            callback?.Invoke();
        }
        catch
        {
            // A terminal observer must not change the completed job state.
        }
    }

    private static Dictionary<string, int> GetQueuePositionsLocked()
    {
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        var position = 0;
        foreach (var jobId in PendingQueue)
        {
            if (Jobs.TryGetValue(jobId, out var job) && string.Equals(job.Status, "Pending", StringComparison.Ordinal))
            {
                positions[jobId] = ++position;
            }
        }

        return positions;
    }

    private static void PruneTerminalJobsLocked(DateTimeOffset now)
    {
        var terminalJobs = Jobs.Values
            .Where(job => job.FinishedAt.HasValue)
            .OrderByDescending(job => job.FinishedAt)
            .ToList();
        var expiredIds = terminalJobs
            .Where(job => now - job.FinishedAt!.Value > _terminalRetention)
            .Select(job => job.JobId)
            .Concat(terminalJobs.Skip(_maxTerminalHistory).Select(job => job.JobId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var jobId in expiredIds)
        {
            Jobs.Remove(jobId);
        }
    }

    private static int StatusSortOrder(string status)
    {
        return status switch
        {
            "Running" => 0,
            "Cancelling" => 1,
            "Pending" => 2,
            _ => 3,
        };
    }

    private sealed class MutableJobStatus
    {
        public MutableJobStatus(
            string jobId,
            ThemeDownloadJobDescriptor descriptor,
            Func<IProgress<double>, CancellationToken, Task<ThemeDownloadExecutionResult>> action,
            CancellationTokenSource cancellationTokenSource,
            System.Action? terminalCallback)
        {
            JobId = jobId;
            Descriptor = descriptor;
            Action = action;
            CancellationTokenSource = cancellationTokenSource;
            TerminalCallback = terminalCallback;
            Status = "Pending";
            Message = "Queued";
            CreatedAt = DateTimeOffset.UtcNow;
        }

        public string JobId { get; }

        public string Status { get; set; }

        public double Progress { get; set; }

        public string Message { get; set; }

        public ThemeDownloadExecutionResult? Result { get; set; }

        public string? Error { get; set; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset? StartedAt { get; set; }

        public DateTimeOffset? FinishedAt { get; set; }

        public ThemeDownloadJobDescriptor Descriptor { get; }

        public Func<IProgress<double>, CancellationToken, Task<ThemeDownloadExecutionResult>>? Action { get; set; }

        public CancellationTokenSource? CancellationTokenSource { get; set; }

        public System.Action? TerminalCallback { get; set; }

        public ThemeDownloadJobStartResult ToStartResult()
        {
            return new ThemeDownloadJobStartResult(JobId, Status, Progress, Message, Descriptor.JobType, Descriptor.ItemId, Descriptor.RowId, Descriptor.DisplayTitle);
        }

        public ThemeDownloadJobStatus ToImmutable(Dictionary<string, int> queuePositions)
        {
            return new ThemeDownloadJobStatus(
                JobId,
                Status,
                Progress,
                Message,
                Result,
                Error,
                Descriptor.JobType,
                Descriptor.ItemId,
                Descriptor.RowId,
                Descriptor.DisplayTitle,
                CreatedAt,
                StartedAt,
                FinishedAt,
                queuePositions.TryGetValue(JobId, out var queuePosition) ? queuePosition : null,
                string.Equals(Status, "Pending", StringComparison.Ordinal) || string.Equals(Status, "Running", StringComparison.Ordinal),
                string.Equals(Status, "Failed", StringComparison.Ordinal) && Action != null);
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
}
