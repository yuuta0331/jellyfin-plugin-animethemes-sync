using System;
using System.Collections.Generic;

namespace AnimeThemesSync.Shared.Models;

public sealed record ThemeBrowserLibraryItem(
    Guid Id,
    string Name,
    string Type,
    string? AnimeThemesSlug,
    string? AniListId,
    string? MyAnimeListId);

public sealed record ThemeBrowserItemResult(
    Guid ItemId,
    string Name,
    string Type,
    string? AnimeThemesSlug,
    string? AnimeThemesUrl,
    List<ThemeBrowserThemeRow> Themes);

public sealed record ThemeBrowserThemeRow(
    string ThemeKey,
    string Type,
    int? Sequence,
    int? Version,
    string? Slug,
    string? Group,
    string? Episodes,
    bool Spoiler,
    bool Nsfw,
    string? Notes,
    string? SongTitle,
    string? Artists,
    string? Quality,
    string? Labels,
    string? VideoUrl,
    string? AudioUrl,
    string? BackdropPath,
    bool BackdropExists,
    string? ThemeMusicPath,
    bool ThemeMusicExists,
    string? ExtraPath,
    bool ExtraExists,
    string? AnimeThemesUrl);
