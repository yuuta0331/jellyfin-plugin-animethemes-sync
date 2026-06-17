# Release and Distribution

## Build Outputs

- Jellyfin: `Jellyfin.Plugin.AnimeThemesSync/bin/Release/net9.0/`
- Emby: `Emby.Plugin.AnimeThemesSync/bin/Release/net8.0/`

## GitHub Actions

- `build.yaml`
  - push / PR 時に Shared, Jellyfin, Emby を検証ビルド
- `test.yaml`
  - テストワークフロー
- `github-release.yaml`
  - `v*` タグでリリース作成、Jellyfin/Emby ZIP と md5 を公開
- `update-repo.yaml`
  - リリース完了後に `manifest.json` を再生成し `gh-pages` に配信

## Release Steps

1. `main` に変更をマージ
2. 注釈付きタグを作成
3. タグを push

```bash
git tag -a v1.2.3 -m "Release v1.2.3"
git push origin v1.2.3
```

## Manifest Generation

- スクリプト: `.github/scripts/generate_manifest.py`
- データソース:
  - `build.yaml`
  - GitHub Releases API
- 挙動:
  - Jellyfin 向け ZIP を優先して採用
  - `.md5` を checksum として反映
  - `published_at` でバージョンを降順ソート

## End-user Distribution

- Jellyfin: GitHub Pages の `manifest.json` URL をリポジトリ登録
- Emby: 現在は手動配布（Release アセットの ZIP を展開して配置）
