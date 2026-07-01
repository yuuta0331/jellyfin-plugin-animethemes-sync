using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class LocalMediaCleanupTaskServiceTests : IDisposable
{
    public LocalMediaCleanupTaskServiceTests()
    {
        LocalMediaCleanupTaskService.ResetForTests();
    }

    public void Dispose()
    {
        LocalMediaCleanupTaskService.ResetForTests();
    }

    [Fact]
    public async Task Scan_FiltersStatusSourceKindAndSearch()
    {
        var scan = LocalMediaCleanupTaskService.StartScan((_, _) => Task.FromResult<IReadOnlyList<LocalMediaCleanupFile>>(
        [
            File("one", "Undesired", "Untracked", "Audio", "Show A"),
            File("two", "Undesired", "BrowserManual", "Video", "Show B"),
            File("three", "Desired", "Scheduled", "Audio", "Show A"),
            File("four", "Unknown", "TrackedUnknown", "Extra", "Show C"),
        ]));

        await WaitUntilAsync(() => LocalMediaCleanupTaskService.GetTask(scan.TaskId)?.Status == "Completed");

        var page = LocalMediaCleanupTaskService.GetFiles(scan.ScanId, 0, 20, "Undesired", "Untracked,Scheduled", "Audio", null, "Show A");
        var item = Assert.Single(page.Items);
        Assert.Equal("one", item.CandidateId);
        Assert.Equal(1, page.TotalRecordCount);
    }

    [Fact]
    public async Task Delete_PassesExplicitlySelectedCandidatesRegardlessOfRecommendation()
    {
        var scan = LocalMediaCleanupTaskService.StartScan((_, _) => Task.FromResult<IReadOnlyList<LocalMediaCleanupFile>>(
        [
            File("delete", "Undesired", "Untracked", "Audio", "Show"),
            File("keep", "Desired", "Scheduled", "Audio", "Show"),
        ]));
        await WaitUntilAsync(() => LocalMediaCleanupTaskService.GetTask(scan.TaskId)?.Status == "Completed");

        IReadOnlyList<LocalMediaCleanupFile>? received = null;
        var task = LocalMediaCleanupTaskService.StartDelete(scan.ScanId, ["delete", "keep"], (files, _, _) =>
        {
            received = files;
            return Task.FromResult(new LocalMediaCleanupDeleteResult(files.Count, files.Sum(file => file.Size), 0, 0));
        });
        await WaitUntilAsync(() => LocalMediaCleanupTaskService.GetTask(task.TaskId)?.Status == "Completed");

        Assert.NotNull(received);
        Assert.Equal(2, received!.Count);
        Assert.Contains(received, file => file.CandidateId == "delete");
        Assert.Contains(received, file => file.CandidateId == "keep");
        Assert.Equal(2, LocalMediaCleanupTaskService.GetTask(task.TaskId)?.Result?.FilesDeleted);
    }

    private static LocalMediaCleanupFile File(string id, string status, string source, string kind, string itemName)
    {
        return new LocalMediaCleanupFile(
            id,
            Guid.NewGuid(),
            itemName,
            "Series",
            "Anime",
            Path.Combine("C:\\Media", id + ".mp3"),
            id + ".mp3",
            kind,
            status,
            source,
            10,
            DateTimeOffset.UtcNow);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var timeout = DateTime.UtcNow.AddSeconds(3);
        while (!predicate())
        {
            if (DateTime.UtcNow >= timeout)
            {
                throw new TimeoutException();
            }

            await Task.Delay(10);
        }
    }
}
