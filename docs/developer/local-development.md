# Local Development

## Prerequisites

- .NET SDK 9.x
- Git
- (任意) Jellyfin 10.11.x / Emby 4.8.x のローカル環境

## Build

```bash
# Jellyfin
dotnet build Jellyfin.Plugin.AnimeThemesSync/Jellyfin.Plugin.AnimeThemesSync.csproj -c Release /p:NuGetAudit=false

# Emby
dotnet build Emby.Plugin.AnimeThemesSync/Emby.Plugin.AnimeThemesSync.csproj -c Release /p:NuGetAudit=false
```

## Test

```bash
dotnet test Jellyfin.Plugin.AnimeThemesSync.Tests/Jellyfin.Plugin.AnimeThemesSync.Tests.csproj -c Release /p:NuGetAudit=false /p:UseSharedCompilation=false -m:1
```

補足:

- 環境によって `obj` の DLL ロックが発生する場合があるため、テスト時は `UseSharedCompilation=false` を推奨します。
- ネットワーク環境によって NuGet 脆弱性 API が不安定な場合があるため、必要に応じて `NuGetAudit=false` を指定します。

## Debug / Deploy

- Jellyfin 側は `CopyToTestyAfterBuild` を `true` にすると、指定ローカル環境へ post-build コピーできます。
- Emby 側は `bin/Release/net8.0` の成果物を手動配置します。

## Coding Rules

- 共通ロジックは `AnimeThemesSync.Shared` を優先
- Provider ID / site key は `AnimeThemesSync.Shared/Constants.cs` を利用
- サービス層の API 呼び出しは `RateLimiter` を介して制限管理
- 変更後は最低限 Jellyfin build + tests を通す
