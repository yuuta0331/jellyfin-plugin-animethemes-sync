# Jellyfin Plugin CI/CD Documentation

このドキュメントでは、本リポジトリで採用している自動リリース（CI/CD）の仕組みと運用方法について解説します。

## 概要

GitHub Actionsを使用して、以下のプロセスを完全に自動化しています。

1.  **プラグインのビルド**: Gitタグ (`v*`) をトリガーにビルドを実行。
2.  **リリースの作成**: ビルド成果物（ZIP）とチェックサム（MD5）をGitHub Releasesに公開。
3.  **リポジトリ情報の更新**: `manifest.json` を再生成し、`gh-pages` ブランチを通じてJellyfinリポジトリとして配信。

## リリース手順

新しいバージョンのプラグインをリリースする手順は以下の通りです。

1.  開発用ブランチ（`main`など）でコードをコミットします。
2.  バージョン番号の**注釈付きタグ**を作成します（例: `v1.0.0`）。
    - タグメッセージがそのまま GitHub Release 本文になります。
    - その本文は `manifest.json` の `changelog` に反映され、Jellyfin 側の更新内容表示に使われます。
3.  タグをGitHubにプッシュします。

```bash
# タグの作成（更新内容を含める）
git tag -a v1.0.0 -m "## Changes
- Fix movie metadata provider registration for Jellyfin/Emby
- Improve deployment path handling for Testy
- Stabilize GitHub Actions release pipeline"

# タグのプッシュ
git push origin v1.0.0
```

これだけで、後はGitHub Actionsが自動的に処理を行います。処理状況はGitHubの **Actions** タブで確認できます。

## ワークフローの仕組み

2つのワークフローが連携して動作します。

### 1. Release to GitHub (`.github/workflows/github-release.yaml`)

タグのプッシュを検知して実行されるワークフローです。

**主な処理:**
*   **Version Extraction**: Gitタグ（例 `v1.0.0`）からバージョン番号（`1.0.0`）を抽出します。
*   **Release Notes Generation**:
    *   注釈付きタグの本文があればそれを使用。
    *   なければ前回タグ以降のコミット一覧を自動生成。
*   **Metadata Sync**: `build.yaml` のバージョンフィールドを自動更新します（ビルド時利用）。
*   **Build**: `.NET` ビルドを実行し、DLLを作成します。この際、抽出したバージョン番号が埋め込まれます。
*   **Package**: プラグインファイルをZIP形式で圧縮します。
*   **Checksum**: **重要** `manifest.json` 生成に必要な `MD5` チェックサムファイルを生成します。
*   **Release**: ZIPファイルとMD5ファイルをGitHub Releasesにアップロードします（本文あり）。

### 2. Update Plugin Repository (`.github/workflows/update-repo.yaml`)

リリース処理の完了を検知して自動実行されるワークフローです。
※ `workflow_run` トリガーを使用し、`📦 Release to GitHub` の成功後に起動します。

**主な処理:**
*   **Setup**: Python環境をセットアップします。
*   **Generate Manifest**: カスタムスクリプト (`.github/scripts/generate_manifest.py`) を実行します。
    *   GitHub API経由ですべてのリリース情報を取得します。
    *   各リリースの `.zip` アセットと `MD5` チェックサムを取得します。
    *   `build.yaml` の情報をベースに `manifest.json` を生成します。
*   **Deploy**: 生成された `manifest.json` だけを `gh-pages` ブランチにデプロイ（公開）します。

## リポジトリURL

Jellyfinに登録するリポジトリURLは以下の通りです。

> **https://CassisCloud.github.io/jellyfin-plugin-animethemes-sync/manifest.json**

※ GitHubリポジトリの **Settings** -> **Pages** で、Sourceが `gh-pages` になっていることを確認してください。

## カスタムスクリプトについて

マニフェスト生成には `jellyfin-plugin-repo-action` などの既存アクションではなく、独自のPythonスクリプトを採用しています。

*   **理由**: 既存アクションのメンテナンス状況による不安定さや、チェックサム取得の挙動を確実に制御するため。
*   **場所**: `.github/scripts/generate_manifest.py`
*   **動作**:
    *   `build.yaml` からプラグインの基本情報（名前、説明など）を読み込みます。
    *   リリースごとにZIPのURLとMD5ハッシュを紐付け、JSONを出力します。
    *   MD5ファイルが見つからない古いリリースに対しては、チェックサムを `0` として処理を継続します。

## トラブルシューティング

### 更新ワークフローが動かない
*   `update-repo.yaml` は `workflow_run` トリガーを使用しています。これが正しく動作するには、`github-release.yaml` がデフォルトブランチ（`main`）に存在する必要があります。

### チェックサムが 0 になる
*   リリースのアセットに `.md5` ファイルが含まれていない場合、スクリプトは `0` を設定します。
*   `github-release.yaml` に `md5sum` コマンドによる生成ステップが含まれているか確認してください。

### ローカルでの検証
スクリプトはローカルでも実行可能です（`GITHUB_TOKEN` 環境変数が必要）。
```bash
export GITHUB_REPOSITORY=CassisCloud/jellyfin-plugin-animethemes-sync
export GITHUB_TOKEN=your_token
python .github/scripts/generate_manifest.py
```
