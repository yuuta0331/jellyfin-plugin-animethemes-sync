# Architecture

## Core Flow

1. メタデータ更新時
   - 既存の AniList / MyAnimeList ID を確認
   - 必要に応じて `AniListService` で名前検索
   - `AnimeThemesService` で AnimeThemes データを取得
   - 外部 ID / 外部リンク / タグ情報を設定

2. スケジュール実行時（ThemeDownloader）
   - 対象ライブラリを走査
   - 作品ごとの desired files を計算
   - 並列数制限付きでダウンロード
   - 必要に応じて ffmpeg で音量調整 / 変換
   - 不要ファイルをクリーンアップ

## Shared Services

- `AniListService`
  - 検索候補をタイトル・年でスコアリング
  - `RateLimiter` により API 制限を吸収

- `AnimeThemesService`
  - 外部 ID（AniList/MAL）または slug から取得
  - `anime` が配列/単体の両パターンに対応

- `ThemeScoringService`
  - OP/ED 種別、クレジット有無、重複などを評価
  - 上位候補をダウンロード対象として採用

## Host-specific Differences

- Jellyfin
  - DI でサービスを登録
  - `IExternalUrlProvider` で AnimeThemes リンクを表示

- Emby
  - Emby API に合わせた Provider 実装
  - ログと HttpClient のアダプタを使用

## Configuration Model

- グローバル
  - ダウンロード有効、並列数、タイムアウト、削除可否、タグ設定
- メディア別
  - Series/Movie x Audio/Video の個別設定
  - 最大件数、OP/ED 除外、クレジット/重複除外、音量

## Error Handling

- API 呼び出し失敗時は null を返し、処理を継続
- ダウンロードはリトライ付き
- ffmpeg 失敗時は raw ファイル配置へフォールバック
