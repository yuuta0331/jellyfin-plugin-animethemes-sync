using System.Net;
using AnimeThemesSync.Shared.Services;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class AnimeThemesMediaHttpClientTests
{
    [Fact]
    public async Task Client_UsesItsOwnedHandler()
    {
        var handler = new RecordingHandler();
        using var mediaClient = new AnimeThemesMediaHttpClient(handler);

        using var response = await mediaClient.Client.GetAsync("https://example.test/theme.webm");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, handler.Requests);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
