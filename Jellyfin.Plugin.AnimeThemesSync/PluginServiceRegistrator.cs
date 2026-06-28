using System;
using System.Net.Http;
using System.Threading;
using AnimeThemesSync.Shared;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Services;
using Jellyfin.Plugin.AnimeThemesSync.ExternalIds;
using Jellyfin.Plugin.AnimeThemesSync.ScheduledTasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        serviceCollection.AddSingleton<IAnimeThemesDataPathProvider, JellyfinAnimeThemesDataPathProvider>();
        serviceCollection.AddSingleton<IAnimeThemesServerIdentityProvider, JellyfinAnimeThemesServerIdentityProvider>();
        serviceCollection.AddSingleton(provider =>
        {
            var store = new AnimeThemesDataStore(
                provider.GetRequiredService<IAnimeThemesDataPathProvider>(),
                provider.GetRequiredService<IAnimeThemesServerIdentityProvider>());
            store.EnsureInitialized();
            ThemeExtrasManifestService.ConfigureStore(store);
            return store;
        });
        serviceCollection.AddSingleton<ISeasonFinderDataStore>(provider =>
        {
            var store = new SeasonFinderDataStore(
                provider.GetRequiredService<IAnimeThemesDataPathProvider>(),
                provider.GetRequiredService<IAnimeThemesServerIdentityProvider>());
            store.EnsureInitialized();
            return store;
        });
        serviceCollection.AddSingleton<ThemeDownloader>();
        serviceCollection.AddHostedService<BrowserCacheWarmupService>();
        serviceCollection.AddHttpClient(Constants.AniListHttpClientName, client =>
        {
            client.BaseAddress = new Uri(Constants.AniListBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Constants.UserAgent);
        });
        serviceCollection.AddHttpClient(Constants.AnimeThemesHttpClientName, client =>
        {
            client.BaseAddress = new Uri(Constants.AnimeThemesBaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Constants.UserAgent);
        });

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
            return new AnimeThemesService(
                httpClientFactory,
                loggerFactory.CreateLogger<AnimeThemesService>(),
                rateLimiter,
                provider.GetRequiredService<ISeasonFinderDataStore>());
        });
    }
}
