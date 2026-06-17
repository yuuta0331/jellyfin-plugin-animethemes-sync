using System;
using System.Net.Http;
using AnimeThemesSync.Shared;

namespace Emby.Plugin.AnimeThemesSync.Helpers;

/// <summary>
/// Minimal HTTP client factory for shared services in Emby runtime.
/// </summary>
public sealed class StaticHttpClientFactory : IHttpClientFactory
{
    /// <inheritdoc />
    public HttpClient CreateClient(string name)
    {
        var client = new HttpClient();

        if (string.Equals(name, Constants.AniListHttpClientName, StringComparison.OrdinalIgnoreCase))
        {
            client.BaseAddress = new Uri(Constants.AniListBaseUrl);
        }
        else if (string.Equals(name, Constants.AnimeThemesHttpClientName, StringComparison.OrdinalIgnoreCase))
        {
            client.BaseAddress = new Uri(Constants.AnimeThemesBaseUrl);
        }

        if (!client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
        }

        return client;
    }
}
