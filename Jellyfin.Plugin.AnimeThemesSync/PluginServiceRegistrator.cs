using System.Net.Http;
using AnimeThemesSync.Shared;
using AnimeThemesSync.Shared.Services;
using Jellyfin.Plugin.AnimeThemesSync.ExternalIds;
using Jellyfin.Plugin.AnimeThemesSync.ScheduledTasks;
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
        serviceCollection.AddSingleton<ThemeDownloader>();

        // Register AniListService as Singleton
        serviceCollection.AddSingleton(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var rateLimiter = new RateLimiter(loggerFactory.CreateLogger<RateLimiter>(), Constants.AniListHttpClientName, 90);
            return new AniListService(httpClientFactory, loggerFactory.CreateLogger<AniListService>(), rateLimiter);
        });

        // Register AnimeThemesService as Singleton
        serviceCollection.AddSingleton(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var rateLimiter = new RateLimiter(loggerFactory.CreateLogger<RateLimiter>(), Constants.AnimeThemesHttpClientName, 80);
            return new AnimeThemesService(httpClientFactory, loggerFactory.CreateLogger<AnimeThemesService>(), rateLimiter);
        });
    }
}
