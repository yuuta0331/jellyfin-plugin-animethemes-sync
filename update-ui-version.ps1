param(
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: ./update-ui-version.ps1"
    Write-Host "Generates a new date-based UI asset version during development."
    exit 0
}

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$constantsPath = Join-Path $root "AnimeThemesSync.Shared\Constants.cs"
$jellyfinBrowser = Join-Path $root "Jellyfin.Plugin.AnimeThemesSync\Configuration\browserPage.html"
$embyBrowser = Join-Path $root "Emby.Plugin.AnimeThemesSync\Configuration\browserPage.html"
$jellyfinConfig = Join-Path $root "Jellyfin.Plugin.AnimeThemesSync\Configuration\configPage.html"
$embyConfig = Join-Path $root "Emby.Plugin.AnimeThemesSync\Configuration\configPage.html"

$constantsContent = Get-Content -LiteralPath $constantsPath -Raw -Encoding UTF8
$match = [regex]::Match($constantsContent, 'UiAssetVersion\s*=\s*"([^"]+)"')
if (-not $match.Success) { throw "Could not find UiAssetVersion in Constants.cs" }
$currentKey = $match.Groups[1].Value

$today = Get-Date -Format "yyyyMMdd"
if ($currentKey.StartsWith($today) -and $currentKey.Length -ge 9) {
    $currentLetter = $currentKey.Substring(8)
    $newLetter = [char]([byte][char]$currentLetter[0] + 1)
    $newKey = $today + $newLetter
} else {
    $newKey = $today + "a"
}

$year = $today.Substring(0, 4)
$month = $today.Substring(4, 2)
$day = $today.Substring(6, 2)
$rev = $newKey.Substring(8)
$uiLabel = "$year.$month.$day-$rev"

Write-Host "Updating: $currentKey -> $newKey (UI: $uiLabel)"

$constantsContent = $constantsContent -replace ('UiAssetVersion\s*=\s*"' + [regex]::Escape($currentKey) + '"'), ('UiAssetVersion = "' + $newKey + '"')
Set-Content -LiteralPath $constantsPath -Value $constantsContent -NoNewline -Encoding UTF8

foreach ($file in @($jellyfinBrowser, $embyBrowser)) {
    $content = Get-Content -LiteralPath $file -Raw -Encoding UTF8
    $content = $content -replace $currentKey, $newKey
    $content = $content -replace '(?<=<div class="fieldDescription">)(?:Version|UI version):[^<]+(?=</div>)', "UI version: $uiLabel."
    Set-Content -LiteralPath $file -Value $content -NoNewline -Encoding UTF8
}

foreach ($file in @($jellyfinConfig, $embyConfig)) {
    $content = Get-Content -LiteralPath $file -Raw -Encoding UTF8
    $content = $content -replace $currentKey, $newKey
    Set-Content -LiteralPath $file -Value $content -NoNewline -Encoding UTF8
}

Write-Host "Done. New UI asset version: $newKey"
