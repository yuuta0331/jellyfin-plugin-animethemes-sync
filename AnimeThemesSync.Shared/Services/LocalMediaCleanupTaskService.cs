using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

public static class LocalMediaCleanupTaskService
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, MutableTask> Tasks = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ScanSnapshot> Scans = new(StringComparer.Ordinal);
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(30);

    public static LocalMediaCleanupTaskStatus StartScan(
        Func<IProgress<double>, CancellationToken, Task<IReadOnlyList<LocalMediaCleanupFile>>> action)
    {
        var id = Guid.NewGuid().ToString("N");
        var task = new MutableTask(id, id, "Scan");
        lock (SyncRoot)
        {
            PruneLocked();
            Tasks[id] = task;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var files = await action(CreateProgress(task), task.Cancellation.Token).ConfigureAwait(false);
                lock (SyncRoot)
                {
                    var now = DateTimeOffset.UtcNow;
                    Scans[id] = new ScanSnapshot(files.ToList(), now.Add(Retention));
                    CompleteLocked(task, "Completed", $"Found {files.Count} local media files.", null, null);
                }
            }
            catch (OperationCanceledException) when (task.Cancellation.IsCancellationRequested)
            {
                lock (SyncRoot)
                {
                    CompleteLocked(task, "Cancelled", "Scan cancelled.", null, null);
                }
            }
            catch (Exception ex)
            {
                lock (SyncRoot)
                {
                    CompleteLocked(task, "Failed", "Scan failed.", null, ex.Message);
                }
            }
        });

        return task.ToImmutable();
    }

    public static LocalMediaCleanupTaskStatus StartDelete(
        string scanId,
        IReadOnlyCollection<string> candidateIds,
        Func<IReadOnlyList<LocalMediaCleanupFile>, IProgress<double>, CancellationToken, Task<LocalMediaCleanupDeleteResult>> action)
    {
        List<LocalMediaCleanupFile> selected;
        lock (SyncRoot)
        {
            PruneLocked();
            if (!Scans.TryGetValue(scanId, out var scan))
            {
                throw new InvalidOperationException("The cleanup scan has expired or was not found.");
            }

            var ids = candidateIds.ToHashSet(StringComparer.Ordinal);
            selected = scan.Files
                .Where(file => ids.Contains(file.CandidateId))
                .ToList();
        }

        if (selected.Count == 0)
        {
            throw new InvalidOperationException("No cleanup files were selected.");
        }

        var taskId = Guid.NewGuid().ToString("N");
        var task = new MutableTask(taskId, scanId, "Delete");
        lock (SyncRoot)
        {
            Tasks[taskId] = task;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await action(selected, CreateProgress(task), task.Cancellation.Token).ConfigureAwait(false);
                lock (SyncRoot)
                {
                    CompleteLocked(task, "Completed", $"Deleted {result.FilesDeleted} files.", result, null);
                }
            }
            catch (OperationCanceledException) when (task.Cancellation.IsCancellationRequested)
            {
                lock (SyncRoot)
                {
                    CompleteLocked(task, "Cancelled", "Cleanup cancelled.", null, null);
                }
            }
            catch (Exception ex)
            {
                lock (SyncRoot)
                {
                    CompleteLocked(task, "Failed", "Cleanup failed.", null, ex.Message);
                }
            }
        });

        return task.ToImmutable();
    }

    public static LocalMediaCleanupTaskStatus? GetTask(string taskId)
    {
        lock (SyncRoot)
        {
            PruneLocked();
            return Tasks.TryGetValue(taskId, out var task) ? task.ToImmutable() : null;
        }
    }

    public static LocalMediaCleanupTaskStatus? Cancel(string taskId)
    {
        lock (SyncRoot)
        {
            PruneLocked();
            if (!Tasks.TryGetValue(taskId, out var task))
            {
                return null;
            }

            if (string.Equals(task.Status, "Running", StringComparison.Ordinal))
            {
                task.Message = "Cancelling...";
                task.Cancellation.Cancel();
            }

            return task.ToImmutable();
        }
    }

    internal static void ResetForTests()
    {
        List<CancellationTokenSource> tokens;
        lock (SyncRoot)
        {
            tokens = Tasks.Values.Select(task => task.Cancellation).ToList();
            Tasks.Clear();
            Scans.Clear();
        }

        foreach (var token in tokens)
        {
            token.Cancel();
            token.Dispose();
        }
    }

    public static LocalMediaCleanupScanPage GetFiles(
        string scanId,
        int? startIndex,
        int? limit,
        string? status,
        string? sources,
        string? kinds,
        string? library,
        string? searchTerm)
    {
        lock (SyncRoot)
        {
            PruneLocked();
            if (!Scans.TryGetValue(scanId, out var scan))
            {
                throw new InvalidOperationException("The cleanup scan has expired or was not found.");
            }

            IEnumerable<LocalMediaCleanupFile> query = scan.Files;
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(file => string.Equals(file.Status, status, StringComparison.OrdinalIgnoreCase));
            }

            query = ApplySetFilter(query, sources, file => file.Source);
            query = ApplySetFilter(query, kinds, file => file.FileKind);
            if (!string.IsNullOrWhiteSpace(library))
            {
                query = query.Where(file => string.Equals(file.LibraryName, library, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(file => file.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                            file.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                            file.Path.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                            (file.LibraryName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var materialized = query.OrderBy(file => file.ItemName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var offset = Math.Max(0, startIndex ?? 0);
            var take = Math.Max(1, Math.Min(200, limit ?? 50));
            return new LocalMediaCleanupScanPage(
                materialized.Skip(offset).Take(take).ToList(),
                materialized.Count,
                materialized.Sum(file => file.Size),
                offset,
                take,
                scanId,
                scan.ExpiresAt);
        }
    }

    private static IEnumerable<LocalMediaCleanupFile> ApplySetFilter(
        IEnumerable<LocalMediaCleanupFile> query,
        string? raw,
        Func<LocalMediaCleanupFile, string> selector)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return query;
        }

        var values = raw.Split(',').Select(value => value.Trim()).Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return query.Where(file => values.Contains(selector(file)));
    }

    private static Progress<double> CreateProgress(MutableTask task)
    {
        return new Progress<double>(value =>
        {
            lock (SyncRoot)
            {
                task.Progress = Math.Max(0, Math.Min(100, value));
                task.Message = task.TaskType == "Scan" ? "Scanning local media..." : "Deleting selected files...";
            }
        });
    }

    private static void CompleteLocked(MutableTask task, string status, string message, LocalMediaCleanupDeleteResult? result, string? error)
    {
        task.Status = status;
        task.Message = message;
        task.Result = result;
        task.Error = error;
        task.Progress = string.Equals(status, "Completed", StringComparison.Ordinal) ? 100 : task.Progress;
        task.FinishedAt = DateTimeOffset.UtcNow;
    }

    private static void PruneLocked()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var scanId in Scans.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToList())
        {
            Scans.Remove(scanId);
        }

        foreach (var taskId in Tasks.Where(pair => pair.Value.FinishedAt.HasValue && now - pair.Value.FinishedAt.Value > Retention).Select(pair => pair.Key).ToList())
        {
            Tasks.Remove(taskId);
        }
    }

    private sealed class MutableTask
    {
        public MutableTask(string taskId, string scanId, string taskType)
        {
            TaskId = taskId;
            ScanId = scanId;
            TaskType = taskType;
        }

        public string TaskId { get; }

        public string ScanId { get; }

        public string TaskType { get; }

        public string Status { get; set; } = "Running";

        public double Progress { get; set; }

        public string Message { get; set; } = "Starting...";

        public LocalMediaCleanupDeleteResult? Result { get; set; }

        public string? Error { get; set; }

        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? FinishedAt { get; set; }

        public CancellationTokenSource Cancellation { get; } = new();

        public LocalMediaCleanupTaskStatus ToImmutable() => new(
            TaskId,
            ScanId,
            TaskType,
            Status,
            Progress,
            Message,
            Result,
            Error,
            CreatedAt,
            FinishedAt,
            string.Equals(Status, "Running", StringComparison.Ordinal));
    }

    private sealed record ScanSnapshot(List<LocalMediaCleanupFile> Files, DateTimeOffset ExpiresAt);
}
