Directory structure:
└── metatube-community-jellyfin-plugin-metatube/
    ├── README.ja.md
    ├── README.md
    ├── jellyfin-plugin-metatube.sln
    ├── LICENSE
    ├── Jellyfin.Plugin.MetaTube/
    │   ├── ApiClient.cs
    │   ├── Jellyfin.Plugin.MetaTube.csproj
    │   ├── Plugin.cs
    │   ├── Configuration/
    │   │   ├── configPage.html
    │   │   └── PluginConfiguration.cs
    │   ├── Extensions/
    │   │   ├── DateTimeExtensions.cs
    │   │   ├── EmbyExtensions.cs
    │   │   ├── EnumerableExtensions.cs
    │   │   ├── JellyfinExtensions.cs
    │   │   └── ProviderIdsExtensions.cs
    │   ├── ExternalIds/
    │   │   ├── ActorExternalId.cs
    │   │   ├── BaseExternalId.cs
    │   │   ├── MovieExternalId.cs
    │   │   └── TrailerExternalId.cs
    │   ├── Helpers/
    │   │   ├── Levenshtein.cs
    │   │   ├── ProviderId.cs
    │   │   └── SubstitutionTable.cs
    │   ├── Metadata/
    │   │   ├── ActorInfo.cs
    │   │   ├── ActorSearchResult.cs
    │   │   ├── ErrorInfo.cs
    │   │   ├── MovieInfo.cs
    │   │   ├── MovieSearchResult.cs
    │   │   ├── ProviderInfo.cs
    │   │   ├── ResponseInfo.cs
    │   │   └── TranslationInfo.cs
    │   ├── Providers/
    │   │   ├── ActorImageProvider.cs
    │   │   ├── ActorProvider.cs
    │   │   ├── BaseProvider.cs
    │   │   ├── ExternalUrlProvider.cs
    │   │   ├── MovieImageProvider.cs
    │   │   └── MovieProvider.cs
    │   ├── ScheduledTasks/
    │   │   ├── GenerateTrailersTask.cs
    │   │   ├── OrganizeMetadataTask.cs
    │   │   └── UpdatePluginTask.cs
    │   └── Translation/
    │       ├── TranslationEngine.cs
    │       ├── TranslationHelper.cs
    │       └── TranslationMode.cs
    ├── scripts/
    │   └── manifest.py
    └── .github/
        ├── FUNDING.yml
        ├── ISSUE_TEMPLATE/
        │   ├── bug_report.yml
        │   ├── config.yml
        │   └── feature_request.yml
        └── workflows/
            ├── dotnetcore.yml
            └── stale.yml


Files Content:

================================================
FILE: README.ja.md
================================================
<h1 align="center">Jellyfin Plugin MetaTube</h1>
<p align="center"><b><a href="README.md">English</a> | 日本語</b></p>

<p align="center">
<img alt="Plugin Banner" src="https://metatube-community.github.io/images/banner-dark.png"/>
<br/>
<br/>

<a href="https://github.com/metatube-community/jellyfin-plugin-metatube/actions">
<img alt="GitHub Workflow Status" src="https://img.shields.io/github/actions/workflow/status/metatube-community/jellyfin-plugin-metatube/dotnetcore.yml?branch=main&logo=github">
</a>
<a href="https://github.com/metatube-community/jellyfin-plugin-metatube/search?l=c%23">
<img alt="GitHub top language" src="https://img.shields.io/github/languages/top/metatube-community/jellyfin-plugin-metatube?color=%23239120&label=.NET&logo=csharp">
</a>
<a href="https://github.com/metatube-community/jellyfin-plugin-metatube/blob/main/LICENSE">
<img alt="License" src="https://img.shields.io/github/license/metatube-community/jellyfin-plugin-metatube">
</a>
<a href="https://github.com/metatube-community/jellyfin-plugin-metatube">
<img alt="gitHub Stars" src="https://img.shields.io/github/stars/metatube-community/jellyfin-plugin-metatube?style=flat">
</a>
<a href="https://github.com/metatube-community/jellyfin-plugin-metatube">
<img alt="Downloads" src="https://img.shields.io/github/downloads/metatube-community/jellyfin-plugin-metatube/total">
</a>
<a href="https://github.com/metatube-community/jellyfin-plugin-metatube/releases">
<img alt="Releases" src="https://img.shields.io/github/v/release/metatube-community/jellyfin-plugin-metatube?include_prereleases&logo=smartthings">
</a>
</p>

## 概要

Jellyfin／Emby 向けに開発された、とても便利なメタデータプラグインです。

## 特徴

- 完全なデータ：タイトル、概要、出演者、タグ、評価 などを含む豊富な情報を提供。
- 強力な検索機能：多数のスクレイピングソースから作品や俳優情報を検索可能。
- トレーラー機能：動画をダウンロードせずに オンラインで予告編を視聴。
- スケジュールタスク：自動的に作品タグを整理し、バックグラウンドでプラグインを更新。
- 顔認識機能：内蔵の顔認識により、顔を中心にポスター画像を自動トリミング。
- 自動翻訳：特定のメタデータ内容を必要な言語に翻訳可能。

## 対応プラットフォーム

[![Jellyfin](https://img.shields.io/static/v1?color=%2300A4DC&style=for-the-badge&label=Jellyfin&logo=jellyfin&message=10.11.x)](https://jellyfin.org/)
[![Emby](https://img.shields.io/static/v1?color=%2352B54B&style=for-the-badge&label=Emby&logo=emby&message=4.9.x)](https://emby.media/)

_※本プロジェクトは Jellyfin／Emby の安定版のみをサポートしています。_

## ドキュメント

- [プラグインのインストール](https://metatube-community.github.io/wiki/plugin-installation/)
- [バックエンドのデプロイ](https://metatube-community.github.io/wiki/server-deployment/)
- [命名規則](https://metatube-community.github.io/wiki/naming-rules/)
- [自動翻訳](https://metatube-community.github.io/wiki/auto-translation/)
- [ソースからのビルド](https://metatube-community.github.io/wiki/build-from-source/)
- [データソース](https://metatube-community.github.io/wiki/metadata-providers/)

詳細な使い方や解説は [Wiki](https://metatube-community.github.io/wiki/) をご参照ください。

## コミュニティ

質問や提案などは、[Discussions](https://github.com/metatube-community/jellyfin-plugin-metatube/discussions) にてお気軽にどうぞ。

## ライセンス

本プラグインは [MIT](https://github.com/metatube-community/jellyfin-plugin-metatube/blob/main/LICENSE) ライセンスの下で公開されています。

## スター履歴

[![Star History Chart](https://api.star-history.com/svg?repos=metatube-community/jellyfin-plugin-metatube&type=Date)](https://star-history.com/#metatube-community/jellyfin-plugin-metatube&Date)



================================================
FILE: README.md
================================================
<h1 align="center">Jellyfin Plugin MetaTube</h1>
<p align="center"><b>English | <a href="README.ja.md">日本語</a></b></p>

<p align="center">
<img alt="Plugin Banner" src="https://metatube-community.github.io/images/banner-dark.png"/>
<br/>
<br/>

<a href="https://github.com/metatube-community/jellyfin-plugin-metatube/actions">
<img alt="GitHub Workflow Status" src="https://img.shields.io/github/actions/workflow/status/metatube-community/jellyfin-plugin-metatube/dotnetcore.yml?branch=main&logo=github">
</a>
<a href="https://github.com/metatube-community/jellyfin-plugin-metatube/search?l=c%23">
<img alt="GitHub top language" src="https://img.shields.io/github/languages/top/metatube-community/jellyfin-plugin-metatube?color=%23239120&label=.NET&logo=csharp">
</a>
<a href="https://github.com/metatube-community/jellyfin-plugin-metatube/blob/main/LICENSE">
<img alt="License" src="https://img.shields.io/github/license/metatube-community/jellyfin-plugin-metatube">
</a>
<a href="https://github.com/metatube-community/jellyfin-plugin-metatube">
<img alt="gitHub Stars" src="https://img.shields.io/github/stars/metatube-community/jellyfin-plugin-metatube?style=flat">
</a>
<a href="https://github.com/metatube-community/jellyfin-plugin-metatube">
<img alt="Downloads" src="https://img.shields.io/github/downloads/metatube-community/jellyfin-plugin-metatube/total">
</a>
<a href="https://github.com/metatube-community/jellyfin-plugin-metatube/releases">
<img alt="Releases" src="https://img.shields.io/github/v/release/metatube-community/jellyfin-plugin-metatube?include_prereleases&logo=smartthings">
</a>
</p>

## About

MetaTube Plugin for Jellyfin/Emby.

## Features

- Full Metadata: Including title, overview, genres, director, actors, and studio.
- Full Search: Support searching for movies and actors across various providers.
- Trailer Video: Support trailers without downloading the full trailer videos.
- Scheduled Task: Automatically organize metadata genres and update plugin.
- Face Detection: Cut primary image with face centered by face detection engine.
- Auto Translation: Support translate certain metadata to preferred language.

## Platforms

[![Jellyfin](https://img.shields.io/static/v1?color=%2300A4DC&style=for-the-badge&label=Jellyfin&logo=jellyfin&message=10.11.x)](https://jellyfin.org/)
[![Emby](https://img.shields.io/static/v1?color=%2352B54B&style=for-the-badge&label=Emby&logo=emby&message=4.9.x)](https://emby.media/)

_NOTE: This project will only support stable versions._

## Documentation

- [Plugin installation](https://metatube-community.github.io/wiki/plugin-installation/)
- [Server deployment](https://metatube-community.github.io/wiki/server-deployment/)
- [File naming rules](https://metatube-community.github.io/wiki/naming-rules/)
- [Auto translation](https://metatube-community.github.io/wiki/auto-translation/)
- [Build from source](https://metatube-community.github.io/wiki/build-from-source/)
- [Metadata providers](https://metatube-community.github.io/wiki/metadata-providers/)

Full documentation and examples can be found at [Wiki](https://metatube-community.github.io/wiki/).

## Community

Welcome and feel free to ask any questions at [Discussions](https://github.com/metatube-community/jellyfin-plugin-metatube/discussions).

## Licence

This plugin is released under the [MIT](https://github.com/metatube-community/jellyfin-plugin-metatube/blob/main/LICENSE) License.

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=metatube-community/jellyfin-plugin-metatube&type=Date)](https://star-history.com/#metatube-community/jellyfin-plugin-metatube&Date)



================================================
FILE: jellyfin-plugin-metatube.sln
================================================
﻿
Microsoft Visual Studio Solution File, Format Version 12.00
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Jellyfin.Plugin.MetaTube", "Jellyfin.Plugin.MetaTube\Jellyfin.Plugin.MetaTube.csproj", "{DD1CDA77-5286-454A-BDCD-866FBE15E740}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
		Debug.Emby|Any CPU = Debug.Emby|Any CPU
		Release.Emby|Any CPU = Release.Emby|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{DD1CDA77-5286-454A-BDCD-866FBE15E740}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{DD1CDA77-5286-454A-BDCD-866FBE15E740}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{DD1CDA77-5286-454A-BDCD-866FBE15E740}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{DD1CDA77-5286-454A-BDCD-866FBE15E740}.Release|Any CPU.Build.0 = Release|Any CPU
		{DD1CDA77-5286-454A-BDCD-866FBE15E740}.Debug.Emby|Any CPU.ActiveCfg = Debug.Emby|Any CPU
		{DD1CDA77-5286-454A-BDCD-866FBE15E740}.Debug.Emby|Any CPU.Build.0 = Debug.Emby|Any CPU
		{DD1CDA77-5286-454A-BDCD-866FBE15E740}.Release.Emby|Any CPU.ActiveCfg = Release.Emby|Any CPU
		{DD1CDA77-5286-454A-BDCD-866FBE15E740}.Release.Emby|Any CPU.Build.0 = Release.Emby|Any CPU
	EndGlobalSection
EndGlobal



================================================
FILE: LICENSE
================================================
MIT License

Copyright (c) 2022 MetaTube Community

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.



================================================
FILE: Jellyfin.Plugin.MetaTube/ApiClient.cs
================================================
using System.Collections.Specialized;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;
using Jellyfin.Plugin.MetaTube.Metadata;
#if __EMBY__
using MediaBrowser.Common.Net;
#endif

namespace Jellyfin.Plugin.MetaTube;

public static class ApiClient
{
    private const string ActorInfoApi = "/v1/actors";
    private const string MovieInfoApi = "/v1/movies";
    private const string ActorSearchApi = "/v1/actors/search";
    private const string MovieSearchApi = "/v1/movies/search";
    private const string PrimaryImageApi = "/v1/images/primary";
    private const string ThumbImageApi = "/v1/images/thumb";
    private const string BackdropImageApi = "/v1/images/backdrop";
    private const string TranslateApi = "/v1/translate";

    private static string ComposeUrl(string path, NameValueCollection nv)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        foreach (string key in nv) query.Add(key, nv.Get(key));

        // Build URL
        var uriBuilder = new UriBuilder(Plugin.Instance.Configuration.Server)
        {
            Path = path,
            Query = query.ToString() ?? string.Empty
        };
        return uriBuilder.ToString();
    }

    private static string ComposeImageApiUrl(string path, string provider, string id, string url = default,
        double ratio = -1, double position = -1, bool auto = false, string badge = default)
    {
        return ComposeUrl(Path.Combine(path, provider, id), new NameValueCollection
        {
            { "url", url },
            { "ratio", ratio.ToString("R") },
            { "pos", position.ToString("R") },
            { "auto", auto.ToString() },
            { "badge", badge },
            { "quality", Plugin.Instance.Configuration.DefaultImageQuality.ToString() }
        });
    }

    private static string ComposeInfoApiUrl(string path, string provider, string id, bool lazy)
    {
        return ComposeUrl(Path.Combine(path, provider, id), new NameValueCollection
        {
            { "lazy", lazy.ToString() }
        });
    }

    private static string ComposeSearchApiUrl(string path, string q, string provider, bool fallback)
    {
        return ComposeUrl(path, new NameValueCollection
        {
            { "q", q },
            { "provider", provider },
            { "fallback", fallback.ToString() }
        });
    }

    private static string ComposeTranslateApiUrl(string path, string q, string from, string to, string engine,
        NameValueCollection nv = null)
    {
        return ComposeUrl(path, new NameValueCollection
        {
            { "q", q },
            { "from", from },
            { "to", to },
            { "engine", engine },
            nv ?? new NameValueCollection()
        });
    }

    public static string GetPrimaryImageApiUrl(string provider, string id, double position = -1, string badge = default)
    {
        return ComposeImageApiUrl(PrimaryImageApi, provider, id,
            ratio: Plugin.Instance.Configuration.PrimaryImageRatio, position: position, badge: badge);
    }

    public static string GetPrimaryImageApiUrl(string provider, string id, string url, double position = -1,
        bool auto = false, string badge = default)
    {
        return ComposeImageApiUrl(PrimaryImageApi, provider, id, url,
            Plugin.Instance.Configuration.PrimaryImageRatio, position, auto, badge);
    }

    public static string GetThumbImageApiUrl(string provider, string id)
    {
        return ComposeImageApiUrl(ThumbImageApi, provider, id);
    }

    public static string GetThumbImageApiUrl(string provider, string id, string url, double position = -1,
        bool auto = false)
    {
        return ComposeImageApiUrl(ThumbImageApi, provider, id, url, position: position, auto: auto);
    }

    public static string GetBackdropImageApiUrl(string provider, string id)
    {
        return ComposeImageApiUrl(BackdropImageApi, provider, id);
    }

    public static string GetBackdropImageApiUrl(string provider, string id, string url, double position = -1,
        bool auto = false)
    {
        return ComposeImageApiUrl(BackdropImageApi, provider, id, url, position: position, auto: auto);
    }

#if __EMBY__
    public static async Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
#else
    public static async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
#endif
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", DefaultUserAgent);
        var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
#if __EMBY__
        return new HttpResponseInfo
        {
            Content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            ContentLength = response.Content.Headers.ContentLength,
            ContentType = response.Content.Headers.ContentType?.ToString(),
            StatusCode = response.StatusCode,
            Headers = response.Content.Headers.ToDictionary(kvp => kvp.Key, kvp => string.Join(", ", kvp.Value))
        };
#else
        return response;
#endif
    }

    public static async Task<ActorInfo> GetActorInfoAsync(string provider, string id,
        CancellationToken cancellationToken)
    {
        return await GetActorInfoAsync(provider, id, true /* default */, cancellationToken);
    }

    public static async Task<ActorInfo> GetActorInfoAsync(string provider, string id, bool lazy,
        CancellationToken cancellationToken)
    {
        var apiUrl = ComposeInfoApiUrl(ActorInfoApi, provider, id, lazy);
        return await GetDataAsync<ActorInfo>(apiUrl, true, cancellationToken);
    }

    public static async Task<MovieInfo> GetMovieInfoAsync(string provider, string id,
        CancellationToken cancellationToken)
    {
        return await GetMovieInfoAsync(provider, id, true /* default */, cancellationToken);
    }

    public static async Task<MovieInfo> GetMovieInfoAsync(string provider, string id, bool lazy,
        CancellationToken cancellationToken)
    {
        var apiUrl = ComposeInfoApiUrl(MovieInfoApi, provider, id, lazy);
        return await GetDataAsync<MovieInfo>(apiUrl, true, cancellationToken);
    }

    public static async Task<List<ActorSearchResult>> SearchActorAsync(string q,
        CancellationToken cancellationToken)
    {
        return await SearchActorAsync(q, string.Empty, cancellationToken);
    }

    public static async Task<List<ActorSearchResult>> SearchActorAsync(string q, string provider,
        CancellationToken cancellationToken)
    {
        return await SearchActorAsync(q, provider, true /* default */, cancellationToken);
    }

    public static async Task<List<ActorSearchResult>> SearchActorAsync(string q, string provider,
        bool fallback, CancellationToken cancellationToken)
    {
        var apiUrl = ComposeSearchApiUrl(ActorSearchApi, q, provider, fallback);
        return await GetDataAsync<List<ActorSearchResult>>(apiUrl, true, cancellationToken);
    }

    public static async Task<List<MovieSearchResult>> SearchMovieAsync(string q,
        CancellationToken cancellationToken)
    {
        return await SearchMovieAsync(q, string.Empty, cancellationToken);
    }

    public static async Task<List<MovieSearchResult>> SearchMovieAsync(string q, string provider,
        CancellationToken cancellationToken)
    {
        return await SearchMovieAsync(q, provider, true /* default */, cancellationToken);
    }

    public static async Task<List<MovieSearchResult>> SearchMovieAsync(string q, string provider,
        bool fallback, CancellationToken cancellationToken)
    {
        var apiUrl = ComposeSearchApiUrl(MovieSearchApi, q, provider, fallback);
        return await GetDataAsync<List<MovieSearchResult>>(apiUrl, true, cancellationToken);
    }

    public static async Task<TranslationInfo> TranslateAsync(string q, string from, string to, string engine,
        NameValueCollection nv, CancellationToken cancellationToken)
    {
        var apiUrl = ComposeTranslateApiUrl(TranslateApi, q, from, to, engine, nv);
        return await GetDataAsync<TranslationInfo>(apiUrl, false, cancellationToken);
    }

    private static async Task<T> GetDataAsync<T>(string url, bool requireAuth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Add General Headers.
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("User-Agent", DefaultUserAgent);

        // Set API Authorization Token.
        if (requireAuth && !string.IsNullOrWhiteSpace(Plugin.Instance.Configuration.Token))
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", Plugin.Instance.Configuration.Token);

        var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Nullable forgiving reason:
        // Response is unlikely to be null.
        // If it happens to be null, an exception is planed to be thrown either way.
        var apiResponse = (await response.Content!
            .ReadFromJsonAsync<ResponseInfo<T>>(cancellationToken: cancellationToken).ConfigureAwait(false))!;

        // EnsureSuccessStatusCode ignoring reason:
        // When the status is unsuccessful, the API response contains error details.
        if (!response.IsSuccessStatusCode && apiResponse.Error != null)
            throw new Exception($"API request error: {apiResponse.Error.Code} ({apiResponse.Error.Message})");

        // Note: data field must not be null if there are no errors.
        if (apiResponse.Data == null)
            throw new Exception("Response data field is null");

        return apiResponse.Data;
    }

    #region Http

    private static readonly HttpClient HttpClient;
    private static string DefaultUserAgent => $"{Plugin.ProviderName}/{Plugin.Instance.Version}";

    static ApiClient()
    {
        HttpClient = new HttpClient(new SocketsHttpHandler
        {
            // Connect Timeout.
            ConnectTimeout = TimeSpan.FromSeconds(30),

            // TCP Keep Alive.
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
            KeepAlivePingDelay = TimeSpan.FromSeconds(30),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),

            // Connection Pooling.
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(90)
        });
    }

    #endregion
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Jellyfin.Plugin.MetaTube.csproj
================================================
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <ImplicitUsings>enable</ImplicitUsings>
        <Configurations>Debug;Release;Debug.Emby;Release.Emby</Configurations>
        <Platforms>AnyCPU</Platforms>
        <AssemblyName>MetaTube</AssemblyName>
        <Authors>MetaTube</Authors>
        <Description>MetaTube Plugin for Jellyfin/Emby</Description>
        <Version>$([System.DateTime]::UtcNow.ToString(yyyy.Mdd.Hmm.0))</Version>
        <Copyright>Copyright © 2023 MetaTube</Copyright>
        <RepositoryType>Git</RepositoryType>
        <RepositoryUrl>https://github.com/metatube-community/jellyfin-plugin-metatube.git</RepositoryUrl>
        <PackageProjectUrl>https://github.com/metatube-community/jellyfin-plugin-metatube</PackageProjectUrl>
        <PackageLicenseUrl>https://github.com/metatube-community/jellyfin-plugin-metatube/blob/main/LICENSE</PackageLicenseUrl>
        <PackageIcon>thumb.png</PackageIcon>
        <PackageId>MetaTube</PackageId>
        <Company>MetaTube</Company>
        <Product>MetaTube</Product>
    </PropertyGroup>

    <PropertyGroup>
        <TargetFramework Condition="'$(Configuration)'=='Debug' or '$(Configuration)'=='Release'">net9.0</TargetFramework>
        <TargetFramework Condition="'$(Configuration)'=='Debug.Emby' or '$(Configuration)'=='Release.Emby'">net8.0</TargetFramework>
    </PropertyGroup>

    <PropertyGroup Condition="'$(Configuration)'=='Debug.Emby' or '$(Configuration)'=='Release.Emby'">
        <DefineConstants>__EMBY__</DefineConstants>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="System.Memory" Version="4.5.5"/>
    </ItemGroup>

    <ItemGroup Condition="'$(Configuration)'=='Debug' or '$(Configuration)'=='Release'">
        <PackageReference Include="Jellyfin.Controller" Version="10.11.0"/>
        <PackageReference Include="Jellyfin.Model" Version="10.11.0"/>
    </ItemGroup>

    <ItemGroup Condition="'$(Configuration)'=='Debug.Emby' or '$(Configuration)'=='Release.Emby'">
        <PackageReference Include="MediaBrowser.Server.Core" Version="4.9.1.80"/>
    </ItemGroup>

    <ItemGroup Condition="'$(Configuration)'=='Debug' or '$(Configuration)'=='Release'">
        <None Remove="Configuration\configPage.html"/>
        <EmbeddedResource Include="Configuration\configPage.html"/>
    </ItemGroup>

    <ItemGroup Condition="'$(Configuration)'=='Debug.Emby' or '$(Configuration)'=='Release.Emby'">
        <None Remove="thumb.png"/>
        <EmbeddedResource Include="thumb.png"/>
    </ItemGroup>

    <Target Name="Zip" AfterTargets="PostBuildEvent" Condition="'$(Configuration)'=='Release' or '$(Configuration)'=='Release.Emby'">
        <ItemGroup>
            <FilesToDelete Include="$(BaseOutputPath)Jellyfin.MetaTube*.zip" Condition="'$(Configuration)'=='Release'"/>
            <FilesToDelete Include="$(BaseOutputPath)Emby.MetaTube*.zip" Condition="'$(Configuration)'=='Release.Emby'"/>
            <TempZipDirectory Include="$(OutputPath)output"/>
        </ItemGroup>
        <Delete Files="@(FilesToDelete)"/>
        <Copy SourceFiles="$(OutputPath)$(AssemblyName).dll" DestinationFolder="@(TempZipDirectory)"/>
        <ZipDirectory SourceDirectory="@(TempZipDirectory)" DestinationFile="$(BaseOutputPath)Jellyfin.MetaTube@v$(Version).zip" Condition="'$(Configuration)'=='Release'"/>
        <ZipDirectory SourceDirectory="@(TempZipDirectory)" DestinationFile="$(BaseOutputPath)Emby.MetaTube@v$(Version).zip" Condition="'$(Configuration)'=='Release.Emby'"/>
        <RemoveDir Directories="@(TempZipDirectory)"/>
    </Target>

</Project>



================================================
FILE: Jellyfin.Plugin.MetaTube/Plugin.cs
================================================
using Jellyfin.Plugin.MetaTube.Configuration;
using MediaBrowser.Common.Plugins;
#if __EMBY__
using MediaBrowser.Common;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Drawing;

#else
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Configuration;
#endif

namespace Jellyfin.Plugin.MetaTube;

#if __EMBY__
public class Plugin : BasePluginSimpleUI<PluginConfiguration>, IHasThumbImage
{
    public Plugin(IApplicationHost applicationHost) : base(applicationHost)
    {
        Instance = this;
    }
#else
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths,
        xmlSerializer)
    {
        Instance = this;
    }
#endif

    public const string ProviderName = "MetaTube";

    public const string ProviderId = "MetaTube";

    public override string Name => ProviderName;

    public override string Description => "MetaTube Plugin for Jellyfin/Emby";

    public override Guid Id => Guid.Parse("01cc53ec-c415-4108-bbd4-a684a9801a32");

    public static Plugin Instance { get; private set; }

#if !__EMBY__
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            }
        };
    }
#endif

#if __EMBY__
    public PluginConfiguration Configuration => GetOptions();

    public Stream GetThumbImage()
    {
        return GetType().Assembly.GetManifestResourceStream($"{GetType().Namespace}.thumb.png");
    }

    public ImageFormat ThumbImageFormat => ImageFormat.Png;
#endif
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Configuration/configPage.html
================================================
<!DOCTYPE html>
<html lang="en">

<head>
    <title>MetaTube</title>
</head>

<body>
<div class="page type-interior pluginConfigurationPage MetaTubeConfigurationPage" data-require="emby-input,emby-button,emby-select,emby-checkbox,emby-linkbutton" data-role="page">
    <div data-role="content">
        <div class="content-primary">

            <h1>MetaTube</h1>

            <div class="readOnlyContent">
                <p class="description1">MetaTube Plugin for Jellyfin/Emby. <a class="button-link emby-button" href="https://metatube-community.github.io" is="emby-linkbutton" target="_blank">Learn more</a>.</p>
            </div>

            <form class="MetaTubeConfigurationForm">

                <div class="verticalSection">
                    <h2>General</h2>

                    <div class="inputContainer">
                        <label class="inputLabel inputLabelUnfocused" for="txtServer">Server:</label>
                        <input id="txtServer" is="emby-input" name="txtServer" pattern="^https?://.+$" required
                               type="text"/>
                        <div class="fieldDescription">Full url of the MetaTube Server, HTTPS protocol is recommended.</div>
                    </div>

                    <div class="inputContainer">
                        <label class="inputLabel inputLabelUnfocused" for="txtToken">Token:</label>
                        <input id="txtToken" is="emby-input" name="txtToken" type="text"/>
                        <div class="fieldDescription">Access token for the MetaTube Server, or blank if no token is set by the backend.</div>
                    </div>

                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnableCollections" is="emby-checkbox" name="chkEnableCollections" type="checkbox"/>
                            <span>Enable collections</span>
                        </label>
                        <div class="fieldDescription checkboxFieldDescription">Automatically create collections by series.</div>
                    </div>

                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnableDirectors" is="emby-checkbox" name="chkEnableDirectors" type="checkbox"/>
                            <span>Enable directors</span>
                        </label>
                        <div class="fieldDescription checkboxFieldDescription">Add directors to corresponding video metadata.</div>
                    </div>

                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnableRatings" is="emby-checkbox" name="chkEnableRatings" type="checkbox"/>
                            <span>Enable ratings</span>
                        </label>
                        <div class="fieldDescription checkboxFieldDescription">Display community ratings from the original website.</div>
                    </div>

                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnableTrailers" is="emby-checkbox" name="chkEnableTrailers" type="checkbox"/>
                            <span>Enable trailers</span>
                        </label>
                        <div class="fieldDescription checkboxFieldDescription">Generate online video trailers in strm format.</div>
                    </div>

                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnableRealActorNames" is="emby-checkbox" name="chkEnableRealActorNames" type="checkbox"/>
                            <span>Enable real actor names</span>
                        </label>
                        <div class="fieldDescription checkboxFieldDescription">Search and replace with real actor names from AVBASE.</div>
                    </div>
                </div>

                <div class="verticalSection">
                    <h2 class="sectionTitle">Badge</h2>

                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnableBadges" is="emby-checkbox" name="chkEnableBadges" type="checkbox"/>
                            <span>Enable badges</span>
                        </label>
                        <div class="fieldDescription checkboxFieldDescription">Add Chinese subtitle badges to primary images.</div>
                    </div>

                    <div class="inputContainer">
                        <label class="inputLabel inputLabelUnfocused" for="txtBadgeUrl">Badge url:</label>
                        <input id="txtBadgeUrl" is="emby-input" name="txtBadgeUrl" type="text"/>
                        <div class="fieldDescription">Custom badge url, PNG format is recommended. (default: zimu.png)</div>
                    </div>
                </div>

                <div class="verticalSection">
                    <h2 class="sectionTitle">Image</h2>

                    <div class="inputContainer">
                        <label class="inputLabel inputLabelUnfocused" for="txtPrimaryImageRatio">Primary image ratio:</label>
                        <input id="txtPrimaryImageRatio" is="emby-input" name="txtPrimaryImageRatio" step="any" type="number"/>
                        <div class="fieldDescription">Aspect ratio for primary images, set a negative value to use the default.</div>
                    </div>

                    <div class="inputContainer">
                        <label class="inputLabel inputLabelUnfocused" for="txtDefaultImageQuality">Default image quality:</label>
                        <input id="txtDefaultImageQuality" is="emby-input" max="100" min="0" name="txtDefaultImageQuality" step="1" type="number"/>
                        <div class="fieldDescription">Default compression quality for JPEG images, set between 0 and 100. (default: 90)</div>
                    </div>
                </div>

                <div class="verticalSection">
                    <h2 class="sectionTitle">Provider</h2>

                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnableMovieProviderFilter" is="emby-checkbox" name="chkEnableMovieProviderFilter" type="checkbox"/>
                            <span>Enable movie provider filter</span>
                        </label>
                        <div class="fieldDescription checkboxFieldDescription">Filter and reorder search results from movie providers.</div>
                    </div>

                    <div class="inputContainer">
                        <label class="inputLabel inputLabelUnfocused" for="txtRawMovieProviderFilter">Movie provider filter:</label>
                        <input id="txtRawMovieProviderFilter" is="emby-input" name="txtRawMovieProviderFilter" type="text"/>
                        <div class="fieldDescription">Provider names are case-insensitive, with decreasing precedence from left to right, separated by commas.</div>
                    </div>
                </div>

                <div class="verticalSection">
                    <h2 class="sectionTitle">Template</h2>

                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnableTemplate" is="emby-checkbox" name="chkEnableTemplate" type="checkbox"/>
                            <span>Enable template</span>
                        </label>
                        <div class="fieldDescription checkboxFieldDescription">Predefined template variables can be found <a class="button-link emby-button" href="https://metatube-community.github.io/wiki/text-template/" is="emby-linkbutton" target="_blank">here</a>.</div>
                    </div>

                    <div class="inputContainer">
                        <label class="inputLabel inputLabelUnfocused" for="txtNameTemplate">Name template:</label>
                        <input id="txtNameTemplate" is="emby-input" name="txtNameTemplate" type="text"/>
                        <div class="fieldDescription"></div>
                    </div>

                    <div class="inputContainer">
                        <label class="inputLabel inputLabelUnfocused" for="txtTaglineTemplate">Tagline template:</label>
                        <input id="txtTaglineTemplate" is="emby-input" name="txtTaglineTemplate" type="text"/>
                        <div class="fieldDescription"></div>
                    </div>
                </div>

                <div class="verticalSection">
                    <h2 class="sectionTitle">Translation</h2>

                    <div class="selectContainer">
                        <label class="selectLabel selectLabelText" for="selectTranslationMode">Translation mode:</label>
                        <select class="emby-select-withcolor emby-select selectTranslationMode" id="selectTranslationMode" is="emby-select" name="selectTranslationMode">
                            <option value="Disabled">Disabled</option>
                            <option value="Title">Title</option>
                            <option value="Summary">Summary</option>
                            <option value="Both">Title and Summary</option>
                        </select>
                    </div>

                    <div class="selectTranslationModeEnabled">
                        <div class="selectContainer">
                            <label class="selectLabel selectLabelText" for="selectTranslationEngine">Translation engine:</label>
                            <select class="emby-select-withcolor emby-select selectTranslationEngine" id="selectTranslationEngine" is="emby-select" name="selectTranslationEngine">
                                <option value="Baidu">Baidu</option>
                                <option value="Google">Google</option>
                                <option value="GoogleFree">Google (Free)</option>
                                <option value="DeepL">DeepL</option>
                                <option value="OpenAi">OpenAI</option>
                            </select>
                        </div>

                        <div class="selectTranslationEngineBaidu">
                            <div class="inputContainer">
                                <label class="inputLabel inputLabelUnfocused" for="txtBaiduAppId">Baidu app id:</label>
                                <input id="txtBaiduAppId" is="emby-input" name="txtBaiduAppId" type="text"/>
                                <div class="fieldDescription"></div>
                            </div>

                            <div class="inputContainer">
                                <label class="inputLabel inputLabelUnfocused" for="txtBaiduAppKey">Baidu app key:</label>
                                <input id="txtBaiduAppKey" is="emby-input" name="txtBaiduAppKey" type="text"/>
                                <div class="fieldDescription"></div>
                            </div>
                        </div>

                        <div class="selectTranslationEngineGoogle">
                            <div class="inputContainer">
                                <label class="inputLabel inputLabelUnfocused" for="txtGoogleApiKey">Google api key:</label>
                                <input id="txtGoogleApiKey" is="emby-input" name="txtGoogleApiKey" type="text"/>
                                <div class="fieldDescription"></div>
                            </div>
                            <div class="inputContainer">
                                <label class="inputLabel inputLabelUnfocused" for="txtGoogleApiUrl">Google api url:</label>
                                <input id="txtGoogleApiUrl" is="emby-input" name="txtGoogleApiUrl" type="text"/>
                                <div class="fieldDescription">Custom Google translate api url. (optional)</div>
                            </div>
                        </div>

                        <div class="selectTranslationEngineDeepL">
                            <div class="inputContainer">
                                <label class="inputLabel inputLabelUnfocused" for="txtDeepLApiKey">DeepL api key:</label>
                                <input id="txtDeepLApiKey" is="emby-input" name="txtDeepLApiKey" type="text"/>
                                <div class="fieldDescription"></div>
                            </div>
                            <div class="inputContainer">
                                <label class="inputLabel inputLabelUnfocused" for="txtDeepLApiUrl">DeepL api url:</label>
                                <input id="txtDeepLApiUrl" is="emby-input" name="txtDeepLApiUrl" type="text"/>
                                <div class="fieldDescription">Custom DeepL-compatible api url. (optional)</div>
                            </div>
                        </div>

                        <div class="selectTranslationEngineOpenAi">
                            <div class="inputContainer">
                                <label class="inputLabel inputLabelUnfocused" for="txtOpenAiApiKey">OpenAI api key:</label>
                                <input id="txtOpenAiApiKey" is="emby-input" name="txtOpenAiApiKey" type="text"/>
                                <div class="fieldDescription"></div>
                            </div>
                            <div class="inputContainer">
                                <label class="inputLabel inputLabelUnfocused" for="txtOpenAiApiUrl">OpenAI api url:</label>
                                <input id="txtOpenAiApiUrl" is="emby-input" name="txtOpenAiApiUrl" type="text"/>
                                <div class="fieldDescription">Custom OpenAI-compatible api url. (optional)</div>
                            </div>
                            <div class="inputContainer">
                                <label class="inputLabel inputLabelUnfocused" for="txtOpenAiModel">OpenAI model:</label>
                                <input id="txtOpenAiModel" is="emby-input" name="txtOpenAiModel" type="text"/>
                                <div class="fieldDescription">Custom OpenAI-compatible api model. (optional)</div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="verticalSection">
                    <h2 class="sectionTitle">Substitution</h2>

                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnableTitleSubstitution" is="emby-checkbox" name="chkEnableTitleSubstitution" type="checkbox"/>
                            <span>Enable title substitution</span>
                        </label>
                        <div class="fieldDescription checkboxFieldDescription"></div>
                    </div>

                    <div class="inputContainer">
                        <label class="inputLabel inputLabel-float inputLabelUnfocused" for="txtTitleRawSubstitutionTable">Title substitution table:</label>
                        <textarea class="emby-input" id="txtTitleRawSubstitutionTable" is="emby-input" name="txtTitleRawSubstitutionTable" rows="5"></textarea>
                        <div class="fieldDescription">One record per line, separated by equal signs. Leave the target substring blank to delete the source substring.</div>
                    </div>

                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnableActorSubstitution" is="emby-checkbox" name="chkEnableActorSubstitution" type="checkbox"/>
                            <span>Enable actor substitution</span>
                        </label>
                        <div class="fieldDescription checkboxFieldDescription"></div>
                    </div>

                    <div class="inputContainer">
                        <label class="inputLabel inputLabel-float inputLabelUnfocused" for="txtActorRawSubstitutionTable">Actor substitution table:</label>
                        <textarea class="emby-input" id="txtActorRawSubstitutionTable" is="emby-input" name="txtActorRawSubstitutionTable" rows="5"></textarea>
                        <div class="fieldDescription">One record per line, separated by equal signs. Leave the target actor blank to delete the source actor.</div>
                    </div>

                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnableGenreSubstitution" is="emby-checkbox" name="chkEnableGenreSubstitution" type="checkbox"/>
                            <span>Enable genre substitution</span>
                        </label>
                        <div class="fieldDescription checkboxFieldDescription"></div>
                    </div>

                    <div class="inputContainer">
                        <label class="inputLabel inputLabel-float inputLabelUnfocused" for="txtGenreRawSubstitutionTable">Genre substitution table:</label>
                        <textarea class="emby-input" id="txtGenreRawSubstitutionTable" is="emby-input" name="txtGenreRawSubstitutionTable" rows="5"></textarea>
                        <div class="fieldDescription">One record per line, separated by equal signs. Leave the target genre blank to delete the source genre.</div>
                    </div>
                </div>

                <div>
                    <button class="raised button-submit block" is="emby-button" type="submit">
                        <span>Save</span></button>
                </div>

            </form>
        </div>
    </div>
    <script type="text/javascript">
        var MetaTubePluginConfig = {
            pluginUniqueId: "01cc53ec-c415-4108-bbd4-a684a9801a32"
        };

        $('.selectTranslationMode').on('change', function (_) {
            if (this.value === "Disabled") {
                $('.selectTranslationModeEnabled').css('display', 'none');
            } else {
                $('.selectTranslationModeEnabled').css('display', 'inherit');
            }
        });

        $('.selectTranslationEngine').on('change', function (_) {
            if (this.value === "Baidu") {
                $('.selectTranslationEngineBaidu').css('display', 'inherit');
                $('.selectTranslationEngineGoogle').css('display', 'none');
                $('.selectTranslationEngineDeepL').css('display', 'none');
                $('.selectTranslationEngineOpenAi').css('display', 'none');
            } else if (this.value === "Google") {
                $('.selectTranslationEngineBaidu').css('display', 'none');
                $('.selectTranslationEngineGoogle').css('display', 'inherit');
                $('.selectTranslationEngineDeepL').css('display', 'none');
                $('.selectTranslationEngineOpenAi').css('display', 'none');
            } else if (this.value === "GoogleFree") {
                $('.selectTranslationEngineBaidu').css('display', 'none');
                $('.selectTranslationEngineGoogle').css('display', 'none');
                $('.selectTranslationEngineDeepL').css('display', 'none');
                $('.selectTranslationEngineOpenAi').css('display', 'none');
            } else if (this.value === "DeepL") {
                $('.selectTranslationEngineBaidu').css('display', 'none');
                $('.selectTranslationEngineGoogle').css('display', 'none');
                $('.selectTranslationEngineDeepL').css('display', 'inherit');
                $('.selectTranslationEngineOpenAi').css('display', 'none');
            } else if (this.value === "OpenAi") {
                $('.selectTranslationEngineBaidu').css('display', 'none');
                $('.selectTranslationEngineGoogle').css('display', 'none');
                $('.selectTranslationEngineDeepL').css('display', 'none');
                $('.selectTranslationEngineOpenAi').css('display', 'inherit');
            } else {
                $('.selectTranslationEngineBaidu').css('display', 'none');
                $('.selectTranslationEngineGoogle').css('display', 'none');
                $('.selectTranslationEngineDeepL').css('display', 'none');
                $('.selectTranslationEngineOpenAi').css('display', 'none');
            }
        });

        $('.MetaTubeConfigurationPage').on('pageshow', function () {
            Dashboard.showLoadingMsg();
            var page = this;
            ApiClient.getPluginConfiguration(MetaTubePluginConfig.pluginUniqueId).then(function (config) {
                $('#txtServer', page).val(config.Server).change();
                $('#txtToken', page).val(config.Token).change();
                page.querySelector('#chkEnableCollections').checked = config.EnableCollections;
                page.querySelector('#chkEnableDirectors').checked = config.EnableDirectors;
                page.querySelector('#chkEnableRatings').checked = config.EnableRatings;
                page.querySelector('#chkEnableTrailers').checked = config.EnableTrailers;
                page.querySelector('#chkEnableRealActorNames').checked = config.EnableRealActorNames;
                page.querySelector('#chkEnableBadges').checked = config.EnableBadges;
                $('#txtBadgeUrl', page).val(config.BadgeUrl).change();
                $('#txtPrimaryImageRatio', page).val(config.PrimaryImageRatio).change();
                $('#txtDefaultImageQuality', page).val(config.DefaultImageQuality).change();
                page.querySelector('#chkEnableMovieProviderFilter').checked = config.EnableMovieProviderFilter;
                $('#txtRawMovieProviderFilter', page).val(config.RawMovieProviderFilter).change();
                page.querySelector('#chkEnableTemplate').checked = config.EnableTemplate;
                $('#txtNameTemplate', page).val(config.NameTemplate).change();
                $('#txtTaglineTemplate', page).val(config.TaglineTemplate).change();
                $('#selectTranslationMode', page).val(config.TranslationMode).change();
                $('#selectTranslationEngine', page).val(config.TranslationEngine).change();
                $('#txtBaiduAppId', page).val(config.BaiduAppId).change();
                $('#txtBaiduAppKey', page).val(config.BaiduAppKey).change();
                $('#txtGoogleApiKey', page).val(config.GoogleApiKey).change();
                $('#txtGoogleApiUrl', page).val(config.GoogleApiUrl).change();
                $('#txtDeepLApiKey', page).val(config.DeepLApiKey).change();
                $('#txtDeepLApiUrl', page).val(config.DeepLApiUrl).change();
                $('#txtOpenAiApiKey', page).val(config.OpenAiApiKey).change();
                $('#txtOpenAiApiUrl', page).val(config.OpenAiApiUrl).change();
                $('#txtOpenAiModel', page).val(config.OpenAiModel).change();
                page.querySelector('#chkEnableTitleSubstitution').checked = config.EnableTitleSubstitution;
                $('#txtTitleRawSubstitutionTable', page).val(config.TitleRawSubstitutionTable).change();
                page.querySelector('#chkEnableActorSubstitution').checked = config.EnableActorSubstitution;
                $('#txtActorRawSubstitutionTable', page).val(config.ActorRawSubstitutionTable).change();
                page.querySelector('#chkEnableGenreSubstitution').checked = config.EnableGenreSubstitution;
                $('#txtGenreRawSubstitutionTable', page).val(config.GenreRawSubstitutionTable).change();
                Dashboard.hideLoadingMsg();
            });
        });

        $('.MetaTubeConfigurationForm').on('submit', function () {
            Dashboard.showLoadingMsg();
            var form = this;
            ApiClient.getPluginConfiguration(MetaTubePluginConfig.pluginUniqueId).then(function (config) {
                config.Server = $('#txtServer', form).val();
                config.Token = $('#txtToken', form).val();
                config.EnableCollections = $('#chkEnableCollections', form).prop('checked');
                config.EnableDirectors = $('#chkEnableDirectors', form).prop('checked');
                config.EnableRatings = $('#chkEnableRatings', form).prop('checked');
                config.EnableTrailers = $('#chkEnableTrailers', form).prop('checked');
                config.EnableRealActorNames = $('#chkEnableRealActorNames', form).prop('checked');
                config.EnableBadges = $('#chkEnableBadges', form).prop('checked');
                config.BadgeUrl = $('#txtBadgeUrl', form).val();
                config.PrimaryImageRatio = $('#txtPrimaryImageRatio', form).val();
                config.DefaultImageQuality = $('#txtDefaultImageQuality', form).val();
                config.EnableMovieProviderFilter = $('#chkEnableMovieProviderFilter', form).prop('checked');
                config.RawMovieProviderFilter = $('#txtRawMovieProviderFilter', form).val();
                config.EnableTemplate = $('#chkEnableTemplate', form).prop('checked');
                config.NameTemplate = $('#txtNameTemplate', form).val();
                config.TaglineTemplate = $('#txtTaglineTemplate', form).val();
                config.TranslationMode = $('#selectTranslationMode', form).val();
                config.TranslationEngine = $('#selectTranslationEngine', form).val();
                config.BaiduAppId = $('#txtBaiduAppId', form).val();
                config.BaiduAppKey = $('#txtBaiduAppKey', form).val();
                config.GoogleApiKey = $('#txtGoogleApiKey', form).val();
                config.GoogleApiUrl = $('#txtGoogleApiUrl', form).val();
                config.DeepLApiKey = $('#txtDeepLApiKey', form).val();
                config.DeepLXAltUrl = $('#txtDeepLApiUrl', form).val();
                config.OpenAiApiKey = $('#txtOpenAiApiKey', form).val();
                config.OpenAiApiUrl = $('#txtOpenAiApiUrl', form).val();
                config.OpenAiModel = $('#txtOpenAiModel', form).val();
                config.EnableTitleSubstitution = $('#chkEnableTitleSubstitution', form).prop('checked');
                config.TitleRawSubstitutionTable = $('#txtTitleRawSubstitutionTable', form).val();
                config.EnableActorSubstitution = $('#chkEnableActorSubstitution', form).prop('checked');
                config.ActorRawSubstitutionTable = $('#txtActorRawSubstitutionTable', form).val();
                config.EnableGenreSubstitution = $('#chkEnableGenreSubstitution', form).prop('checked');
                config.GenreRawSubstitutionTable = $('#txtGenreRawSubstitutionTable', form).val();
                ApiClient.updatePluginConfiguration(MetaTubePluginConfig.pluginUniqueId, config).then(Dashboard.processPluginConfigurationUpdateResult);
            });
            // Disable default form submission
            return false;
        });
    </script>
</div>
</body>
</html>



================================================
FILE: Jellyfin.Plugin.MetaTube/Configuration/PluginConfiguration.cs
================================================
using Jellyfin.Plugin.MetaTube.Helpers;
using Jellyfin.Plugin.MetaTube.Translation;
#if __EMBY__
using System.ComponentModel;
using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;

#else
using MediaBrowser.Model.Plugins;
#endif

namespace Jellyfin.Plugin.MetaTube.Configuration;

#if __EMBY__
public class PluginConfiguration : EditableOptionsBase
{
    public override string EditorTitle => Plugin.ProviderName;
#else
public class PluginConfiguration : BasePluginConfiguration
{
#endif

#if __EMBY__
    [DisplayName("Server")]
    [Description("Full url of the MetaTube Server, HTTPS protocol is recommended.")]
    [Required]
#endif
    public string Server { get; set; } = string.Empty;

#if __EMBY__
    [DisplayName("Token")]
    [Description("Access token for the MetaTube Server, or blank if no token is set by the backend.")]
#endif
    public string Token { get; set; } = string.Empty;

#if __EMBY__
    [DisplayName("Enable auto update")]
    [Description("Automatically update the plugin through scheduled tasks.")]
    public bool EnableAutoUpdate { get; set; } = true;
#endif

#if __EMBY__
    [DisplayName("Enable collections")]
    [Description("Automatically create collections by series.")]
#endif
    public bool EnableCollections { get; set; } = false;

#if __EMBY__
    [DisplayName("Enable directors")]
    [Description("Add directors to corresponding video metadata.")]
#endif
    public bool EnableDirectors { get; set; } = true;

#if __EMBY__
    [DisplayName("Enable ratings")]
    [Description("Display community ratings from the original website.")]
#endif
    public bool EnableRatings { get; set; } = true;

#if __EMBY__
    [DisplayName("Enable trailers")]
    [Description("Generate online video trailers in strm format.")]
#endif
    public bool EnableTrailers { get; set; } = false;

#if __EMBY__
    [DisplayName("Enable real actor names")]
    [Description("Search and replace with real actor names from AVBASE.")]
#endif
    public bool EnableRealActorNames { get; set; } = false;

#if __EMBY__
    [DisplayName("Enable badges")]
    [Description("Add Chinese subtitle badges to primary images.")]
#endif
    public bool EnableBadges { get; set; } = false;

#if __EMBY__
    [DisplayName("Badge url")]
    [Description("Custom badge url, PNG format is recommended. (default: zimu.png)")]
#endif
    public string BadgeUrl { get; set; } = "zimu.png";

#if __EMBY__
    [DisplayName("Primary image ratio")]
    [Description("Aspect ratio for primary images, set a negative value to use the default.")]
#endif
    public double PrimaryImageRatio { get; set; } = -1;

#if __EMBY__
    [DisplayName("Default image quality")]
    [Description("Default compression quality for JPEG images, set between 0 and 100. (default: 90)")]
    [MinValue(0)]
    [MaxValue(100)]
    [Required]
#endif
    public int DefaultImageQuality { get; set; } = 90;

#if __EMBY__
    [DisplayName("Enable movie provider filter")]
    [Description("Filter and reorder search results from movie providers.")]
#endif
    public bool EnableMovieProviderFilter { get; set; } = false;

#if __EMBY__
    [DisplayName("Movie provider filter")]
    [Description(
        "Provider names are case-insensitive, with decreasing precedence from left to right, separated by commas.")]
#endif
    public string RawMovieProviderFilter
    {
        get => _movieProviderFilter?.Any() == true ? string.Join(',', _movieProviderFilter) : string.Empty;
        set => _movieProviderFilter = value?.Split(',').Select(s => s.Trim()).Where(s => s.Any())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public List<string> GetMovieProviderFilter()
    {
        return _movieProviderFilter;
    }

    private List<string> _movieProviderFilter;

#if __EMBY__
    [DisplayName("Enable template")]
#endif
    public bool EnableTemplate { get; set; } = false;

#if __EMBY__
    [DisplayName("Name template")]
#endif
    public string NameTemplate { get; set; } = DefaultNameTemplate;

#if __EMBY__
    [DisplayName("Tagline template")]
#endif
    public string TaglineTemplate { get; set; } = DefaultTaglineTemplate;

    public static string DefaultNameTemplate => "{number} {title}";

    public static string DefaultTaglineTemplate => "配信開始日 {date}";

#if __EMBY__
    [DisplayName("Translation mode")]
#endif
    public TranslationMode TranslationMode { get; set; } = TranslationMode.Disabled;

#if __EMBY__
    [DisplayName("Translation engine")]
#endif
    public TranslationEngine TranslationEngine { get; set; } = TranslationEngine.Baidu;

#if __EMBY__
    [DisplayName("Baidu app id")]
    [VisibleCondition(nameof(TranslationEngine), ValueCondition.IsEqual, TranslationEngine.Baidu)]
#endif
    public string BaiduAppId { get; set; } = string.Empty;

#if __EMBY__
    [DisplayName("Baidu app key")]
    [VisibleCondition(nameof(TranslationEngine), ValueCondition.IsEqual, TranslationEngine.Baidu)]
#endif
    public string BaiduAppKey { get; set; } = string.Empty;

#if __EMBY__
    [DisplayName("Google api key")]
    [VisibleCondition(nameof(TranslationEngine), ValueCondition.IsEqual, TranslationEngine.Google)]
#endif
    public string GoogleApiKey { get; set; } = string.Empty;

#if __EMBY__
    [DisplayName("Google api url")]
    [Description("Custom Google translate api url. (optional)")]
    [VisibleCondition(nameof(TranslationEngine), ValueCondition.IsEqual, TranslationEngine.Google)]
#endif
    public string GoogleApiUrl { get; set; } = string.Empty;

#if __EMBY__
    [DisplayName("DeepL api key")]
    [VisibleCondition(nameof(TranslationEngine), ValueCondition.IsEqual, TranslationEngine.DeepL)]
#endif
    public string DeepLApiKey { get; set; } = string.Empty;

#if __EMBY__
    [DisplayName("DeepL api url")]
    [Description("Custom DeepL-compatible api url. (optional)")]
    [VisibleCondition(nameof(TranslationEngine), ValueCondition.IsEqual, TranslationEngine.DeepL)]
#endif
    public string DeepLApiUrl { get; set; } = string.Empty;

#if __EMBY__
    [DisplayName("OpenAI api key")]
    [VisibleCondition(nameof(TranslationEngine), ValueCondition.IsEqual, TranslationEngine.OpenAi)]
#endif
    public string OpenAiApiKey { get; set; } = string.Empty;

#if __EMBY__
    [DisplayName("OpenAI api url")]
    [Description("Custom OpenAI-compatible api url. (optional)")]
    [VisibleCondition(nameof(TranslationEngine), ValueCondition.IsEqual, TranslationEngine.OpenAi)]
#endif
    public string OpenAiApiUrl { get; set; } = string.Empty;

#if __EMBY__
    [DisplayName("OpenAI model")]
    [Description("Custom OpenAI-compatible api model. (optional)")]
    [VisibleCondition(nameof(TranslationEngine), ValueCondition.IsEqual, TranslationEngine.OpenAi)]
#endif
    public string OpenAiModel { get; set; } = string.Empty;

#if __EMBY__
    [DisplayName("Enable title substitution")]
#endif
    public bool EnableTitleSubstitution { get; set; } = false;

#if __EMBY__
    [DisplayName("Title substitution table")]
    [Description(
        "One record per line, separated by equal signs. Leave the target substring blank to delete the source substring.")]
    [EditMultiline(5)]
#endif
    public string TitleRawSubstitutionTable
    {
        get => _titleSubstitutionTable?.ToString();
        set => _titleSubstitutionTable = SubstitutionTable.Parse(value);
    }

    public SubstitutionTable GetTitleSubstitutionTable()
    {
        return _titleSubstitutionTable;
    }

    private SubstitutionTable _titleSubstitutionTable;

#if __EMBY__
    [DisplayName("Enable actor substitution")]
#endif
    public bool EnableActorSubstitution { get; set; } = false;

#if __EMBY__
    [DisplayName("Actor substitution table")]
    [Description(
        "One record per line, separated by equal signs. Leave the target actor blank to delete the source actor.")]
    [EditMultiline(5)]
#endif
    public string ActorRawSubstitutionTable
    {
        get => _actorSubstitutionTable?.ToString();
        set => _actorSubstitutionTable = SubstitutionTable.Parse(value);
    }

    public SubstitutionTable GetActorSubstitutionTable()
    {
        return _actorSubstitutionTable;
    }

    private SubstitutionTable _actorSubstitutionTable;

#if __EMBY__
    [DisplayName("Enable genre substitution")]
#endif
    public bool EnableGenreSubstitution { get; set; } = false;

#if __EMBY__
    [DisplayName("Title substitution table")]
    [Description(
        "One record per line, separated by equal signs. Leave the target genre blank to delete the source genre.")]
    [EditMultiline(5)]
#endif
    public string GenreRawSubstitutionTable
    {
        get => _genreSubstitutionTable?.ToString();
        set => _genreSubstitutionTable = SubstitutionTable.Parse(value);
    }

    public SubstitutionTable GetGenreSubstitutionTable()
    {
        return _genreSubstitutionTable;
    }

    private SubstitutionTable _genreSubstitutionTable;
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Extensions/DateTimeExtensions.cs
================================================
namespace Jellyfin.Plugin.MetaTube.Extensions;

public static class DateTimeExtensions
{
    public static DateTime? GetValidDateTime(this DateTime dateTime)
    {
        return dateTime.Year > 1 ? dateTime : null;
    }

    public static int? GetValidYear(this DateTime dateTime)
    {
        return dateTime.GetValidDateTime()?.Year;
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Extensions/EmbyExtensions.cs
================================================
#if __EMBY__

using System.Text;
using MediaBrowser.Model.Logging;

namespace Jellyfin.Plugin.MetaTube.Extensions;

public static class EmbyExtensions
{
    #region LogManager

    public static ILogger CreateLogger<T>(this ILogManager logManager)
    {
        return logManager.GetLogger($"{Plugin.ProviderName}.{typeof(T).Name}");
    }

    #endregion

    #region Sorting

    public static IEnumerable<T> OrderByString<T>(this IEnumerable<T> list, Func<T, string> getName)
    {
        return list.OrderBy(getName, new AlphanumComparator());
    }

    public static IEnumerable<T> OrderByStringDescending<T>(
        this IEnumerable<T> list,
        Func<T, string> getName)
    {
        return list.OrderByDescending(getName, new AlphanumComparator());
    }

    public static IOrderedEnumerable<T> ThenByString<T>(
        this IOrderedEnumerable<T> list,
        Func<T, string> getName)
    {
        return list.ThenBy(getName, new AlphanumComparator());
    }

    public static IOrderedEnumerable<T> ThenByStringDescending<T>(
        this IOrderedEnumerable<T> list,
        Func<T, string> getName)
    {
        return list.ThenByDescending(getName, new AlphanumComparator());
    }

    private sealed class AlphanumComparator : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            return CompareValues(x, y);
        }

        private static bool InChunk(char ch, char otherCh)
        {
            var chunkType = ChunkType.Alphanumeric;
            if (char.IsDigit(otherCh))
                chunkType = ChunkType.Numeric;
            return (chunkType != ChunkType.Alphanumeric || !char.IsDigit(ch)) &&
                   (chunkType != ChunkType.Numeric || char.IsDigit(ch));
        }

        private static int CompareValues(string s1, string s2)
        {
            if (s1 == null || s2 == null)
                return 0;
            var index1 = 0;
            var index2 = 0;
            while (index1 < s1.Length || index2 < s2.Length)
            {
                if (index1 >= s1.Length)
                    return -1;
                if (index2 >= s2.Length)
                    return 1;
                var ch1 = s1[index1];
                var ch2 = s2[index2];
                var stringBuilder1 = new StringBuilder();
                var stringBuilder2 = new StringBuilder();
                while (index1 < s1.Length && (stringBuilder1.Length == 0 || InChunk(ch1, stringBuilder1[0])))
                {
                    stringBuilder1.Append(ch1);
                    ++index1;
                    if (index1 < s1.Length)
                        ch1 = s1[index1];
                }

                while (index2 < s2.Length && (stringBuilder2.Length == 0 || InChunk(ch2, stringBuilder2[0])))
                {
                    stringBuilder2.Append(ch2);
                    ++index2;
                    if (index2 < s2.Length)
                        ch2 = s2[index2];
                }

                var num = 0;
                if (char.IsDigit(stringBuilder1[0]) && char.IsDigit(stringBuilder2[0]))
                {
                    if (!int.TryParse(stringBuilder1.ToString(), out var result1) ||
                        !int.TryParse(stringBuilder2.ToString(), out var result2))
                        return 0;
                    if (result1 < result2)
                        num = -1;
                    if (result1 > result2)
                        num = 1;
                }
                else
                {
                    num = string.Compare(stringBuilder1.ToString(), stringBuilder2.ToString(),
                        StringComparison.CurrentCulture);
                }

                if (num != 0)
                    return num;
            }

            return 0;
        }

        private enum ChunkType
        {
            Alphanumeric,
            Numeric
        }
    }

    #endregion
}

#endif


================================================
FILE: Jellyfin.Plugin.MetaTube/Extensions/EnumerableExtensions.cs
================================================
namespace Jellyfin.Plugin.MetaTube.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<(int index, T item)> WithIndex<T>(this IEnumerable<T> source)
    {
        return source.Select((item, index) => (index, item));
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Extensions/JellyfinExtensions.cs
================================================
#if !__EMBY__
#pragma warning disable CA2254

using MediaBrowser.Controller.Entities.Movies;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MetaTube.Extensions;

public static class JellyfinExtensions
{
    #region Logger

    public static void Debug(this ILogger logger, string message, params object[] args)
    {
        logger.LogDebug(message, args);
    }

    public static void Info(this ILogger logger, string message, params object[] args)
    {
        logger.LogInformation(message, args);
    }

    public static void Warn(this ILogger logger, string message, params object[] args)
    {
        logger.LogWarning(message, args);
    }

    public static void Error(this ILogger logger, string message, params object[] args)
    {
        logger.LogError(message, args);
    }

    #endregion

    #region Movie

    public static void AddCollection(this Movie movie, string name)
    {
        movie.CollectionName = name;
    }

    #endregion
}

#pragma warning restore CA2254
#endif


================================================
FILE: Jellyfin.Plugin.MetaTube/Extensions/ProviderIdsExtensions.cs
================================================
using System.Web;
using Jellyfin.Plugin.MetaTube.Helpers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.MetaTube.Extensions;

public static class ProviderIdsExtensions
{
    public static ProviderId GetPid(this IHasProviderIds instance, string name)
    {
        return ProviderId.Parse(instance.GetProviderId(name));
    }

    public static void SetPid(this IHasProviderIds instance, string name, string provider, string id,
        double? position = null, bool? update = null)
    {
        var pid = new ProviderId
        {
            Provider = provider,
            Id = Uri.EscapeDataString(id),
            Position = position,
            Update = update
        };
        instance.SetProviderId(name, pid.ToString());
    }

    public static string GetTrailerUrl(this IHasProviderIds instance)
    {
        return !instance.ProviderIds.Any()
            ? string.Empty
            : HttpUtility.UrlDecode(instance.GetProviderId("TrailerUrl"));
    }

    public static void SetTrailerUrl(this IHasProviderIds instance, string url)
    {
        instance.SetProviderId("TrailerUrl", HttpUtility.UrlEncode(url));
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/ExternalIds/ActorExternalId.cs
================================================
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
#if !__EMBY__
using MediaBrowser.Model.Providers;
#endif

namespace Jellyfin.Plugin.MetaTube.ExternalIds;

public class ActorExternalId : BaseExternalId
{
#if !__EMBY__
    public override ExternalIdMediaType? Type => ExternalIdMediaType.Person;
#endif

    public override bool Supports(IHasProviderIds item)
    {
        return item is Person;
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/ExternalIds/BaseExternalId.cs
================================================
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
#if !__EMBY__
using MediaBrowser.Model.Providers;
#endif

namespace Jellyfin.Plugin.MetaTube.ExternalIds;

public abstract class BaseExternalId : IExternalId
{
#if __EMBY__
    public virtual string Name => Plugin.ProviderName;
#else
    public virtual string ProviderName => Plugin.ProviderName;

    public abstract ExternalIdMediaType? Type { get; }
#endif

    public virtual string Key => Plugin.ProviderId;

    public virtual string UrlFormatString => Plugin.Instance.Configuration.Server + "?redirect={0}";

    public abstract bool Supports(IHasProviderIds item);
}


================================================
FILE: Jellyfin.Plugin.MetaTube/ExternalIds/MovieExternalId.cs
================================================
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
#if !__EMBY__
using MediaBrowser.Model.Providers;
#endif

namespace Jellyfin.Plugin.MetaTube.ExternalIds;

public class MovieExternalId : BaseExternalId
{
#if !__EMBY__
    public override ExternalIdMediaType? Type => ExternalIdMediaType.Movie;
#endif

    public override bool Supports(IHasProviderIds item)
    {
        return item is Movie;
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/ExternalIds/TrailerExternalId.cs
================================================
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
#if !__EMBY__
using MediaBrowser.Model.Providers;
#endif

namespace Jellyfin.Plugin.MetaTube.ExternalIds;

public class TrailerExternalId : BaseExternalId
{
#if __EMBY__
    public override string Name => "TrailerUrl";
#else
    public override string ProviderName => "TrailerUrl";

    public override ExternalIdMediaType? Type => ExternalIdMediaType.Movie;
#endif

    public override string Key => "TrailerUrl";

    public override string UrlFormatString => null;

    public override bool Supports(IHasProviderIds item)
    {
        return item is Movie;
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Helpers/Levenshtein.cs
================================================
namespace Jellyfin.Plugin.MetaTube.Helpers;

public static class Levenshtein
{
    public static int Distance(string value1, string value2)
    {
        if (value2.Length == 0)
        {
            return value1.Length;
        }

        int[] costs = new int[value2.Length];

        for (int i = 0; i < costs.Length;)
        {
            costs[i] = ++i;
        }

        for (int i = 0; i < value1.Length; i++)
        {
            int cost = i;
            int previousCost = i;

            char value1Char = value1[i];

            for (int j = 0; j < value2.Length; j++)
            {
                int currentCost = cost;

                cost = costs[j];

                if (value1Char != value2[j])
                {
                    if (previousCost < currentCost)
                    {
                        currentCost = previousCost;
                    }

                    if (cost < currentCost)
                    {
                        currentCost = cost;
                    }

                    ++currentCost;
                }

                costs[j] = currentCost;
                previousCost = currentCost;
            }
        }

        return costs[costs.Length - 1];
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Helpers/ProviderId.cs
================================================
namespace Jellyfin.Plugin.MetaTube.Helpers;

public class ProviderId
{
    public string Provider { get; set; }

    public string Id { get; set; }

    public double? Position { get; set; }

    public bool? Update { get; set; }

    public static ProviderId Parse(string rawPid)
    {
        var values = rawPid?.Split(':');
        return new ProviderId
        {
            Provider = values?.Length > 0 ? values[0] : string.Empty,
            Id = values?.Length > 1 ? Uri.UnescapeDataString(values[1]) : string.Empty,
            Position = values?.Length > 2 ? ToDouble(values[2]) : null,
            Update = values?.Length > 3 ? ToBool(values[3]) : null
        };
    }

    public override string ToString()
    {
        var pid = this;
        var values = new List<string>
        {
            pid.Provider, pid.Id
        };
        if (pid.Position.HasValue) values.Add(pid.Position.ToString());
        if (pid.Update.HasValue) values.Add((values.Count == 2 ? ":" : string.Empty) + pid.Update);
        return string.Join(':', values);
    }

    private static bool? ToBool(string s)
    {
        switch (s)
        {
            case "1":
            case "t":
            case "T":
            case "true":
            case "True":
            case "TRUE":
                return true;
            case "0":
            case "f":
            case "F":
            case "false":
            case "False":
            case "FALSE":
                return false;
        }

        return null;
    }

    private static double? ToDouble(string s)
    {
        return double.TryParse(s, out var result) ? result : null;
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Helpers/SubstitutionTable.cs
================================================
using System.Text;

namespace Jellyfin.Plugin.MetaTube.Helpers;

public class SubstitutionTable : Dictionary<string, string>
{
    private SubstitutionTable() : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    public static SubstitutionTable Parse(string text)
    {
        var dictionary = new SubstitutionTable();

        var reader = new StringReader(text ?? string.Empty);
        while (reader.ReadLine() is { } line)
        {
            var kvp = line.Split('=', 2).Select(s => s.Trim()).ToList();
            if (string.IsNullOrWhiteSpace(kvp.First()))
                continue;
            dictionary[kvp[0]] = kvp.Count switch
            {
                1 => null,
                2 => kvp[1],
                _ => dictionary[kvp[0]]
            };
        }

        return dictionary;
    }

    public override string ToString()
    {
        var table = this;
        return table.Any() != true
            ? string.Empty
            : string.Join('\n',
                table.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                    .Select(kvp => $"{kvp.Key?.Trim()}={kvp.Value?.Trim()}"));
    }

    public string Substitute(string source)
    {
        var table = this;

        return table.Any() != true
            ? source
            : table.Aggregate(new StringBuilder(source), (sb, kvp) => sb.Replace(kvp.Key, kvp.Value)).ToString();
    }

    public IEnumerable<string> Substitute(IEnumerable<string> source)
    {
        var table = this;

        if (table.Any() != true)
            return source;

        var target = new List<string>();

        foreach (var item in source ?? Enumerable.Empty<string>())
        {
            if (!table.TryGetValue(item, out var value))
                target.Add(item);
            else if (!string.IsNullOrWhiteSpace(value))
                target.Add(value);
        }

        return target;
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Metadata/ActorInfo.cs
================================================
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetaTube.Metadata;

public class ActorInfo : ActorSearchResult
{
    [JsonPropertyName("aliases")]
    public string[] Aliases { get; set; }

    [JsonPropertyName("birthday")]
    public DateTime Birthday { get; set; }

    [JsonPropertyName("blood_type")]
    public string BloodType { get; set; }

    [JsonPropertyName("cup_size")]
    public string CupSize { get; set; }

    [JsonPropertyName("debut_date")]
    public DateTime DebutDate { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("hobby")]
    public string Hobby { get; set; }

    [JsonPropertyName("skill")]
    public string Skill { get; set; }

    [JsonPropertyName("measurements")]
    public string Measurements { get; set; }

    [JsonPropertyName("nationality")]
    public string Nationality { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Metadata/ActorSearchResult.cs
================================================
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetaTube.Metadata;

public class ActorSearchResult : ProviderInfo
{
    [JsonPropertyName("images")]
    public string[] Images { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Metadata/ErrorInfo.cs
================================================
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetaTube.Metadata;

public class ErrorInfo
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Metadata/MovieInfo.cs
================================================
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetaTube.Metadata;

public class MovieInfo : MovieSearchResult
{
    [JsonPropertyName("big_cover_url")]
    public string BigCoverUrl { get; set; }

    [JsonPropertyName("big_thumb_url")]
    public string BigThumbUrl { get; set; }

    [JsonPropertyName("director")]
    public string Director { get; set; }

    [JsonPropertyName("genres")]
    public string[] Genres { get; set; }

    [JsonPropertyName("maker")]
    public string Maker { get; set; }

    [JsonPropertyName("preview_images")]
    public string[] PreviewImages { get; set; }

    [JsonPropertyName("preview_video_hls_url")]
    public string PreviewVideoHlsUrl { get; set; }

    [JsonPropertyName("preview_video_url")]
    public string PreviewVideoUrl { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; }

    [JsonPropertyName("runtime")]
    public int Runtime { get; set; }

    [JsonPropertyName("series")]
    public string Series { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Metadata/MovieSearchResult.cs
================================================
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetaTube.Metadata;

public class MovieSearchResult : ProviderInfo
{
    [JsonPropertyName("actors")]
    public string[] Actors { get; set; }

    [JsonPropertyName("cover_url")]
    public string CoverUrl { get; set; }

    [JsonPropertyName("number")]
    public string Number { get; set; }

    [JsonPropertyName("release_date")]
    public DateTime ReleaseDate { get; set; }

    [JsonPropertyName("score")]
    public float Score { get; set; }

    [JsonPropertyName("thumb_url")]
    public string ThumbUrl { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Metadata/ProviderInfo.cs
================================================
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetaTube.Metadata;

public class ProviderInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; }

    [JsonPropertyName("homepage")]
    public string Homepage { get; set; }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Metadata/ResponseInfo.cs
================================================
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetaTube.Metadata;

public class ResponseInfo<T>
{
    [JsonPropertyName("data")]
    public T Data { get; set; }

    [JsonPropertyName("error")]
    public ErrorInfo Error { get; set; }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Metadata/TranslationInfo.cs
================================================
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetaTube.Metadata;

public class TranslationInfo
{
    [JsonPropertyName("from")]
    public string From { get; set; }

    [JsonPropertyName("to")]
    public string To { get; set; }

    [JsonPropertyName("translated_text")]
    public string TranslatedText { get; set; }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Providers/ActorImageProvider.cs
================================================
using Jellyfin.Plugin.MetaTube.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
#if __EMBY__
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Configuration;

#else
using Microsoft.Extensions.Logging;
#endif

namespace Jellyfin.Plugin.MetaTube.Providers;

public class ActorImageProvider : BaseProvider, IRemoteImageProvider, IHasOrder
{
#if __EMBY__
    public ActorImageProvider(ILogManager logManager) : base(logManager.CreateLogger<ActorImageProvider>())
#else
    public ActorImageProvider( ILogger<ActorImageProvider> logger) : base(logger)
#endif
    {
    }

#if __EMBY__
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions,
        CancellationToken cancellationToken)
#else
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
#endif
    {
        var pid = item.GetPid(Plugin.ProviderId);
        if (string.IsNullOrWhiteSpace(pid.Id) || string.IsNullOrWhiteSpace(pid.Provider))
            return Enumerable.Empty<RemoteImageInfo>();

        var actorInfo = await ApiClient.GetActorInfoAsync(pid.Provider, pid.Id, cancellationToken);

        if (actorInfo.Images?.Any() != true)
            return Enumerable.Empty<RemoteImageInfo>();

        return actorInfo.Images.Select(image => new RemoteImageInfo
        {
            ProviderName = Name,
            Type = ImageType.Primary,
            Url = ApiClient.GetPrimaryImageApiUrl(actorInfo.Provider, actorInfo.Id, image, 0.5, true)
        });
    }

    public bool Supports(BaseItem item)
    {
        return item is Person;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return new List<ImageType>
        {
            ImageType.Primary
        };
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Providers/ActorProvider.cs
================================================
using Jellyfin.Plugin.MetaTube.Extensions;
using Jellyfin.Plugin.MetaTube.Metadata;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
#if __EMBY__
using MediaBrowser.Model.Logging;

#else
using Microsoft.Extensions.Logging;
#endif

namespace Jellyfin.Plugin.MetaTube.Providers;

public class ActorProvider : BaseProvider, IRemoteMetadataProvider<Person, PersonLookupInfo>, IHasOrder
{
#if __EMBY__
    public ActorProvider(ILogManager logManager) : base(logManager.CreateLogger<ActorProvider>())
#else
    public ActorProvider(ILogger<ActorProvider> logger) : base(logger)
#endif
    {
    }

    public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info,
        CancellationToken cancellationToken)
    {
        var pid = info.GetPid(Plugin.ProviderId);
        if (string.IsNullOrWhiteSpace(pid.Id) || string.IsNullOrWhiteSpace(pid.Provider))
        {
            var firstResult = (await GetSearchResults(info, cancellationToken)).FirstOrDefault();
            if (firstResult != null) pid = firstResult.GetPid(Plugin.ProviderId);
        }

        Logger.Info("Get actor info: {0}", pid.ToString());

        var m = await ApiClient.GetActorInfoAsync(pid.Provider, pid.Id, cancellationToken);

        var result = new MetadataResult<Person>
        {
            Item = new Person
            {
                Name = m.Name,
                PremiereDate = m.Birthday.GetValidDateTime(),
                ProductionYear = m.Birthday.GetValidYear(),
                Overview = FormatOverview(m)
            },
            HasMetadata = true
        };

        // Set ProviderIdModel.
        result.Item.SetPid(Name, m.Provider, m.Id);

        // Set actor nationality.
        if (!string.IsNullOrWhiteSpace(m.Nationality))
            result.Item.ProductionLocations = new[] { m.Nationality };

        return result;
    }

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        PersonLookupInfo info, CancellationToken cancellationToken)
    {
        var pid = info.GetPid(Plugin.ProviderId);

        var searchResults = new List<ActorSearchResult>();
        if (string.IsNullOrWhiteSpace(pid.Id))
        {
            // Search actor by name.
            Logger.Info("Search for actor: {0}", info.Name);
            searchResults.AddRange(await ApiClient.SearchActorAsync(info.Name, pid.Provider, cancellationToken));
        }
        else
        {
            // Exact search.
            Logger.Info("Search for actor: {0}", pid.ToString());
            searchResults.Add(await ApiClient.GetActorInfoAsync(pid.Provider, pid.Id,
                pid.Update != true, cancellationToken));
        }

        var results = new List<RemoteSearchResult>();
        if (!searchResults.Any())
        {
            Logger.Warn("Actor not found: {0}", pid.Id);
            return results;
        }

        foreach (var m in searchResults)
        {
            var result = new RemoteSearchResult
            {
                Name = $"[{m.Provider}] {m.Name}",
                SearchProviderName = Name,
                ImageUrl = m.Images?.Any() == true
                    ? ApiClient.GetPrimaryImageApiUrl(m.Provider, m.Id, m.Images.First(), 0.5, true)
                    : string.Empty
            };
            result.SetPid(Name, m.Provider, m.Id);
            results.Add(result);
        }

        return results;
    }

    private static string FormatOverview(ActorInfo a)
    {
        var aliases = a.Aliases?.Where(alias => !string.Equals(alias, a.Name, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var info = new List<(string, string)>
        {
            ("別名", string.Join(", ", aliases ?? Enumerable.Empty<string>())),
            ("3サイズ", a.Measurements),
            ("カップサイズ", a.CupSize),
            ("身長", a.Height > 0 ? $"{a.Height}cm" : string.Empty),
            ("血液型", !string.IsNullOrWhiteSpace(a.BloodType) ? $"{a.BloodType}型" : string.Empty),
            ("デビュー", a.DebutDate.GetValidDateTime()?.ToString("yyyy年M月d日"))
        };

        return string.Join("\n<br>\n",
            info.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Item2)).Select(kvp => $"{kvp.Item1}: {kvp.Item2}"));
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Providers/BaseProvider.cs
================================================
using Jellyfin.Plugin.MetaTube.Configuration;
#if __EMBY__
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Providers;

#else
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.MetaTube.Extensions;
#endif

namespace Jellyfin.Plugin.MetaTube.Providers;

#if __EMBY__
public abstract class BaseProvider : IHasSupportedExternalIdentifiers
#else
public abstract class BaseProvider
#endif
{
    protected readonly ILogger Logger;

    protected BaseProvider(ILogger logger)
    {
        Logger = logger;
    }

    protected static PluginConfiguration Configuration => Plugin.Instance.Configuration;

    public virtual int Order => 1;

    public virtual string Name => Plugin.ProviderName;

#if __EMBY__
    public string[] GetSupportedExternalIdentifiers()
    {
        return new[] { Plugin.ProviderName };
    }
#endif

#if __EMBY__
    public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
#else
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
#endif
    {
        Logger.Debug("GetImageResponse for url: {0}", url);
        return ApiClient.GetImageResponse(url, cancellationToken);
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Providers/ExternalUrlProvider.cs
================================================
#if !__EMBY__
using Jellyfin.Plugin.MetaTube.ExternalIds;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.MetaTube.Providers;

public class ExternalUrlProvider : IExternalUrlProvider
{
    public string Name => Plugin.ProviderName;

    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item.TryGetProviderId(Plugin.ProviderId, out var pid))
        {
            switch (item)
            {
                case Movie:
                    yield return string.Format(new MovieExternalId().UrlFormatString, pid);
                    break;
                case Person:
                    yield return string.Format(new ActorExternalId().UrlFormatString, pid);
                    break;
            }
        }
    }
}
#endif


================================================
FILE: Jellyfin.Plugin.MetaTube/Providers/MovieImageProvider.cs
================================================
using Jellyfin.Plugin.MetaTube.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
#if __EMBY__
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Logging;

#else
using Microsoft.Extensions.Logging;
#endif

namespace Jellyfin.Plugin.MetaTube.Providers;

public class MovieImageProvider : BaseProvider, IRemoteImageProvider, IHasOrder
{
#if __EMBY__
    public MovieImageProvider(ILogManager logManager) : base(logManager.CreateLogger<MovieImageProvider>())
#else
    public MovieImageProvider(ILogger<MovieImageProvider> logger) : base(logger)
#endif
    {
    }

#if __EMBY__
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions,
        CancellationToken cancellationToken)
#else
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
#endif
    {
        var pid = item.GetPid(Plugin.ProviderId);
        if (string.IsNullOrWhiteSpace(pid.Id) || string.IsNullOrWhiteSpace(pid.Provider))
            return Enumerable.Empty<RemoteImageInfo>();

        var m = await ApiClient.GetMovieInfoAsync(pid.Provider, pid.Id, cancellationToken);
        var images = new List<RemoteImageInfo>
        {
            new()
            {
                ProviderName = Name,
                Type = ImageType.Primary,
                Url = ApiClient.GetPrimaryImageApiUrl(m.Provider, m.Id, pid.Position ?? -1)
            },
            new()
            {
                ProviderName = Name,
                Type = ImageType.Thumb,
                Url = ApiClient.GetThumbImageApiUrl(m.Provider, m.Id)
            },
            new()
            {
                ProviderName = Name,
                Type = ImageType.Backdrop,
                Url = ApiClient.GetBackdropImageApiUrl(m.Provider, m.Id)
            }
        };

        foreach (var imageUrl in m.PreviewImages ?? Enumerable.Empty<string>())
        {
            images.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Type = ImageType.Primary,
                Url = ApiClient.GetPrimaryImageApiUrl(m.Provider, m.Id, imageUrl, pid.Position ?? -1)
            });

            images.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Type = ImageType.Thumb,
                Url = ApiClient.GetThumbImageApiUrl(m.Provider, m.Id, imageUrl)
            });

            images.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Type = ImageType.Backdrop,
                Url = ApiClient.GetBackdropImageApiUrl(m.Provider, m.Id, imageUrl)
            });
        }

        return images;
    }

    public bool Supports(BaseItem item)
    {
        return item is Movie;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return new List<ImageType>
        {
            ImageType.Primary,
            ImageType.Thumb,
            ImageType.Backdrop
        };
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Providers/MovieProvider.cs
================================================
using System.Text;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.MetaTube.Configuration;
using Jellyfin.Plugin.MetaTube.Extensions;
using Jellyfin.Plugin.MetaTube.Helpers;
using Jellyfin.Plugin.MetaTube.Metadata;
using Jellyfin.Plugin.MetaTube.Translation;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using MovieInfo = MediaBrowser.Controller.Providers.MovieInfo;
#if __EMBY__
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;

#else
using Jellyfin.Data.Enums;
using Microsoft.Extensions.Logging;
#endif

namespace Jellyfin.Plugin.MetaTube.Providers;

#if __EMBY__
public class MovieProvider : BaseProvider, IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder, IHasMetadataFeatures
#else
public class MovieProvider : BaseProvider, IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
#endif
{
    private const string AvBase = "AVBASE";
    private const string Gfriends = "Gfriends";
    private const string Rating = "JP-18+";

    private static readonly string[] AvBaseSupportedProviderNames = { "DUGA", "FANZA", "Getchu", "MGS" };

#if __EMBY__
    public MetadataFeatures[] Features => new[]
        { MetadataFeatures.Collections, MetadataFeatures.Adult, MetadataFeatures.RequiredSetup };

    public MovieProvider(ILogManager logManager) : base(logManager.CreateLogger<MovieProvider>())
#else
    public MovieProvider(ILogger<MovieProvider> logger) : base(logger)
#endif
    {
    }

    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info,
        CancellationToken cancellationToken)
    {
        var pid = info.GetPid(Plugin.ProviderId);
        if (string.IsNullOrWhiteSpace(pid.Id) || string.IsNullOrWhiteSpace(pid.Provider))
        {
            // Search movies and pick the first result.
            var firstResult = (await GetSearchResults(info, cancellationToken)).FirstOrDefault();
            if (firstResult != null) pid = firstResult.GetPid(Plugin.ProviderId);
        }

        Logger.Info("Get movie info: {0}", pid.ToString());

        var m = await ApiClient.GetMovieInfoAsync(pid.Provider, pid.Id, cancellationToken);

        // Preserve original title.
        var originalTitle = m.Title;

        // Convert to real actor names.
        if (Configuration.EnableRealActorNames)
            await ConvertToRealActorNames(m, cancellationToken);

        // Substitute title.
        if (Configuration.EnableTitleSubstitution)
            m.Title = Configuration.GetTitleSubstitutionTable().Substitute(m.Title);

        // Substitute actors.
        if (Configuration.EnableActorSubstitution)
            m.Actors = Configuration.GetActorSubstitutionTable().Substitute(m.Actors).ToArray();

        // Substitute genres.
        if (Configuration.EnableGenreSubstitution)
            m.Genres = Configuration.GetGenreSubstitutionTable().Substitute(m.Genres).ToArray();

        // Translate movie info.
        if (Configuration.TranslationMode != TranslationMode.Disabled)
            await TranslateMovieInfo(m, info.MetadataLanguage, cancellationToken);

        // Distinct and clean blank list
        m.Genres = m.Genres?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() ?? Array.Empty<string>();
        m.Actors = m.Actors?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() ?? Array.Empty<string>();
        m.PreviewImages = m.PreviewImages?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() ??
                          Array.Empty<string>();

        // Build parameters.
        var parameters = new Dictionary<string, string>
        {
            { @"{provider}", m.Provider },
            { @"{id}", m.Id },
            { @"{number}", m.Number },
            { @"{title}", m.Title },
            { @"{series}", m.Series },
            { @"{maker}", m.Maker },
            { @"{label}", m.Label },
            { @"{director}", m.Director },
            { @"{actors}", m.Actors?.Any() == true ? string.Join(' ', m.Actors) : string.Empty },
            { @"{first_actor}", m.Actors?.FirstOrDefault() },
            { @"{year}", $"{m.ReleaseDate:yyyy}" },
            { @"{month}", $"{m.ReleaseDate:MM}" },
            { @"{date}", $"{m.ReleaseDate:yyyy-MM-dd}" }
        };

        var result = new MetadataResult<Movie>
        {
            Item = new Movie
            {
                Name = RenderTemplate(
                    Configuration.EnableTemplate
                        ? Configuration.NameTemplate
                        : PluginConfiguration.DefaultNameTemplate, parameters),
                Tagline = RenderTemplate(
                    Configuration.EnableTemplate
                        ? Configuration.TaglineTemplate
                        : PluginConfiguration.DefaultTaglineTemplate, parameters),
                OriginalTitle = originalTitle,
                Overview = m.Summary,
                OfficialRating = Rating,
                PremiereDate = m.ReleaseDate.GetValidDateTime(),
                ProductionYear = m.ReleaseDate.GetValidYear(),
                Genres = m.Genres?.Any() == true ? m.Genres : Array.Empty<string>()
            },
            HasMetadata = true
        };

        // Set provider id.
        result.Item.SetPid(Name, m.Provider, m.Id, pid.Position);

        // Set trailer url.
        var trailerUrl = !string.IsNullOrWhiteSpace(m.PreviewVideoUrl)
            ? m.PreviewVideoUrl
            : m.PreviewVideoHlsUrl;
        if (!string.IsNullOrWhiteSpace(trailerUrl))
            result.Item.SetTrailerUrl(trailerUrl);

        // Set community rating.
        if (Configuration.EnableRatings)
            result.Item.CommunityRating = m.Score > 0 ? (float)Math.Round(m.Score * 2, 1) : null;

        // Add collection.
        if (Configuration.EnableCollections && !string.IsNullOrWhiteSpace(m.Series))
        {
            result.Item.AddCollection(m.Series);
            Logger.Info("Add Collection for movie {0} [{1}]", pid.ToString(), m.Series);
        }

        // Add studio.
        if (!string.IsNullOrWhiteSpace(m.Maker))
            result.Item.AddStudio(m.Maker);

        // Add tag (series).
        if (!string.IsNullOrWhiteSpace(m.Series))
            result.Item.AddTag(m.Series);

        // Add tag (maker).
        if (!string.IsNullOrWhiteSpace(m.Maker))
            result.Item.AddTag(m.Maker);

        // Add tag (label).
        if (!string.IsNullOrWhiteSpace(m.Label))
            result.Item.AddTag(m.Label);

        // Add director.
        if (Configuration.EnableDirectors && !string.IsNullOrWhiteSpace(m.Director))
            result.AddPerson(new PersonInfo
            {
                Name = m.Director,
#if __EMBY__
                Type = PersonType.Director
#else
                Type = PersonKind.Director
#endif
            });

        // Add actors.
        foreach (var name in m.Actors ?? Enumerable.Empty<string>())
        {
            var actor = new PersonInfo
            {
                Name = name,
#if __EMBY__
                Type = PersonType.Actor,
#else
                Type = PersonKind.Actor,
#endif
            };
            await SetActorImageUrl(actor, cancellationToken);
            result.AddPerson(actor);
        }

        return result;
    }

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo info,
        CancellationToken cancellationToken)
    {
        var pid = info.GetPid(Plugin.ProviderId);

        var searchResults = new List<MovieSearchResult>();
        if (string.IsNullOrWhiteSpace(pid.Id) || string.IsNullOrWhiteSpace(pid.Provider))
        {
            // Search movie by name.
            Logger.Info("Search for movie: {0}", info.Name);
            searchResults.AddRange(await ApiClient.SearchMovieAsync(info.Name, pid.Provider, cancellationToken));
        }
        else
        {
            // Exact search.
            Logger.Info("Search for movie: {0}", pid.ToString());
            searchResults.Add(await ApiClient.GetMovieInfoAsync(pid.Provider, pid.Id,
                pid.Update != true, cancellationToken));
        }

        if (Configuration.EnableMovieProviderFilter)
        {
            if (Configuration.GetMovieProviderFilter() is { } filter &&
                filter.Any()) // Apply only if filter is not empty.
            {
                // Filter out mismatched results.
                searchResults.RemoveAll(m => !filter.Contains(m.Provider, StringComparer.OrdinalIgnoreCase));
                // Reorder results by stable sort.
                searchResults = searchResults.OrderBy(m =>
                    filter.FindIndex(s => s.Equals(m.Provider, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            else
            {
                Logger.Warn("Movie provider filter enabled but never used");
            }
        }

        var results = new List<RemoteSearchResult>();
        if (!searchResults.Any())
        {
            Logger.Warn("Movie not found or has been filtered: {0}", pid.Id);
            return results;
        }

        foreach (var m in searchResults)
        {
            var result = new RemoteSearchResult
            {
                Name = $"[{m.Provider}] {m.Number} {m.Title}",
                SearchProviderName = Name,
                PremiereDate = m.ReleaseDate.GetValidDateTime(),
                ProductionYear = m.ReleaseDate.GetValidYear(),
                ImageUrl = ApiClient.GetPrimaryImageApiUrl(m.Provider, m.Id, m.ThumbUrl, 1.0, true)
            };
            result.SetPid(Name, m.Provider, m.Id, pid.Position);
            results.Add(result);
        }

        return results;
    }

    private async Task SetActorImageUrl(PersonInfo actor, CancellationToken cancellationToken)
    {
        try
        {
            var results = await ApiClient.SearchActorAsync(actor.Name, cancellationToken);
            if (results?.Any() != true)
            {
                Logger.Warn("Actor not found: {0}", actor.Name);
                return;
            }

            // Use the first result as the primary actor selection.
            var firstResult = results.First();
            if (firstResult.Images?.Any() == true)
            {
                actor.ImageUrl = ApiClient.GetPrimaryImageApiUrl(
                    firstResult.Provider, firstResult.Id, firstResult.Images.First(), 0.5, true);
                actor.SetPid(Name, firstResult.Provider, firstResult.Id);
            }

            // Use the Gfriends to update the actor profile image, if any.
            foreach (var result in results.Where(result => result.Provider == Gfriends && result.Images?.Any() == true))
            {
                actor.ImageUrl = ApiClient.GetPrimaryImageApiUrl(
                    result.Provider, result.Id, result.Images.First(), 0.5, true);
            }
        }
        catch (Exception e)
        {
            Logger.Error("Get actor image error: {0} ({1})", actor.Name, e.Message);
        }
    }

    private async Task ConvertToRealActorNames(MovieSearchResult m, CancellationToken cancellationToken)
    {
        if (!AvBaseSupportedProviderNames.Contains(m.Provider, StringComparer.OrdinalIgnoreCase)) return;

        try
        {
            var searchResults = await ApiClient.SearchMovieAsync(m.Id, AvBase, cancellationToken);
            if (searchResults?.Any() != true)
            {
                Logger.Warn("Movie not found on AVBASE: {0}", m.Id);
                return;
            }

            foreach (var result in searchResults)
            {
                var similarity = CalculateTitleSimilarity(m, result);

                Logger.Info("Calculate movie title similarity for {0} ({1}) and {2} ({3}): {4:0.00%}",
                    m.Id, m.Provider, result.Id, result.Provider, similarity);

                if (similarity >= 0.8)
                {
                    if (result.Actors?.Any() == true)
                        m.Actors = result.Actors;
                    return;
                }
            }

            Logger.Warn("No matching movie found on AVBASE for {0}", m.Id);
        }
        catch (Exception e)
        {
            Logger.Error("Convert to real actor names error: {0} ({1})", m.Number, e.Message);
        }
    }

    private static double CalculateTitleSimilarity(MovieSearchResult source, MovieSearchResult target)
    {
        var sourceKey = Normalize(source.Number + source.Title);
        var targetKey = Normalize(target.Number + target.Title);

        if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(targetKey))
            return 0.0;

        var distance = Levenshtein.Distance(sourceKey, targetKey);
        var avgLength = (sourceKey.Length + targetKey.Length) / 2.0;
        var similarity = 1.0 - distance / avgLength;

        return Math.Clamp(similarity, 0.0, 1.0);

        string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            s = s.ToLowerInvariant();
            s = Regex.Replace(s, @"[\s\[\]\(\)【】（）]", "");
            return s.Trim();
        }
    }

    private async Task TranslateMovieInfo(Metadata.MovieInfo m, string language, CancellationToken cancellationToken)
    {
        try
        {
            Logger.Info("Translate movie info language: {0} => {1}", m.Number, language);
            await TranslationHelper.TranslateAsync(m, language, cancellationToken);
        }
        catch (Exception e)
        {
            Logger.Error("Translate error: {0}", e.Message);
        }
    }

    private static string RenderTemplate(string template, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        var sb = parameters.Where(kvp => template.Contains(kvp.Key))
            .Aggregate(new StringBuilder(template),
                (sb, kvp) => sb.Replace(kvp.Key, kvp.Value));

        return sb.ToString().Trim();
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/ScheduledTasks/GenerateTrailersTask.cs
================================================
using System.Text;
using Jellyfin.Plugin.MetaTube.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
#if __EMBY__
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Logging;

#else
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
#endif

namespace Jellyfin.Plugin.MetaTube.ScheduledTasks;

public class GenerateTrailersTask : IScheduledTask
{
    // Emby: trailers can be stored in a trailers sub-folder.
    // https://support.emby.media/support/solutions/articles/44001159193-trailers
    private const string TrailersFolder = "trailers";

    // Uniform suffix for all trailer files.
    private const string TrailerFileSuffix = "-Trailer.strm";
    private const string TrailerSearchPattern = $"*{TrailerFileSuffix}";

    // UTF-8 without BOM encoding.
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    private readonly ILibraryManager _libraryManager;
    private readonly ILogger _logger;

#if __EMBY__
    public GenerateTrailersTask(ILogManager logManager, ILibraryManager libraryManager)
    {
        _logger = logManager.CreateLogger<GenerateTrailersTask>();
        _libraryManager = libraryManager;
    }
#else
    public GenerateTrailersTask(ILogger<GenerateTrailersTask> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }
#endif

    public string Key => $"{Plugin.ProviderName}GenerateTrailers";

    public string Name => "Generate Trailers";

    public string Description => $"Generates video trailers provided by {Plugin.ProviderName} in library.";

    public string Category => Plugin.ProviderName;

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
#if __EMBY__
            Type = TaskTriggerInfo.TriggerDaily,
#else
            Type = TaskTriggerInfoType.DailyTrigger,
#endif
            TimeOfDayTicks = TimeSpan.FromHours(1).Ticks
        };
    }

#if __EMBY__
    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
#else
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
#endif
    {
        // Stop the task if disabled.
        if (!Plugin.Instance.Configuration.EnableTrailers)
            return;

        await Task.Yield();

        progress?.Report(0);

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            MediaTypes = new[] { MediaType.Video },
#if __EMBY__
            HasAnyProviderId = new[] { Plugin.ProviderId },
            IncludeItemTypes = new[] { nameof(Movie) },
#else
            HasAnyProviderId = new Dictionary<string, string> { { Plugin.ProviderId, string.Empty } },
            IncludeItemTypes = new[] { BaseItemKind.Movie }
#endif
        }).ToList();

        foreach (var (idx, item) in items.WithIndex())
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report((double)idx / items.Count * 100);

            try
            {
                var trailersFolderPath = Path.Join(item.ContainingFolderPath, TrailersFolder);

                // Skip if contains .ignore file.
                if (File.Exists(Path.Join(trailersFolderPath, ".ignore")))
                    continue;

                var trailerUrl = item.GetTrailerUrl();

                // Skip if no remote trailers.
                if (string.IsNullOrWhiteSpace(trailerUrl))
                {
                    if (Directory.Exists(trailersFolderPath))
                    {
                        // Delete obsolete trailer files.
                        DeleteFiles(trailersFolderPath, TrailerSearchPattern);

                        // Delete directory if empty.
                        DeleteDirectoryIfEmpty(trailersFolderPath);
                    }

                    continue;
                }

                var trailerFilePath = Path.Join(trailersFolderPath,
                    $"{item.Name.Split().First()}{TrailerFileSuffix}");

#if __EMBY__
                var lastSavedUtcDateTime = item.DateLastSaved.UtcDateTime;
#else
                var lastSavedUtcDateTime = item.DateLastSaved.ToUniversalTime();
#endif

                // When trailer file already exists.
                if (File.Exists(trailerFilePath))
                {
                    // Skip if trailer file is up to date.
                    if (File.GetLastWriteTimeUtc(trailerFilePath).CompareTo(lastSavedUtcDateTime) >= 0)
                        continue;

                    // Skip if content is not modified.
                    if (string.Equals(await File.ReadAllTextAsync(trailerFilePath, cancellationToken), trailerUrl))
                    {
                        File.SetLastWriteTimeUtc(trailerFilePath, DateTime.UtcNow);
                        continue;
                    }
                }

                // Create trailers folder if not exists.
                if (!Directory.Exists(trailersFolderPath))
                    Directory.CreateDirectory(trailersFolderPath);

                // Delete other trailer files, if any.
                DeleteFiles(trailersFolderPath, TrailerSearchPattern, trailerFilePath);

                _logger.Info("Generate trailer for video {0} at {1}", item.Name, trailerFilePath);

                // Write .strm trailer file.
                await File.WriteAllTextAsync(trailerFilePath, trailerUrl, Utf8WithoutBom, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.Error("Generate trailer for video {0} error: {1}", item.Name, e.Message);
            }
        }

        progress?.Report(100);
    }

    private static void DeleteFiles(string path, string searchPattern, params string[] excludedFiles)
    {
        DeleteFiles(Directory.GetFiles(path, searchPattern).Where(file => !excludedFiles.Contains(file)));
    }

    private static void DeleteFiles(IEnumerable<string> files)
    {
        foreach (var file in files) File.Delete(file);
    }

    private static void DeleteDirectoryIfEmpty(string path)
    {
        if (!Directory.GetDirectories(path).Any() && !Directory.GetFiles(path).Any())
            Directory.Delete(path);
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/ScheduledTasks/OrganizeMetadataTask.cs
================================================
using System.Text.RegularExpressions;
using Jellyfin.Plugin.MetaTube.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
#if __EMBY__
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Logging;

#else
using MediaBrowser.Controller.Sorting;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
#endif

namespace Jellyfin.Plugin.MetaTube.ScheduledTasks;

public class OrganizeMetadataTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger _logger;

#if __EMBY__
    public OrganizeMetadataTask(ILogManager logManager, ILibraryManager libraryManager)
    {
        _logger = logManager.CreateLogger<OrganizeMetadataTask>();
        _libraryManager = libraryManager;
    }
#else
    public OrganizeMetadataTask(ILogger<OrganizeMetadataTask> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }
#endif

    public string Key => $"{Plugin.ProviderName}OrganizeMetadata";

    public string Name => "Organize Metadata";

    public string Description => $"Organizes video metadata provided by {Plugin.ProviderName} in library.";

    public string Category => Plugin.ProviderName;

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
#if __EMBY__
            Type = TaskTriggerInfo.TriggerDaily,
#else
            Type = TaskTriggerInfoType.DailyTrigger,
#endif
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
        };
    }

#if __EMBY__
    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
#else
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
#endif
    {
        await Task.Yield();

        progress?.Report(0);

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            MediaTypes = new[] { MediaType.Video },
#if __EMBY__
            HasAnyProviderId = new[] { Plugin.ProviderId },
            IncludeItemTypes = new[] { nameof(Movie) },
#else
            HasAnyProviderId = new Dictionary<string, string> { { Plugin.ProviderId, string.Empty } },
            IncludeItemTypes = new[] { BaseItemKind.Movie }
#endif
        }).ToList();

        foreach (var (idx, item) in items.WithIndex())
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report((double)idx / items.Count * 100);

            var genres = item.Genres?.ToList() ?? new List<string>();

            try
            {
                switch (HasEmbeddedChineseSubtitle(item.FileNameWithoutExtension) ||
                        HasExternalChineseSubtitle(item.Path))
                {
                    // Add `ChineseSubtitle` genre.
                    case true when !genres.Contains(ChineseSubtitle):
                    {
                        genres.Add(ChineseSubtitle);
                        if (Plugin.Instance.Configuration.EnableBadges)
                            await SetPrimaryImage(item, Plugin.Instance.Configuration.BadgeUrl, cancellationToken);
                        break;
                    }
                    // Remove `ChineseSubtitle` genre.
                    case false when genres.Contains(ChineseSubtitle):
                    {
                        genres.RemoveAll(s => s.Equals(ChineseSubtitle));
                        if (Plugin.Instance.Configuration.EnableBadges)
                            await SetPrimaryImage(item, string.Empty, cancellationToken);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error("Update ChineseSubtitle for video {0}: {1}", item.Name, e.Message);
            }

            // Remove duplicates.
            var orderedGenres =
                (Plugin.Instance.Configuration.EnableGenreSubstitution
                    // Substitute genres.
                    ? Plugin.Instance.Configuration.GetGenreSubstitutionTable().Substitute(genres)
                    : genres).Distinct().OrderByString(genre => genre).ToList();

            // Skip updating item if equal.
            if (!orderedGenres.Any() ||
                (item.Genres?.SequenceEqual(orderedGenres, StringComparer.OrdinalIgnoreCase)).GetValueOrDefault(false))
                continue;

            item.Genres = orderedGenres.ToArray();

            _logger.Info("Organize metadata for video: {0}", item.Name);

#if __EMBY__
            _libraryManager.UpdateItem(item, item, ItemUpdateType.MetadataEdit, null);
#else
            await _libraryManager
                .UpdateItemAsync(item, item, ItemUpdateType.MetadataEdit, cancellationToken)
                .ConfigureAwait(false);
#endif
        }

        progress?.Report(100);
    }

    #region Helper

    private const string ChineseSubtitle = "中文字幕";

    private static bool HasTag(string filename, string tag)
    {
        var r = new Regex(@"[-_\s]", RegexOptions.Compiled);
        return r.Split(filename).Contains(tag, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasTag(string filename, params string[] tags)
    {
        return tags.Any(tag => HasTag(filename, tag));
    }

    private static bool HasEmbeddedChineseSubtitle(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        return filename.Contains(ChineseSubtitle) || HasTag(filename, "C", "UC", "ch");
    }

    private static bool HasExternalChineseSubtitle(string path)
    {
        return HasExternalChineseSubtitle(Path.GetFileNameWithoutExtension(path),
            Directory.GetParent(path)?.GetFiles().Select(info => info.Name));
    }

    private static bool HasExternalChineseSubtitle(string basename, IEnumerable<string> files)
    {
        var r = new Regex(@"\.(ch[ist]|zho?(-(cn|hk|sg|tw))?)\.(ass|srt|ssa|smi|sub|idx|psb|vtt)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        return files.Any(name => r.IsMatch(name) &&
                                 r.Replace(name, string.Empty)
                                     .Equals(basename, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task SetPrimaryImage(BaseItem item, string badge, CancellationToken cancellationToken)
    {
        var pid = item.GetPid(Plugin.ProviderId);
        if (string.IsNullOrWhiteSpace(pid.Id) || string.IsNullOrWhiteSpace(pid.Provider))
            return;

        var m = await ApiClient.GetMovieInfoAsync(pid.Provider, pid.Id, cancellationToken);
        // Set first primary image.
        item.SetImage(new ItemImageInfo
        {
            Path = ApiClient.GetPrimaryImageApiUrl(m.Provider, m.Id, pid.Position ?? -1, badge),
            Type = ImageType.Primary
        }, 0);
    }

    #endregion
}


================================================
FILE: Jellyfin.Plugin.MetaTube/ScheduledTasks/UpdatePluginTask.cs
================================================
#if __EMBY__
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.MetaTube.Extensions;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using HttpRequestOptions = MediaBrowser.Common.Net.HttpRequestOptions;

namespace Jellyfin.Plugin.MetaTube.ScheduledTasks;

public class UpdatePluginTask : IScheduledTask
{
    private readonly IApplicationHost _applicationHost;
    private readonly IApplicationPaths _applicationPaths;
    private readonly IHttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly IZipClient _zipClient;

    public UpdatePluginTask(IApplicationHost applicationHost, IApplicationPaths applicationPaths,
        IHttpClient httpClient, ILogManager logManager, IZipClient zipClient)
    {
        _applicationHost = applicationHost;
        _applicationPaths = applicationPaths;
        _httpClient = httpClient;
        _logger = logManager.CreateLogger<UpdatePluginTask>();
        _zipClient = zipClient;
    }

    private static string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString();

    public string Key => $"{Plugin.ProviderName}UpdatePlugin";

    public string Name => "Update Plugin";

    public string Description => $"Updates {Plugin.ProviderName} plugin to latest version.";

    public string Category => Plugin.ProviderName;

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfo.TriggerDaily,
            TimeOfDayTicks = TimeSpan.FromHours(5).Ticks
        };
    }

    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        await Task.Yield();

        if (!Plugin.Instance.Configuration.EnableAutoUpdate)
        {
            _logger.Info("Auto update is disabled");
            return;
        }

        progress?.Report(0);

        try
        {
            var apiResult = JsonSerializer.Deserialize<ApiResponseInfo>(await _httpClient.Get(new HttpRequestOptions
            {
                Url = "https://api.github.com/repos/metatube-community/jellyfin-plugin-metatube/releases/latest",
                CancellationToken = cancellationToken,
                AcceptHeader = "application/json",
                EnableDefaultUserAgent = true
            }).ConfigureAwait(false));

            var currentVersion = ParseVersion(CurrentVersion);
            var remoteVersion = ParseVersion(apiResult?.TagName);

            if (currentVersion.CompareTo(remoteVersion) < 0)
            {
                _logger.Info("Found new plugin version: {0}", remoteVersion);

                var url = apiResult?.Assets
                    .Where(asset => asset.Name.StartsWith("Emby") && asset.Name.EndsWith(".zip")).ToArray()
                    .FirstOrDefault()
                    ?.BrowserDownloadUrl;
                if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    throw new Exception("Invalid download url");

                var zipStream = await _httpClient.Get(new HttpRequestOptions
                {
                    Url = url,
                    CancellationToken = cancellationToken,
                    EnableDefaultUserAgent = true,
                    Progress = progress
                }).ConfigureAwait(false);

                _zipClient.ExtractAllFromZip(zipStream, _applicationPaths.PluginsPath, true);

                _logger.Info("Plugin update complete");

                _applicationHost.NotifyPendingRestart();
            }
            else
            {
                _logger.Info("No need to update");
            }
        }
        catch (Exception e)
        {
            _logger.Error("Update error: {0}", e.Message);
        }

        progress?.Report(100);
    }

    private static Version ParseVersion(string v)
    {
        return new Version(v.StartsWith("v") ? v[1..] : v);
    }

    private class ApiResponseInfo
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }

        [JsonPropertyName("assets")]
        public ApiAssetInfo[] Assets { get; set; }
    }

    private class ApiAssetInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; }
    }
}

#endif


================================================
FILE: Jellyfin.Plugin.MetaTube/Translation/TranslationEngine.cs
================================================
using System.ComponentModel;

namespace Jellyfin.Plugin.MetaTube.Translation;

public enum TranslationEngine
{
    [Description("Baidu")]
    Baidu,

    [Description("Google")]
    Google,

    [Description("Google (Free)")]
    GoogleFree,

    [Description("DeepL")]
    DeepL,

    [Description("OpenAI")]
    OpenAi
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Translation/TranslationHelper.cs
================================================
using System.Collections.Specialized;
using Jellyfin.Plugin.MetaTube.Configuration;
using Jellyfin.Plugin.MetaTube.Metadata;

namespace Jellyfin.Plugin.MetaTube.Translation;

public static class TranslationHelper
{
    private const string AutoLanguageCode = "auto";
    private const string JapaneseLanguageCode = "ja";

    private static readonly SemaphoreSlim Semaphore = new(1);

    private static PluginConfiguration Configuration => Plugin.Instance.Configuration;

    private static async Task<string> TranslateAsync(string q, string from, string to,
        CancellationToken cancellationToken)
    {
        int millisecondsDelay;
        var nv = new NameValueCollection();
        switch (Configuration.TranslationEngine)
        {
            case TranslationEngine.Baidu:
                millisecondsDelay = 1000; // Limit Baidu API request rate to 1 rps.
                nv.Add(new NameValueCollection
                {
                    { "baidu-app-id", Configuration.BaiduAppId },
                    { "baidu-app-key", Configuration.BaiduAppKey }
                });
                break;
            case TranslationEngine.Google:
                millisecondsDelay = 100; // Limit Google API request rate to 10 rps.
                nv.Add(new NameValueCollection
                {
                    { "google-api-key", Configuration.GoogleApiKey },
                    { "google-api-url", Configuration.GoogleApiUrl }
                });
                break;
            case TranslationEngine.GoogleFree:
                millisecondsDelay = 100;
                nv.Add(new NameValueCollection());
                break;
            case TranslationEngine.DeepL:
                millisecondsDelay = 100;
                nv.Add(new NameValueCollection
                {
                    { "deepl-api-key", Configuration.DeepLApiKey },
                    { "deepl-api-url", Configuration.DeepLApiUrl }
                });
                break;
            case TranslationEngine.OpenAi:
                millisecondsDelay = 1000;
                nv.Add(new NameValueCollection
                {
                    { "openai-api-key", Configuration.OpenAiApiKey },
                    { "openai-api-url", Configuration.OpenAiApiUrl },
                    { "openai-model", Configuration.OpenAiModel }
                });
                break;
            default:
                throw new ArgumentException($"Invalid translation engine: {Configuration.TranslationEngine}");
        }

        await Semaphore.WaitAsync(cancellationToken);

        try
        {
            async Task<string> TranslateWithDelay()
            {
                await Task.Delay(millisecondsDelay, cancellationToken);
                return (await ApiClient
                    .TranslateAsync(q, from, to, Configuration.TranslationEngine.ToString(), nv, cancellationToken)
                    .ConfigureAwait(false)).TranslatedText;
            }

            return await RetryAsync(TranslateWithDelay, 5);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public static async Task TranslateAsync(MovieInfo m, string to, CancellationToken cancellationToken)
    {
        if (string.Equals(to, JapaneseLanguageCode, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"language not allowed: {to}");

        if (Configuration.TranslationMode.HasFlag(TranslationMode.Title) && !string.IsNullOrWhiteSpace(m.Title))
            m.Title = await TranslateAsync(m.Title, AutoLanguageCode, to, cancellationToken);

        if (Configuration.TranslationMode.HasFlag(TranslationMode.Summary) && !string.IsNullOrWhiteSpace(m.Summary))
            m.Summary = await TranslateAsync(m.Summary, AutoLanguageCode, to, cancellationToken);
    }

    private static async Task<T> RetryAsync<T>(Func<Task<T>> func, int retryCount)
    {
        while (true)
        {
            try
            {
                return await func();
            }
            catch when (--retryCount > 0)
            {
            }
        }
    }
}


================================================
FILE: Jellyfin.Plugin.MetaTube/Translation/TranslationMode.cs
================================================
using System.ComponentModel;

namespace Jellyfin.Plugin.MetaTube.Translation;

public enum TranslationMode
{
    [Description("Disabled")]
    Disabled,

    [Description("Title")]
    Title,

    [Description("Summary")]
    Summary,

    [Description("Title and Summary")]
    Both
}


================================================
FILE: scripts/manifest.py
================================================
#!/usr/bin/env python3
import hashlib
import json
import os
import sys
import xml.etree.ElementTree as ET
from datetime import datetime
from urllib.request import urlopen
from packaging.version import Version


def md5sum(filename) -> str:
    with open(filename, 'rb') as f:
        return hashlib.md5(f.read()).hexdigest()


def get_jellyfin_version(csproj: str) -> str:
    tree = ET.parse(csproj)
    root = tree.getroot()

    for pkg in root.iter("PackageReference"):
        if pkg.attrib.get("Include") in ("Jellyfin.Controller", "Jellyfin.Model"):
            return Version(pkg.attrib.get("Version")).base_version

    raise Exception("Jellyfin version not found")


def generate(filename, version, csproj) -> dict:
    return {
        'checksum': md5sum(filename),
        'changelog': 'Auto Released by Actions',
        'targetAbi': f'{get_jellyfin_version(csproj)}.0',
        'sourceUrl': 'https://github.com/metatube-community/jellyfin-plugin-metatube/releases/download/'
                     f'v{version}/Jellyfin.MetaTube@v{version}.zip',
        'timestamp': datetime.now().strftime('%Y-%m-%dT%H:%M:%SZ'),
        'version': version
    }


def main() -> None:
    filename = sys.argv[1]
    version = filename.split('@', maxsplit=1)[1] \
        .removeprefix('v') \
        .removesuffix('.zip')

    csproj = os.path.join(os.path.dirname(__file__),
                          "../Jellyfin.Plugin.MetaTube/Jellyfin.Plugin.MetaTube.csproj")

    with urlopen(
            'https://raw.githubusercontent.com/metatube-community/jellyfin-plugin-metatube/dist/manifest.json') as f:
        manifest = json.load(f)

    manifest[0]['versions'].insert(0, generate(filename, version, csproj))

    with open('manifest.json', 'w') as f:
        json.dump(manifest, f, indent=2)


if __name__ == '__main__':
    main()



================================================
FILE: .github/FUNDING.yml
================================================
# These are supported funding model platforms

github: [xjasonlyu]
patreon: # Replace with a single Patreon username
open_collective: # Replace with a single Open Collective username
ko_fi: # Replace with a single Ko-fi username
tidelift: # Replace with a single Tidelift platform-name/package-name e.g., npm/babel
community_bridge: # Replace with a single Community Bridge project-name e.g., cloud-foundry
liberapay: # Replace with a single Liberapay username
issuehunt: # Replace with a single IssueHunt username
otechie: # Replace with a single Otechie username
lfx_crowdfunding: # Replace with a single LFX Crowdfunding project-name e.g., cloud-foundry
custom: # Replace with up to 4 custom sponsorship URLs e.g., ['link1', 'link2']



================================================
FILE: .github/ISSUE_TEMPLATE/bug_report.yml
================================================
name: Bug report
description: Create a report to help us improve
title: "[Bug] "
body:
  - type: checkboxes
    id: ensure
    attributes:
      label: Verify steps
      description: Please verify that you've followed these steps
      options:
        - label: Is this something you can **debug and fix**? Send a pull request! Bug fixes and documentation fixes are welcome.
          required: true

        - label: I have read the [Wiki](https://metatube-community.github.io/wiki/), especially the [FAQ](https://metatube-community.github.io/faq/) page.
          required: true

        - label: I have searched on the [issue tracker](……/) for a related issue.
          required: true

  - type: input
    attributes:
      label: MetaTube Plugin Version
    validations:
      required: true

  - type: input
    attributes:
      label: MetaTube Server Version
    validations:
      required: true

  - type: dropdown
    id: os
    attributes:
      label: What OS are you seeing the problem on?
      multiple: true
      options:
        - Windows
        - Linux
        - macOS
        - Other

  - type: textarea
    attributes:
      label: Description
    validations:
      required: true

  - type: textarea
    attributes:
      label: MetaTube Server
      description: Paste the command line parameters or environment below.

  - type: textarea
    attributes:
      label: Jellyfin/Emby Logs
      description: Paste the Jellyfin/Emby logs below.

  - type: textarea
    attributes:
      label: MetaTube Server Logs
      description: Paste the MetaTube server logs below.

  - type: textarea
    attributes:
      label: How to Reproduce
      description: Steps to reproduce the behavior, if any.



================================================
FILE: .github/ISSUE_TEMPLATE/config.yml
================================================
blank_issues_enabled: false

contact_links:
  - name: MetaTube GitHub Wiki
    url: https://metatube-community.github.io/wiki/
    about: Please see the wiki for configuration and examples
  - name: MetaTube GitHub Discussions
    url: https://github.com/metatube-community/jellyfin-plugin-metatube/discussions
    about: Ask questions and get help on GitHub Discussions



================================================
FILE: .github/ISSUE_TEMPLATE/feature_request.yml
================================================
name: Feature request
description: Suggest an idea or improvement
title: "[Feature] "
body:
  - type: textarea
    id: description
    attributes:
      label: Description
      placeholder: A clear description of the feature or enhancement.
    validations:
      required: true

  - type: textarea
    id: related
    attributes:
      label: Is this feature related to a specific bug?
      description: Please include a bug references if yes.

  - type: textarea
    id: solution
    attributes:
      label: Do you have a specific solution in mind?
      description: >
        Please include any details about a solution that you have in mind,
        including any alternatives considered.



================================================
FILE: .github/workflows/dotnetcore.yml
================================================
name: .NET

on:
  workflow_dispatch:

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET Core
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.x'

      - name: Setup Python
        uses: actions/setup-python@v5
        with:
          python-version: '3.12'

      - name: Generate Version
        id: shell
        run: |
          echo "version=$(date -u '+%Y.%-m%d.%-H%M.0')" >> $GITHUB_OUTPUT

      - name: Build Plugins
        run: |
          dotnet build --configuration Release -p:Version=${{ steps.shell.outputs.version }}
          dotnet build --configuration Release.Emby -p:Version=${{ steps.shell.outputs.version }}

      - name: Generate Manifest
        run: |
          python3 -m pip install packaging
          python3 scripts/manifest.py Jellyfin.Plugin.MetaTube/bin/Jellyfin.MetaTube@v${{ steps.shell.outputs.version }}.zip

      - name: Publish Manifest
        run: |
          git config --global user.name  'metatube-bot'
          git config --global user.email 'metatube-bot@users.noreply.github.com'
          git remote set-url origin https://x-access-token:${{ secrets.GITHUB_TOKEN }}@github.com/${GITHUB_REPOSITORY}

          git checkout --orphan dist
          git rm -rf .
          git add manifest.json
          git commit -m "Auto Updated by Actions"
          git push -f -u origin dist

      - name: Upload Plugins
        uses: softprops/action-gh-release@v2
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        with:
          body: _Auto Released by Actions_
          draft: false
          tag_name: v${{ steps.shell.outputs.version }}
          files: |
            Jellyfin.Plugin.MetaTube/bin/Jellyfin.MetaTube@v${{ steps.shell.outputs.version }}.zip
            Jellyfin.Plugin.MetaTube/bin/Emby.MetaTube@v${{ steps.shell.outputs.version }}.zip



================================================
FILE: .github/workflows/stale.yml
================================================
name: Mark stale issues and pull requests

on:
  schedule:
    - cron: "0 10 * * *"

jobs:
  stale:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/stale@v9
        with:
          stale-issue-message: 'This issue is stale because it has been open 60 days with no activity. Remove stale label or comment or this will be closed in 7 days'
          exempt-issue-labels: 'question,bug,enhancement'
          days-before-stale: 30
          days-before-close: 7


