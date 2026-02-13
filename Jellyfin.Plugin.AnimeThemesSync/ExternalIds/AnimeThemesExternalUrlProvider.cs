using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.AnimeThemesSync.ExternalIds;

/// <summary>
/// External url provider for AnimeThemes.
/// </summary>
public class AnimeThemesExternalUrlProvider : IExternalUrlProvider
{
    /// <inheritdoc />
    public string Name => "AnimeThemes";

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item.TryGetProviderId("AnimeThemes", out var slug))
        {
            yield return $"https://animethemes.moe/anime/{slug}";
        }
    }
}
