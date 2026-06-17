using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

public static class ThemeDownloadJobService
{
    private static readonly ConcurrentDictionary<string, MutableJobStatus> Jobs = new();

    public static ThemeDownloadJobStartResult Start(
        string message,
        Func<IProgress<double>, CancellationToken, Task<ThemeDownloadExecutionResult>> action)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var status = new MutableJobStatus(jobId, "Running", 0, message, null, null);
        Jobs[jobId] = status;

        _ = Task.Run(async () =>
        {
            var progress = new Progress<double>(value =>
            {
                status.Progress = Math.Max(0, Math.Min(100, value));
                status.Message = value >= 100 ? "Finalizing..." : message;
            });

            try
            {
                var result = await action(progress, CancellationToken.None).ConfigureAwait(false);
                status.Result = result;
                status.Progress = 100;
                status.Status = "Completed";
                status.Message = "Completed";
            }
            catch (Exception ex)
            {
                status.Status = "Failed";
                status.Error = ex.Message;
                status.Message = "Failed";
            }
        });

        return new ThemeDownloadJobStartResult(jobId, status.Status, status.Progress, status.Message);
    }

    public static ThemeDownloadJobStatus? Get(string jobId)
    {
        return Jobs.TryGetValue(jobId, out var status)
            ? status.ToImmutable()
            : null;
    }

    private sealed class MutableJobStatus
    {
        public MutableJobStatus(
            string jobId,
            string status,
            double progress,
            string message,
            ThemeDownloadExecutionResult? result,
            string? error)
        {
            JobId = jobId;
            Status = status;
            Progress = progress;
            Message = message;
            Result = result;
            Error = error;
        }

        public string JobId { get; }

        public string Status { get; set; }

        public double Progress { get; set; }

        public string Message { get; set; }

        public ThemeDownloadExecutionResult? Result { get; set; }

        public string? Error { get; set; }

        public ThemeDownloadJobStatus ToImmutable()
        {
            return new ThemeDownloadJobStatus(JobId, Status, Progress, Message, Result, Error);
        }
    }
}
