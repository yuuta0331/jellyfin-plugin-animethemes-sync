using System;
using System.Collections.Generic;

namespace AnimeThemesSync.Shared.Models;

public sealed record ThemeBrowserLibraryItem(
    Guid Id,
    string Name,
    string Type,
    string? AnimeThemesSlug,
    string? AniListId,
    string? MyAnimeListId,
    string? PrimaryImageUrl,
    string? LogoImageUrl,
    string? BackdropImageUrl,
    string? ThumbImageUrl);

public sealed record ThemeBrowserItemResult(
    Guid ItemId,
    string Name,
    string Type,
    string? AnimeThemesSlug,
    string? AnimeThemesUrl,
    List<ThemeBrowserThemeRow> Themes);

public sealed record ThemeBrowserThemeRow(
    string RowId,
    int Order,
    int ThemeId,
    int EntryId,
    int VideoId,
    int? AudioId,
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
    bool SavedVideoPlayable,
    string? ThemeMusicPath,
    bool ThemeMusicExists,
    bool SavedAudioPlayable,
    string? ExtraPath,
    bool ExtraExists,
    bool SavedExtraPlayable,
    string? AnimeThemesUrl);

public sealed record ThemeLocalMediaResult(
    string Path,
    string ContentType,
    string FileName);
