using Jellyfin.Plugin.AnimeThemesSync.ExternalIds;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.AnimeThemesSync
{
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
        }
    }
}
