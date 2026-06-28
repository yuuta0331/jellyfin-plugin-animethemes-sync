using System.IO;
using AnimeThemesSync.Shared.Services;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class FileDeleteRetryServiceTests
{
    [Fact]
    public async Task DeleteAsync_RetriesTransientIoFailuresThenSucceeds()
    {
        var attempts = 0;
        var retryDelays = new List<int>();

        await FileDeleteRetryService.DeleteAsync(
            () =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new IOException("locked");
                }
            },
            "theme.webm",
            "Emby",
            (_, delay, _, _) => retryDelays.Add(delay),
            CancellationToken.None,
            (_, _) => Task.CompletedTask);

        Assert.Equal(3, attempts);
        Assert.Equal([250, 500], retryDelays);
    }

    [Fact]
    public async Task DeleteAsync_ConvertsPersistentIoFailureToControlledError()
    {
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => FileDeleteRetryService.DeleteAsync(
            () =>
            {
                attempts++;
                throw new IOException("locked");
            },
            "theme.webm",
            "Emby",
            null,
            CancellationToken.None,
            (_, _) => Task.CompletedTask));

        Assert.Equal(5, attempts);
        Assert.IsType<IOException>(exception.InnerException);
        Assert.Contains("currently in use by Emby", exception.Message, StringComparison.Ordinal);
        Assert.Contains("theme.webm", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAsync_ObservesCancellationBetweenRetries()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FileDeleteRetryService.DeleteAsync(
            () =>
            {
                attempts++;
                throw new IOException("locked");
            },
            "theme.webm",
            "Emby",
            null,
            cancellationTokenSource.Token,
            (_, token) =>
            {
                cancellationTokenSource.Cancel();
                return Task.Delay(0, token);
            }));

        Assert.Equal(1, attempts);
    }
}
