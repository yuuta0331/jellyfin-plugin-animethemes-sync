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
   - Series の AniList ID から AniList relations を辿り、通常 Season を別 AnimeThemes anime へ自動割り当て
   - `SeasonThemeMappings` がある場合は自動割り当てより優先
   - Season Finder UI で保存された手動マッピングも同じ `SeasonThemeMappings` として扱う
   - 並列数制限付きでダウンロード
   - 必要に応じて ffmpeg で音量調整 / 変換
   - Series / Season / Movie ごとの出力ディレクトリ単位で不要ファイルをクリーンアップ

## Season Theme Flow

- Series 直下の `theme-music` / `backdrops` は従来通り維持する。
- Series 配下の Season に対しては、以下の順で AnimeThemes anime を解決する。
  - 設定画面の `SeasonThemeMappings`
  - Season item 自身の AnimeThemes / AniList / MAL provider id
  - Series の AniList ID から取得した `SEQUEL` / `PREQUEL` relations
- 自動 relations 解決では Movie / OVA / Special / Music 形式を通常 Season 候補から除外する。
- Season が Series と同じ AnimeThemes anime に解決された場合は、重複回避のため Season 側には出力しない。
- Season ごとに別 AnimeThemes anime が確定した場合は、`Season xx/theme-music` と `Season xx/backdrops` に出力する。
- 自動割り当てが不正確な作品は、AnimeThemes Browser の Season Finder で検索・プレビューして `SeasonThemeMappings` へ保存する。
- `Save & Download` は保存後に Season item のオンデマンドダウンロードジョブを起動する。

## Theme Finder API

- `GET /AnimeThemesSync/SeasonMappings`
  - Series配下Seasonを列挙し、`Manual` / `Direct` / `Auto` / `Series` / `Unmatched` を返す。
- `GET /AnimeThemesSync/Search?query=&year=`
  - AnimeThemes search APIを使い、候補をスコア順に返す。
- `GET /AnimeThemesSync/Anime/{slug}/Themes`
  - 候補AnimeThemes作品のOP/EDプレビュー行を返す。
- `POST /AnimeThemesSync/SeasonMappings`
  - Season item id と AnimeThemes slug / AniList / MAL ID を `SeasonThemeMappings` に保存する。
- `DELETE /AnimeThemesSync/SeasonMappings/{seasonItemId}`
  - 対象Seasonの手動マッピングを削除する。

## Shared Services

- `AniListService`
  - 検索候補をタイトル・年でスコアリング
  - AniList relations から続編/前日譚チェーンを取得
  - `RateLimiter` により API 制限を吸収

- `AnimeThemesService`
  - 外部 ID（AniList/MAL）または slug から取得
  - AnimeThemes search API によるタイトル検索
  - `anime` が配列/単体の両パターンに対応

- `ThemeScoringService`
  - OP/ED 種別、クレジット有無、重複などを評価
  - 上位候補をダウンロード対象として採用

- `ThemeFilePlanner`
  - Series / Season / Movie の出力先ごとに `theme-music` / `backdrops` / `extras` の desired files を構築
  - 複数出力先の計画を `MergePlans` で統合
  - 出力先ごとの cleanup plan を生成

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
- Season mapping
  - `SeasonThemeMappings` は Season item id、Season path、または Series path + Season number で対象 Season を指定
  - 解決先は AnimeThemes slug、AniList ID、MyAnimeList ID のいずれか
  - `Locked` は手動割り当てを将来の自動推定より優先する意図を示す
  - 通常操作は Season Finder UI から行い、JSON編集はAdvanced fallbackとして残す

## Error Handling

- API 呼び出し失敗時は null を返し、処理を継続
- ダウンロードはリトライ付き
- ffmpeg 失敗時は raw ファイル配置へフォールバック
