using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class ThemeDownloadJobServiceTests : IDisposable
{
    public ThemeDownloadJobServiceTests()
    {
        ThemeDownloadJobService.ResetForTests();
    }

    public void Dispose()
    {
        ThemeDownloadJobService.ResetForTests();
    }

    [Fact]
    public async Task Jobs_RunInFifoOrderAndRespectConcurrency()
    {
        ThemeDownloadJobService.Configure(1);
        var firstGate = NewCompletionSource();
        var secondStarted = NewCompletionSource();
        var first = Start("first", async (_, cancellationToken) =>
        {
            await firstGate.Task.WaitAsync(cancellationToken);
            return EmptyResult();
        });
        var second = Start("second", (_, _) =>
        {
            secondStarted.TrySetResult();
            return Task.FromResult(EmptyResult());
        });

        var statuses = ThemeDownloadJobService.GetAll().ToList();
        Assert.Equal("Running", statuses.Single(job => job.JobId == first.JobId).Status);
        var pending = statuses.Single(job => job.JobId == second.JobId);
        Assert.Equal("Pending", pending.Status);
        Assert.Equal(1, pending.QueuePosition);
        Assert.False(secondStarted.Task.IsCompleted);

        firstGate.TrySetResult();
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => ThemeDownloadJobService.Get(second.JobId)?.Status == "Completed");
    }

    [Fact]
    public async Task Cancel_TransitionsPendingAndRunningJobsCorrectly()
    {
        ThemeDownloadJobService.Configure(1);
        var running = Start("running", async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return EmptyResult();
        });
        var pending = Start("pending", (_, _) => Task.FromResult(EmptyResult()));

        var pendingCancellation = ThemeDownloadJobService.Cancel(pending.JobId);
        Assert.NotNull(pendingCancellation);
        Assert.Equal("Cancelled", pendingCancellation.Status);
        Assert.False(pendingCancellation.CanCancel);

        var runningCancellation = ThemeDownloadJobService.Cancel(running.JobId);
        Assert.NotNull(runningCancellation);
        Assert.Equal("Cancelling", runningCancellation.Status);
        Assert.False(runningCancellation.CanCancel);
        await WaitUntilAsync(() => ThemeDownloadJobService.Get(running.JobId)?.Status == "Cancelled");
    }

    [Fact]
    public async Task Progress_IsClampedAndMetadataIsPreserved()
    {
        ThemeDownloadJobService.Configure(1);
        var gate = NewCompletionSource();
        var descriptor = new ThemeDownloadJobDescriptor("Theme", Guid.NewGuid(), "row-1", "Opening 1");
        var job = ThemeDownloadJobService.Start(descriptor, async (progress, cancellationToken) =>
        {
            progress.Report(150);
            await gate.Task.WaitAsync(cancellationToken);
            return EmptyResult();
        });

        await WaitUntilAsync(() => ThemeDownloadJobService.Get(job.JobId)?.Progress == 100);
        var status = ThemeDownloadJobService.Get(job.JobId);
        Assert.NotNull(status);
        Assert.Equal(descriptor.ItemId, status.ItemId);
        Assert.Equal("row-1", status.RowId);
        Assert.Equal("Opening 1", status.DisplayTitle);
        Assert.Equal("Theme", status.JobType);

        gate.TrySetResult();
        await WaitUntilAsync(() => ThemeDownloadJobService.Get(job.JobId)?.Status == "Completed");
    }

    [Fact]
    public async Task TerminalHistory_IsBoundedAndExpires()
    {
        ThemeDownloadJobService.Configure(1);
        ThemeDownloadJobService.ConfigureRetentionForTests(TimeSpan.FromMinutes(30), 2);
        var jobs = new List<ThemeDownloadJobStartResult>();
        for (var index = 0; index < 3; index++)
        {
            var job = Start("job-" + index, (_, _) => Task.FromResult(EmptyResult()));
            jobs.Add(job);
            await WaitUntilAsync(() => ThemeDownloadJobService.Get(job.JobId)?.Status == "Completed");
        }

        Assert.Equal(2, ThemeDownloadJobService.GetAll().Count());

        ThemeDownloadJobService.ConfigureRetentionForTests(TimeSpan.Zero, 100);
        await Task.Delay(10);
        Assert.Empty(ThemeDownloadJobService.GetAll());
    }

    [Fact]
    public async Task RemoveTerminal_RemovesCompletedFailedAndCancelledHistory()
    {
        ThemeDownloadJobService.Configure(1);
        var completed = Start("completed", (_, _) => Task.FromResult(EmptyResult()));
        await WaitUntilAsync(() => ThemeDownloadJobService.Get(completed.JobId)?.Status == "Completed");

        var failed = Start("failed", (_, _) => throw new InvalidOperationException("failed"));
        await WaitUntilAsync(() => ThemeDownloadJobService.Get(failed.JobId)?.Status == "Failed");

        var gate = NewCompletionSource();
        var cancelled = Start("cancelled", async (_, cancellationToken) =>
        {
            await gate.Task.WaitAsync(cancellationToken);
            return EmptyResult();
        });
        ThemeDownloadJobService.Cancel(cancelled.JobId);
        await WaitUntilAsync(() => ThemeDownloadJobService.Get(cancelled.JobId)?.Status == "Cancelled");

        Assert.Equal(ThemeDownloadJobRemovalResult.Removed, ThemeDownloadJobService.RemoveTerminal(completed.JobId));
        Assert.Equal(ThemeDownloadJobRemovalResult.Removed, ThemeDownloadJobService.RemoveTerminal(failed.JobId));
        Assert.Equal(ThemeDownloadJobRemovalResult.Removed, ThemeDownloadJobService.RemoveTerminal(cancelled.JobId));
        Assert.Empty(ThemeDownloadJobService.GetAll());
    }

    [Fact]
    public async Task RemoveTerminal_RejectsActiveAndReportsUnknownJobs()
    {
        ThemeDownloadJobService.Configure(1);
        var gate = NewCompletionSource();
        var running = Start("running", async (_, cancellationToken) =>
        {
            await gate.Task.WaitAsync(cancellationToken);
            return EmptyResult();
        });

        Assert.Equal(ThemeDownloadJobRemovalResult.Active, ThemeDownloadJobService.RemoveTerminal(running.JobId));
        Assert.Equal(ThemeDownloadJobRemovalResult.NotFound, ThemeDownloadJobService.RemoveTerminal("unknown"));
        Assert.NotNull(ThemeDownloadJobService.Get(running.JobId));

        gate.TrySetResult();
        await WaitUntilAsync(() => ThemeDownloadJobService.Get(running.JobId)?.Status == "Completed");
    }

    private static ThemeDownloadJobStartResult Start(
        string title,
        Func<IProgress<double>, CancellationToken, Task<ThemeDownloadExecutionResult>> action)
    {
        return ThemeDownloadJobService.Start(new ThemeDownloadJobDescriptor("Item", Guid.NewGuid(), null, title), action);
    }

    private static ThemeDownloadExecutionResult EmptyResult()
    {
        return new ThemeDownloadExecutionResult(0, 0, 0, 0, 0, 0);
    }

    private static TaskCompletionSource NewCompletionSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!predicate())
        {
            if (DateTime.UtcNow >= timeout)
            {
                throw new TimeoutException("The expected job state was not reached.");
            }

            await Task.Delay(10);
        }
    }
}
