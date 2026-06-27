# AnimeThemes Sync Plugin (Jellyfin / Emby)

<p>
<a href="https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync/actions/workflows/build.yaml">
<img alt="GitHub Workflow Status" src="https://img.shields.io/github/actions/workflow/status/CassisCloud/jellyfin-plugin-animethemes-sync/build.yaml?branch=main&logo=github">
</a>
<a href="https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync/search?l=c%23">
<img alt="GitHub top language" src="https://img.shields.io/github/languages/top/CassisCloud/jellyfin-plugin-animethemes-sync?color=%23239120&label=.NET&logo=csharp">
</a>
<a href="https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync/blob/main/LICENSE">
<img alt="License" src="https://img.shields.io/github/license/CassisCloud/jellyfin-plugin-animethemes-sync">
</a>
<a href="https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync">
<img alt="GitHub Stars" src="https://img.shields.io/github/stars/CassisCloud/jellyfin-plugin-animethemes-sync?style=flat">
</a>
<a href="https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync">
<img alt="Downloads" src="https://img.shields.io/github/downloads/CassisCloud/jellyfin-plugin-animethemes-sync/total">
</a>
<a href="https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync/releases">
<img alt="Releases" src="https://img.shields.io/github/v/release/CassisCloud/jellyfin-plugin-animethemes-sync?include_prereleases&logo=smartthings">
</a>
</p>

## Platforms

[![Jellyfin](https://img.shields.io/static/v1?color=%2300A4DC&style=for-the-badge&label=Jellyfin&logo=jellyfin&message=10.11.x)](https://jellyfin.org/)
[![Emby](https://img.shields.io/static/v1?color=%2352B54B&style=for-the-badge&label=Emby&logo=emby&message=4.8.x)](https://emby.media/)

AnimeThemes Sync adds AnimeThemes.moe integration to your media server:

- Metadata matching via AniList / MyAnimeList IDs
- AnimeThemes external links on items
- Scheduled OP/ED theme downloading (video/audio)
- Series, season, and movie support
- Season Finder UI for unmatched season mappings
- Optional season-level downloads, so you can keep output at the series level when preferred

## Installation

### Jellyfin (Repository - Recommended)

1. Open Jellyfin Dashboard.
2. Go to `Plugins` -> `Repositories`.
3. Add repository:
   - Name: `AnimeThemes Sync`
   - URL: `https://cassiscloud.github.io/jellyfin-plugin-animethemes-sync/manifest.json`
4. Open `Catalog`, find `AnimeThemes Sync`, and install.
5. Restart Jellyfin.

### Jellyfin (Manual)

1. Download assets from [Releases](https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync/releases).
2. Extract/copy plugin files into your Jellyfin plugin directory.
3. Restart Jellyfin.

### Emby (Manual)

1. Download the latest Emby package from [Releases](https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync/releases).
2. Place the Emby plugin files in your Emby plugins folder (for example: `.../embyserver/system/plugins/AnimeThemesSync/`).
3. Restart Emby Server.

## Usage

### Enable metadata provider

- Enable `AnimeThemes Sync` in your library metadata downloaders.
- Refresh metadata for your anime library/items.

### Run theme downloader

- Open Scheduled Tasks.
- Run `Download Anime Themes`.
- Theme files will be created in media folders (`backdrops` / `theme-music`).
- Series output remains under the series folder; season-specific mappings write to each season folder.
- Disable `Enable Season Theme Downloads` in the plugin configuration to keep scheduled and on-demand output at the series/movie level.
- Open `AnimeThemes Browser` -> `Season Finder` to review unmatched seasons, search AnimeThemes, preview OP/ED entries, and save season mappings without editing JSON.

## Manual Linking

If automatic matching fails, set external IDs manually:

- `AnimeThemes Slug` (recommended)
- `AnimeThemes ID`

Example: `https://animethemes.moe/anime/blackrock_shooter_tv` -> slug is `blackrock_shooter_tv`.

### Season Finder and season mappings

When multiple anime seasons are grouped into one Jellyfin/Emby series, the scheduled task follows AniList relations from the series AniList ID and tries to assign normal seasons to separate AnimeThemes anime automatically.
If a season is unmatched or mapped incorrectly, open `AnimeThemes Browser` -> `Season Finder`:

1. Select a season from `Unmatched`, `Manual`, `Auto`, or `All`.
2. Search AnimeThemes by title and optional year.
3. Select a candidate, preview the OP/ED rows, then choose `Save mapping` or `Save & Download`.

`Save & Download` stores the mapping and runs an on-demand download for that season item. Season 1 (and an unnumbered normal season) writes `backdrops`, `theme-music`, and `extras` to the parent Series folder; Season 2 and later write to their Season folder. When Season 1 is explicitly mapped to an AnimeThemes entry different from the Series entry, its filenames use a `Season 01 - ` prefix to avoid collisions. Existing files under a Season 1 folder are not moved or deleted automatically. The plugin still supports `SeasonThemeMappings` JSON in the configuration page as an advanced fallback.
If `Enable Season Theme Downloads` is disabled, mappings remain saved but season output and season on-demand downloads are skipped until the option is enabled again.

```json
{
  "SeasonThemeMappings": [
    {
      "Enabled": true,
      "SeriesPath": "D:\\Anime\\Example Series",
      "SeasonNumber": 2,
      "AnimeThemesSlug": "example_series_second_season",
      "Locked": true
    },
    {
      "SeasonPath": "D:\\Anime\\Example Series\\Season 03",
      "AniListId": 12345
    }
  ]
}
```

The series-level `theme-music` / `backdrops` folders are kept. If a season resolves to the same AnimeThemes anime as the series, duplicate season output is skipped.

## License

This project is licensed under the GNU GPL v3.0. See [LICENSE](LICENSE).

## Disclaimer

This plugin is unofficial and is not affiliated with Jellyfin, Emby, AniList, MyAnimeList, or AnimeThemes.moe.
Please follow each service's terms and rate limits.
