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
- `ExternalIds/`
  - AnimeThemes 用外部 ID / URL プロバイダ
- `Configuration/`
  - 設定モデル・設定 UI

## Emby Plugin

- `Plugin.cs`
  - プラグインエントリ
- `Providers/`
  - シリーズ / 映画メタデータプロバイダ
- `ScheduledTasks/ThemeDownloader.cs`
  - ダウンロードタスク
- `Helpers/`
  - Emby ロガーアダプタ、HttpClientFactory
- `Configuration/`
  - 設定モデル・設定 UI

## Shared

- `Services/AniListService.cs`
  - AniList GraphQL 検索
- `Services/AnimeThemesService.cs`
  - AnimeThemes API クライアント
- `Services/ThemeScoringService.cs`
  - OP/ED 候補スコアリング
- `Models/`
  - AnimeThemes API DTO / スコアモデル
- `Configuration/`
  - 音声/映像ごとのテーマ設定
- `Constants.cs`
  - プロバイダキー、API URL、HttpClient 名など
- `RateLimiter.cs`
  - レスポンスヘッダ反映型レート制御
