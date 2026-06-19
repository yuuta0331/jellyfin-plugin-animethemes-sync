namespace AnimeThemesSync.Shared;

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
    /// UI asset version used to force web clients to pick up changed embedded pages.
    /// </summary>
    public const string UiAssetVersion = "20260620a";

    /// <summary>
    /// Human-readable UI version displayed on plugin pages.
    /// </summary>
    public const string UiDisplayVersion = "2026.06.20-a";

    /// <summary>
    /// The display name used by the metadata provider (matches AnimeThemesMetadataProvider.Name).
    /// </summary>
    public const string MetadataProviderName = "AnimeThemes Sync";

    /// <summary>
    /// The AniList provider id key.
    /// </summary>
    public const string AniListProviderId = "AniList";

    /// <summary>
    /// The MyAnimeList provider id key.
    /// </summary>
    public const string MyAnimeListProviderId = "MyAnimeList";

    /// <summary>
    /// The AnimeThemes provider id key.
    /// </summary>
    public const string AnimeThemesProviderId = "AnimeThemes";

    /// <summary>
    /// The AnimeThemes numeric id provider key.
    /// </summary>
    public const string AnimeThemesNumericProviderId = "AnimeThemesId";

    /// <summary>
    /// Site key for AniList in AnimeThemes resource API.
    /// </summary>
    public const string AniListSiteKey = "anilist";

    /// <summary>
    /// Site key for MyAnimeList in AnimeThemes resource API.
    /// </summary>
    public const string MyAnimeListSiteKey = "myanimelist";

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

    /// <summary>
    /// Named HttpClient for AniList.
    /// </summary>
    public const string AniListHttpClientName = "AniList";

    /// <summary>
    /// Named HttpClient for AnimeThemes.
    /// </summary>
    public const string AnimeThemesHttpClientName = "AnimeThemes";

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
