param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,

    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: ./update-version.ps1 -Version 2.4.0"
    Write-Host "Syncs build metadata and UI for a release."
    exit 0
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be in X.Y.Z format (e.g., 2.4.0)"
}

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$constantsPath = Join-Path $root "AnimeThemesSync.Shared\Constants.cs"
$buildYaml = Join-Path $root "build.yaml"
$propsPath = Join-Path $root "Directory.Build.props"
$jellyfinBrowser = Join-Path $root "Jellyfin.Plugin.AnimeThemesSync\Configuration\browserPage.html"
$embyBrowser = Join-Path $root "Emby.Plugin.AnimeThemesSync\Configuration\browserPage.html"
$jellyfinConfig = Join-Path $root "Jellyfin.Plugin.AnimeThemesSync\Configuration\configPage.html"
$embyConfig = Join-Path $root "Emby.Plugin.AnimeThemesSync\Configuration\configPage.html"

$cacheKey = "v" + ($Version -replace '\.', '')

Write-Host "Release version: $Version"
Write-Host "Cache key:       $cacheKey"

$constantsContent = Get-Content -LiteralPath $constantsPath -Raw -Encoding UTF8
$oldKeyMatch = [regex]::Match($constantsContent, 'UiAssetVersion\s*=\s*"([^"]+)"')
if (-not $oldKeyMatch.Success) { throw "Could not find UiAssetVersion in Constants.cs" }
$oldKey = $oldKeyMatch.Groups[1].Value

$constantsContent = $constantsContent -replace ('UiAssetVersion\s*=\s*"[^"]*"'), ('UiAssetVersion = "' + $cacheKey + '"')
$constantsContent = $constantsContent -replace ('PluginVersion\s*=\s*"[^"]*"'), ('PluginVersion = "' + $Version + '"')
Set-Content -LiteralPath $constantsPath -Value $constantsContent -NoNewline -Encoding UTF8

$buildContent = Get-Content -LiteralPath $buildYaml -Raw -Encoding UTF8
$buildContent = $buildContent -replace ('version:\s*"[^"]*"'), ('version: "' + $Version + '"')
Set-Content -LiteralPath $buildYaml -Value $buildContent -NoNewline -Encoding UTF8

$assemblyVersion = "$Version.0"
$propsContent = Get-Content -LiteralPath $propsPath -Raw -Encoding UTF8
$propsContent = $propsContent -replace '(?<=<Version Condition="''\$\(Version\)'' == ''''">)[^<]+(?=</Version>)', $Version
$propsContent = $propsContent -replace '(?<=<AssemblyVersion Condition="''\$\(AssemblyVersion\)'' == ''''">)[^<]+(?=</AssemblyVersion>)', $assemblyVersion
$propsContent = $propsContent -replace '(?<=<FileVersion Condition="''\$\(FileVersion\)'' == ''''">)[^<]+(?=</FileVersion>)', $assemblyVersion
Set-Content -LiteralPath $propsPath -Value $propsContent -NoNewline -Encoding UTF8

foreach ($file in @($jellyfinBrowser, $embyBrowser)) {
    $content = Get-Content -LiteralPath $file -Raw -Encoding UTF8
    $content = $content -replace $oldKey, $cacheKey
    $content = $content -replace '(?<=<div class="fieldDescription">)(?:Version|UI version):[^<]+(?=</div>)', "Version: $Version"
    Set-Content -LiteralPath $file -Value $content -NoNewline -Encoding UTF8
}

foreach ($file in @($jellyfinConfig, $embyConfig)) {
    $content = Get-Content -LiteralPath $file -Raw -Encoding UTF8
    $content = $content -replace $oldKey, $cacheKey
    Set-Content -LiteralPath $file -Value $content -NoNewline -Encoding UTF8
}

Write-Host "Done. Release version: $Version (cache: $cacheKey)"
