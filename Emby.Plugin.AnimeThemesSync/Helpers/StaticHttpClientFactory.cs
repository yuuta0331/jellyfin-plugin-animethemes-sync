using System;
using System.Net.Http;
using AnimeThemesSync.Shared;

namespace Emby.Plugin.AnimeThemesSync.Helpers;

/// <summary>
/// Minimal HTTP client factory for shared services in Emby runtime.
/// </summary>
public sealed class StaticHttpClientFactory : IHttpClientFactory
{
    private static readonly HttpClient AniListClient = CreateConfiguredClient(Constants.AniListBaseUrl);
    private static readonly HttpClient AnimeThemesClient = CreateConfiguredClient(Constants.AnimeThemesBaseUrl);

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

        return CreateConfiguredClient(null);
    }

    private static HttpClient CreateConfiguredClient(string? baseUrl)
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
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
