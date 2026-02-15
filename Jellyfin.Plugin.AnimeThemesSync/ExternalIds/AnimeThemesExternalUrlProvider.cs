using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.AnimeThemesSync.ExternalIds;

/// <summary>
/// Provides external URL to AnimeThemes page.
/// </summary>
public class AnimeThemesExternalUrlProvider : IExternalUrlProvider
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public string Name => "AnimeThemes";

    /// <summary>
    /// Gets external URLs for the given item.
    /// </summary>
    /// <param name="item">The item to get URLs for.</param>
    /// <returns>External URLs.</returns>
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item is Series && item.TryGetProviderId("AnimeThemes", out var slug) && !string.IsNullOrEmpty(slug))
        {
            yield return $"{Constants.AnimeThemesWebUrl}/anime/{slug}";
        }
    }
}
