namespace Jellyfin.Plugin.AnimeThemesSync;

/// <summary>
/// Constants for the AnimeThemes plugin.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The name of the plugin.
    /// </summary>
    public const string PluginName = "AnimeThemesSync";

    /// <summary>
    /// The display name used by the metadata provider (matches AnimeThemesMetadataProvider.Name).
    /// </summary>
    public const string MetadataProviderName = "AnimeThemes Sync";

    /// <summary>
    /// The GUID of the plugin.
    /// </summary>
    public const string PluginGuid = "66d528df-4632-4d43-9828-56957262572b";

    /// <summary>
    /// The base URL for AniList API.
    /// </summary>
    public const string AniListBaseUrl = "https://graphql.anilist.co";

    /// <summary>
    /// The base URL for AnimeThemes API.
    /// </summary>
    public const string AnimeThemesBaseUrl = "https://api.animethemes.moe";

    /// <summary>
    /// The base URL for AnimeThemes web pages.
    /// </summary>
    public const string AnimeThemesWebUrl = "https://animethemes.moe";

    // Headers

    /// <summary>
    /// Header name for rate limit remaining.
    /// </summary>
    public const string RateLimitRemainingHeader = "X-RateLimit-Remaining";

    /// <summary>
    /// Header name for rate limit reset time.
    /// </summary>
    public const string RateLimitResetHeader = "X-RateLimit-Reset";

    /// <summary>
    /// Header name for retry after time.
    /// </summary>
    public const string RetryAfterHeader = "Retry-After";

    /// <summary>
    /// The user agent string to use for HTTP requests.
    /// </summary>
    public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
}
