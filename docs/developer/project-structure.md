# Project Structure

## Top-level

- `Jellyfin.Plugin.AnimeThemesSync/`
  - Jellyfin 向けプラグイン本体（net9.0）
- `Emby.Plugin.AnimeThemesSync/`
  - Emby 向けプラグイン本体（net8.0）
- `AnimeThemesSync.Shared/`
  - 共通ロジック（API クライアント、DTO、スコアリング、設定モデル）
- `Jellyfin.Plugin.AnimeThemesSync.Tests/`
  - xUnit テスト
- `.github/workflows/`
  - CI/CD ワークフロー
- `.github/scripts/`
  - `manifest.json` 生成スクリプト
- `build.yaml`
  - Jellyfin リポジトリ公開用メタデータ

## Jellyfin Plugin

- `Plugin.cs`
  - プラグインエントリ
- `PluginServiceRegistrator.cs`
  - DI 登録
- `AnimeThemesMetadataProvider.cs`
  - シリーズ向けメタデータ
- `AnimeThemesMovieMetadataProvider.cs`
  - 映画向けメタデータ
- `ScheduledTasks/ThemeDownloader.cs`
  - ダウンロードタスク
  - Series / Season / Movie のテーマ出力計画を作成
  - AniList relations と `SeasonThemeMappings` による Season 別 AnimeThemes 解決
  - Season Finder API の状態一覧、検索、プレビュー、保存/削除処理
- `ExternalIds/`
  - AnimeThemes 用外部 ID / URL プロバイダ
- `Configuration/`
  - 設定モデル・設定 UI
  - AnimeThemes Browser / Season Finder UI

## Emby Plugin

- `Plugin.cs`
  - プラグインエントリ
- `Providers/`
  - シリーズ / 映画メタデータプロバイダ
- `ScheduledTasks/ThemeDownloader.cs`
  - ダウンロードタスク
  - Series / Season / Movie のテーマ出力計画を作成
  - AniList relations と `SeasonThemeMappings` による Season 別 AnimeThemes 解決
  - Season Finder API の状態一覧、検索、プレビュー、保存/削除処理
- `Helpers/`
  - Emby ロガーアダプタ、HttpClientFactory
- `Configuration/`
  - 設定モデル・設定 UI
  - AnimeThemes Browser / Season Finder UI

## Shared

- `Services/AniListService.cs`
  - AniList GraphQL 検索
  - AniList relations から続編/前日譚チェーンを取得
- `Services/AnimeThemesService.cs`
  - AnimeThemes API クライアント
  - external-id / slug 解決とタイトル検索
- `Services/ThemeScoringService.cs`
  - OP/ED 候補スコアリング
- `Services/ThemeFilePlanner.cs`
  - `theme-music` / `backdrops` / `extras` の出力計画と cleanup plan を生成
  - 複数出力先の計画を統合
- `Models/`
  - AnimeThemes API DTO / スコアモデル / Theme Browser・Season Finder DTO
- `Configuration/`
  - 音声/映像ごとのテーマ設定
  - Season ごとの AnimeThemes 手動マッピングモデル
- `Constants.cs`
  - プロバイダキー、API URL、HttpClient 名など
- `RateLimiter.cs`
  - レスポンスヘッダ反映型レート制御
