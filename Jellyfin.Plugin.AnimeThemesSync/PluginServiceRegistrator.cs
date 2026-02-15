using System;
using System.Net.Http;
using Jellyfin.Plugin.AnimeThemesSync.ExternalIds;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync;

/// <summary>
/// Registers plugin services.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <summary>
    /// Registers the plugin services.
    /// </summary>
    /// <param name="serviceCollection">The service collection.</param>
    /// <param name="applicationHost">The server application host.</param>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IExternalId, AnimeThemesSlugExternalId>();
        serviceCollection.AddSingleton<IExternalId, AnimeThemesIdExternalId>();
        serviceCollection.AddSingleton<IExternalUrlProvider, AnimeThemesExternalUrlProvider>();

        var userAgent = $"{Constants.PluginName}/{Plugin.Instance?.Version ?? new Version(1, 0, 0)}";

        // Register AniListService as Singleton
        serviceCollection.AddSingleton(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var rateLimiter = new RateLimiter(loggerFactory.CreateLogger<RateLimiter>(), "AniList", 90);
            return new Services.AniListService(httpClientFactory, loggerFactory.CreateLogger<Services.AniListService>(), rateLimiter);
        });

        // Register AnimeThemesService as Singleton
        serviceCollection.AddSingleton(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var rateLimiter = new RateLimiter(loggerFactory.CreateLogger<RateLimiter>(), "AnimeThemes", 80);
            return new Services.AnimeThemesService(httpClientFactory, loggerFactory.CreateLogger<Services.AnimeThemesService>(), rateLimiter);
        });
    }
}
