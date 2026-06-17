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
- シリーズ・映画の両対応

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

## Manual Linking

自動マッチングが失敗する場合は、外部 ID を手動で設定します。

- `AnimeThemes Slug`（推奨）
- `AnimeThemes ID`

例: `https://animethemes.moe/anime/blackrock_shooter_tv` の slug は `blackrock_shooter_tv`

## License

このプロジェクトは GNU GPL v3.0 ライセンスです。詳細は [LICENSE](LICENSE) を参照してください。

## Disclaimer

このプラグインは非公式であり、Jellyfin / Emby / AniList / MyAnimeList / AnimeThemes.moe とは提携していません。
各サービスの利用規約およびレート制限を守ってご利用ください。
