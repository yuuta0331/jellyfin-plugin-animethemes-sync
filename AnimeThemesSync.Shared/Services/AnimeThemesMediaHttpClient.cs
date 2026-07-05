using System;
using System.Net.Http;
using System.Threading;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Owns the dedicated HTTP client used only for AnimeThemes media transfers.
/// The client is intentionally created outside IHttpClientFactory so global
/// handlers registered by unrelated plugins cannot affect media downloads.
/// </summary>
public sealed class AnimeThemesMediaHttpClient : IDisposable
{
    private readonly HttpClient _client;

    public AnimeThemesMediaHttpClient()
        : this(CreateHandler())
    {
    }

    internal AnimeThemesMediaHttpClient(HttpMessageHandler handler)
    {
        _client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Constants.UserAgent);
    }

    public HttpClient Client => _client;

    public void Dispose()
    {
        _client.Dispose();
    }

    private static HttpClientHandler CreateHandler()
    {
        return new HttpClientHandler
        {
            UseCookies = false,
        };
    }
}
