using System;
using System.Net.Http;
using AnimeThemesSync.Shared;

namespace Emby.Plugin.AnimeThemesSync.Helpers;

/// <summary>
/// Minimal HTTP client factory for shared services in Emby runtime.
/// </summary>
public sealed class StaticHttpClientFactory : IHttpClientFactory
{
    private static readonly HttpClient AniListClient = CreateConfiguredClient(Constants.AniListBaseUrl, TimeSpan.FromSeconds(20));
    private static readonly HttpClient AnimeThemesClient = CreateConfiguredClient(Constants.AnimeThemesBaseUrl, System.Threading.Timeout.InfiniteTimeSpan);

    /// <inheritdoc />
    public HttpClient CreateClient(string name)
    {
        if (string.Equals(name, Constants.AniListHttpClientName, StringComparison.OrdinalIgnoreCase))
        {
            return AniListClient;
        }

        if (string.Equals(name, Constants.AnimeThemesHttpClientName, StringComparison.OrdinalIgnoreCase))
        {
            return AnimeThemesClient;
        }

        return CreateConfiguredClient(null, TimeSpan.FromSeconds(20));
    }

    private static HttpClient CreateConfiguredClient(string? baseUrl, TimeSpan timeout)
    {
        var client = new HttpClient
        {
            Timeout = timeout,
        };

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            client.BaseAddress = new Uri(baseUrl);
        }

        if (!client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
        }

        return client;
    }
}
