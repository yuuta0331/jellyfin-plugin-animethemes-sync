# Jellyfin Plugin AnimeThemes Sync

This plugin automatically downloads Opening (OP) and Ending (ED) themes from **[AnimeThemes.moe](https://animethemes.moe)** to enhance your Jellyfin library.
It also adds direct links to AnimeThemes on each series page, making it easy to browse themes in your browser.

## Key Features

-   **Automated Theme Downloading**
    -   Automatically fetches high-quality theme videos for your series and saves them locally.
    -   **Smart Downloading**: Use of temporary files during download prevents Jellyfin from detecting incomplete files.
    -   **Quality Control**: Customizable preferences for resolution and creditless (NC) versions.
-   **Quick Access to AnimeThemes.moe**
    -   Adds a link button to the Jellyfin series page, providing one-click access to the full theme list on AnimeThemes.
-   **Accurate Series Matching**
    -   Utilizes **AniList** or **MyAnimeList** IDs to correctly link your series with AnimeThemes data.
    -   As a matching aid, it can automatically tag series with "Year" and "Season" for better library organization.
-   **Flexible Configuration**
    -   Per-series settings allow you to control download targets (OP/ED only) and volume levels.

## Installation

Since this plugin is not yet in the official repository, you need to build it from source or download a release (if available).

### Manual Installation (from source)

1.  **Build the plugin**:
    ```bash
    dotnet build -c Release
    ```
2.  **Locate the DLL**:
    The compiled file will be at:
    `Jellyfin.Plugin.AnimeThemesSync/bin/Release/net9.0/Jellyfin.Plugin.AnimeThemesSync.dll`
3.  **Install**:
    -   Create a folder named `AnimeThemesSync` inside your Jellyfin plugins directory.
        -   Windows: `%ProgramData%\Jellyfin\Server\plugins\` or `%AppData%\jellyfin\plugins\`
        -   Linux: `/var/lib/jellyfin/plugins/`
    -   Copy `Jellyfin.Plugin.AnimeThemesSync.dll` into that folder.
4.  **Restart Jellyfin**: Restart your Jellyfin server to load the plugin.

## Configuration

1.  Go to **Dashboard** > **Plugins**.
2.  Click on **AnimeThemes Sync**.
3.  **Enable OP/ED Theme Downloading**: Check this box to enable the downloader task (Default: Enabled).
4.  Click **Save**.

## Usage

### Metadata
The plugin works as a metadata provider.
1.  Go to your Anime library settings.
2.  Enable **AnimeThemesSync** under "Series Metadata Downloaders".
3.  Refresh metadata for your library or specific series.
    -   The plugin attempts to match anime using AniList/MyAnimeList ID (if already present) or by searching AniList with the series name and year.
    -   Once matched, it fetches data from AnimeThemes and adds Tags.

### Theme Downloader
The downloader runs as a scheduled task.
1.  Go to **Dashboard** > **Scheduled Tasks**.
2.  Find **Download Anime Themes** under the "Anime" category.
3.  Click the **Play** button to run it manually, or wait for the weekly schedule.
4.  Check your series folders; you should see a `themes` folder containing `.webm` files.

## Customization

### Display AnimeThemes Logo instead of Text
To replace the text link "AnimeThemes" with a logo in the Jellyfin web interface, add the following CSS to **Dashboard** > **General** > **Custom CSS**:

```css
/* Replace "AnimeThemes" link with a logo */
.itemExternalLinks > a[href*="animethemes.moe"] {
    font-size: 0 !important;
    display: inline-block;
    width: 24px;
    height: 24px;
    background-image: url('https://github.com/AnimeThemes.png');
    background-size: contain;
    background-repeat: no-repeat;
    background-position: center;
    vertical-align: middle;
}
```

## Manual Linking

If automatic matching fails, you can manually link an anime series.

1. Open the anime series in Jellyfin.
2. Select **Edit Metadata**.
3. Locate **AnimeThemes ID** or **AnimeThemes Slug** in the "External Ids" section.
4. Enter the **AnimeThemes Slug** (recommended as the ID is not visible on the site).
   - If the URL is `https://animethemes.moe/anime/blackrock_shooter_tv`, the Slug is `blackrock_shooter_tv`.
5. Save changes and **Refresh Metadata**.

## Disclaimer
This plugin is unofficial and not affiliated with Jellyfin, AniList, MyAnimeList, or AnimeThemes.moe. Please respect the API rate limits and terms of service of these platforms.
