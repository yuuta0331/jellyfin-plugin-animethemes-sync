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

AnimeThemes Sync は、AnimeThemes.moe 連携機能を Jellyfin / Emby に追加します。

- AniList / MyAnimeList ID を使ったメタデータマッチング
- アイテム画面への AnimeThemes 外部リンク追加
- OP/ED テーマの定期ダウンロード（動画/音声）
- シリーズ・Season・映画の対応
- 未一致Seasonを検索・保存できるSeason Finder UI
- Season単位の出力を有効/無効にできる設定

## Installation

### Jellyfin（リポジトリ経由 - 推奨）

1. Jellyfin Dashboard を開く
2. `Plugins` -> `Repositories` へ移動
3. 以下を追加
   - Name: `AnimeThemes Sync`
   - URL: `https://cassiscloud.github.io/jellyfin-plugin-animethemes-sync/manifest.json`
4. `Catalog` で `AnimeThemes Sync` をインストール
5. Jellyfin を再起動

### Jellyfin（手動）

1. [Releases](https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync/releases) からアセットをダウンロード
2. Jellyfin のプラグインフォルダへ展開/配置
3. Jellyfin を再起動

### Emby（手動）

1. [Releases](https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync/releases) から最新の Emby 用パッケージをダウンロード
2. Emby の plugins フォルダへ配置（例: `.../embyserver/system/plugins/AnimeThemesSync/`）
3. Emby Server を再起動

## Usage

### メタデータプロバイダーを有効化

- ライブラリの Metadata Downloaders で `AnimeThemes Sync` を有効化
- 対象ライブラリ/アイテムのメタデータを更新

### テーマダウンローダーを実行

- Scheduled Tasks を開く
- `Download Anime Themes` を実行
- メディアフォルダ内にテーマファイルが作成されます（`backdrops` / `theme-music`）
- シリーズにはシリーズ直下、Seasonに別マッピングがある場合は各Seasonフォルダ直下へ作成されます
- 設定画面の `Enable Season Theme Downloads` を無効にすると、Season単位の出力を止めてシリーズ/映画単位の出力だけにできます
- `AnimeThemes Browser` -> `Season Finder` を開くと、未一致Seasonの確認、AnimeThemes検索、OP/EDプレビュー、Seasonマッピング保存をJSON編集なしで実行できます

## Manual Linking

自動マッチングが失敗する場合は、外部 ID を手動で設定します。

- `AnimeThemes Slug`（推奨）
- `AnimeThemes ID`

例: `https://animethemes.moe/anime/blackrock_shooter_tv` の slug は `blackrock_shooter_tv`

### Season Finder とSeasonごとの手動マッピング

複数期が1つのJellyfin/Embyシリーズにまとまっている場合、スケジュールタスクはシリーズのAniList IDからAniList relationsを辿り、通常SeasonをAnimeThemesの別作品へ自動割り当てします。
未一致または誤一致のSeasonがある場合は、`AnimeThemes Browser` -> `Season Finder` を使用します。

1. `Unmatched` / `Manual` / `Auto` / `All` からSeasonを選択
2. タイトルと任意の年でAnimeThemesを検索
3. 候補を選択してOP/EDをプレビューし、`Save mapping` または `Save & Download` を実行

`Save & Download` はマッピングを `animethemes-sync.db` に保存後、そのSeason itemのオンデマンドダウンロードを実行します。Season 1（および番号なしの通常Season）の `backdrops` / `theme-music` / `extras` は親Seriesフォルダへ、Season 2以降は各Seasonフォルダへ出力します。Season 1がSeriesとは異なるAnimeThemes entryへ明示的に割り当てられた場合は、衝突を避けるためファイル名に `Season 01 - ` prefixを付けます。既存のSeason 1フォルダ内のファイルは自動移動・自動削除しません。既存の `SeasonThemeMappings` 設定JSONは互換性のため初回にSQLiteへ移行され、それ以降の変更にはMappings import/exportを使用します。
`Enable Season Theme Downloads` が無効の場合、Seasonマッピングは保存されたままですが、Season出力とSeason itemのオンデマンドダウンロードは再度有効化するまでスキップされます。

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

シリーズ直下の `theme-music` / `backdrops` は維持されます。Seasonがシリーズ直下と同じAnimeThemes作品へ解決される場合、そのSeasonへの重複出力はスキップされます。

## License

このプロジェクトは GNU GPL v3.0 ライセンスです。詳細は [LICENSE](LICENSE) を参照してください。

## Disclaimer

このプラグインは非公式であり、Jellyfin / Emby / AniList / MyAnimeList / AnimeThemes.moe とは提携していません。
各サービスの利用規約およびレート制限を守ってご利用ください。
