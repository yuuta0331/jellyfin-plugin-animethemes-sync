# AnimeThemes Sync Plugin (Jellyfin / Emby)

<p align="center">
  <img src="resource/images/jellyfin-plugin-animethemes-sync.jpeg" alt="AnimeThemes Sync Logo" width="600" />
</p>

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

[![Jellyfin](https://img.shields.io/static/v1?color=%2300A4DC&style=for-the-badge&label=Jellyfin&logo=jellyfin&message=10.11.x)](https://jellyfin.org/)
[![Emby](https://img.shields.io/static/v1?color=%2352B54B&style=for-the-badge&label=Emby&logo=emby&message=4.8%2B)](https://emby.media/)

[AnimeThemes.moe](https://animethemes.moe/) のOP/EDテーマをアニメライブラリに統合するプラグインです。自動マッチング、テーマ動画/音声のダウンロード、管理UI、放送シーズンのタグ・コレクション自動化を提供します。

[English README is here](README.md)

---

## スクリーンショット

<p align="center">
  <img src="resource/images/01-library-browser.png" alt="AnimeThemes Browser" width="800" />
  <br/><sup><em>AnimeThemes Browser — ライブラリの検索・フィルタ・管理</em></sup>
</p>

<p align="center">
  <img src="resource/images/02-library-detail.png" alt="テーマ詳細" width="800" />
  <br/><sup><em>シーズンごとのテーマ詳細 — OP/EDのプレビュー、再生、個別ダウンロード</em></sup>
</p>

---

## 主な機能

- **自動マッチング** — AniList / MyAnimeList IDを使ってシリーズ・シーズン・映画をAnimeThemesの作品へ解決。外部IDによる手動指定にも対応
- **テーマダウンロード** — OP/EDのテーマ動画（`backdrops`）とテーマ曲（`theme-music`）、任意でブラウズ可能なExtras。メディア種別ごとの上限設定、ffmpegによる音量調整
- **ダウンロードエンジン** — 進捗表示・キャンセル・リトライ・履歴付きのジョブキュー、分割（マルチコネクション）ダウンロード、同時実行数の設定
- **AnimeThemes Browser** — 検索、フィルタ（種別・リンク状態・保存状態・放送シーズン）、ソート、ページングを備えた管理ページ。OP/EDのプレビュー、テーマ単位のダウンロード（音声/動画/Extrasを選択可）、保存済みファイルの再生・削除
- **Season Finder** — 未一致シーズンの確認、タイトル+年でのAnimeThemes検索、候補のプレビュー、シーズンマッピングの保存をJSON編集なしで実行。マッピングのエクスポート/インポートに対応
- **放送シーズン自動化** — シーズンタグ（表記は`{Season} {Year}`形式でローカライズ・カスタマイズ可能）と、メタデータロック・ポスター/サムネイル/背景の自動生成に対応した放送シーズンコレクション
- **メンテナンス機能** — プラグイン作成ファイルのクリーンアップスキャナ、Browser/プロバイダキャッシュの管理、TTL設定付きの永続キャッシュ
- **Manager Issues** — ダウンロード、インポート、マッピング、シーズン自動化、Managerタスクで永続的に失敗またはスキップされた作業を追跡し、フィルタとアクションを提供

## インストール

### Jellyfin（リポジトリ経由 - 推奨）

1. Jellyfin Dashboard → `Plugins` → `Repositories` を開く
2. リポジトリを追加
   - Name: `AnimeThemes Sync`
   - URL: `https://cassiscloud.github.io/jellyfin-plugin-animethemes-sync/manifest.json`
3. `Catalog` から `AnimeThemes Sync` をインストールし、Jellyfinを再起動

### Jellyfin（手動）

1. [Releases](https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync/releases) からJellyfin用パッケージをダウンロード
2. Jellyfinのプラグインフォルダへ展開し、再起動

### Emby（手動）

1. [Releases](https://github.com/CassisCloud/jellyfin-plugin-animethemes-sync/releases) からEmby用パッケージをダウンロード
2. Embyのpluginsフォルダへ配置（例: `.../embyserver/system/plugins/AnimeThemesSync/`）し、Emby Serverを再起動

## クイックスタート

1. アニメライブラリのMetadata Downloadersで `AnimeThemes Sync` を有効化し、メタデータを更新
2. スケジュールタスク `Download Anime Themes` を実行 — メディアフォルダにテーマファイルが作成されます
3. ダッシュボードメニューの `AnimeThemes Browser` で結果の確認、プレビュー、個別ダウンロードができます

## スケジュールタスク

| タスク | 内容 |
|---|---|
| `Download Anime Themes` | 有効なライブラリ全体のOP/EDテーマを解決・ダウンロードし、シーズンメタデータとBrowserキャッシュを更新 |
| `Refresh Anime Season Metadata` | 古い/欠落したシーズンメタデータ・タグ・コレクション・Browserデータを**ダウンロードなしで**更新（既定: 週1回） |

## 出力レイアウト

- シリーズのテーマはシリーズフォルダへ出力されます（`backdrops` / `theme-music`、Extras有効時は `extras`）
- `Enable Season Theme Downloads` 有効時（既定）、Season 1（および番号なしの通常シーズン）は親シリーズフォルダへ、Season 2以降は各シーズンフォルダへ出力されます
- Season 1がシリーズと異なるAnimeThemes作品へ明示的にマッピングされた場合、衝突回避のためファイル名に `Season 01 - ` プレフィックスが付きます
- シーズンがシリーズと同じAnimeThemes作品に解決される場合、重複出力はスキップされます。既存ファイルの自動移動・自動削除は行いません

## Season Finder とマッピング

<p align="center">
  <img src="resource/images/03-season-finder.png" alt="Season Finder" width="800" />
  <br/><sup><em>Season Finder — 未一致シーズンをAnimeThemesの作品にマッピング</em></sup>
</p>

複数期が1つのシリーズにまとまっている場合、プラグインはAniListのrelationsを辿って各シーズンをAnimeThemesの別作品へ自動割り当てします。未一致・誤一致のシーズンは `AnimeThemes Browser` → `Season Finder` で修正できます。

1. `Unmatched` / `Manual` / `Auto` / `All` タブからシーズンを選択（シーズン番号や検索語で絞り込み可能）
2. タイトルと任意の年でAnimeThemesを検索し、候補のOP/EDをプレビュー
3. `Save mapping` または `Save & Download` を実行

マッピングはプラグインのSQLiteデータベース（`animethemes-sync.db`）に保存され、MappingsコントロールからJSONでエクスポート/インポートできます。プラグイン設定内の旧 `SeasonThemeMappings` は初回に自動で取り込まれます。

## 放送シーズンのタグとコレクション

- **タグ**: 放送シーズンタグ（例: `Spring 2024`）を付与します。季節の表記と `{Season} {Year}` 形式はカスタマイズ・ローカライズ可能です。付与先は Series / 各 Season / 両方 から選択できます。タグ機能を無効化する際は、プラグインが付与したタグを削除するかどうかを選べるクリーンアップダイアログが表示されます。
- **コレクション**: `Create broadcast-season collections` で放送シーズンごとのコレクションを作成します。管理対象はプラグインが作成したコレクションのみで、同名のため再利用しただけのコレクションには触れません。機能を無効化する際は、管理コレクションを維持するか整理するかを選択カードで選べます。
  - `Lock collection metadata`（既定: 有効）は他のメタデータプロバイダーによる名前・画像の上書きを防ぎます。ユーザー自身が設定したロックは解除されません。解除するにはオプションを無効にしてSyncを実行してください。
  - `Generate collection images`（既定: 有効）はメンバーのポスターを合成してポスター（最大4枚）、16:9サムネイル、16:9背景グリッドを生成し、メンバー変更時に再生成します。手動で差し替えた画像は検知され、以後そのスロットには触れません。オーバーレイ・キャンバスの色/不透明度は設定できます。
- どちらも次回の定期実行または手動Syncで既存の管理コレクションへ遡って適用されます。Browser設定には全シリーズを強制更新する `Rebuild all season metadata` と、実行中Syncのキャンセルも用意されています。

## メンテナンスとキャッシュ

- **ローカルメディアクリーンアップ**（Browser → Manager）: ライブラリ内のテーマファイルをスキャンし、プラグインが作成したファイルと未追跡ファイルを区別して表示。選択したものだけを削除します（管理対象フォルダの外には触れません）
- **キャッシュ**: Browserデータとプロバイダ（AniList / AnimeThemes）応答は永続化され、再起動やページ再読込後も高速に表示されます。TTLは設定可能です（`Season metadata TTL (days)` / `Provider response TTL (days)`、1〜365、既定30）。Browserキャッシュの再構築/クリアとプロバイダキャッシュのクリアはBrowser設定から実行できます
- ライブラリ変更（追加・更新）は数秒以内にBrowserキャッシュへ差分反映されます。削除時は全再構築が走ります

## 手動リンク

自動マッチングが失敗する場合は、アイテムに外部IDを設定してください。

- `AnimeThemes Slug`（推奨） — `https://animethemes.moe/anime/blackrock_shooter_tv` の場合、slugは `blackrock_shooter_tv`
- `AnimeThemes ID`

## ライセンス

GNU GPL v3.0 — 詳細は [LICENSE](LICENSE) を参照してください。

## 免責事項

このプラグインは非公式であり、Jellyfin / Emby / AniList / MyAnimeList / AnimeThemes.moe とは提携していません。各サービスの利用規約およびレート制限を守ってご利用ください。
