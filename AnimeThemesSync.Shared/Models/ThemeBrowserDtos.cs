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

public sealed record ThemeBrowserSummary(
    int Items,
    int ThemeVideos,
    int ThemeSongs,
    int Extras,
    long TotalBytes);

public sealed record ThemeDeleteResult(
    int FilesDeleted,
    long BytesDeleted);

public sealed record SeasonThemeMappingRow(
    Guid SeriesItemId,
    string SeriesName,
    string? SeriesPath,
    Guid SeasonItemId,
    string SeasonName,
    string? SeasonPath,
    int? SeasonNumber,
    string Status,
    string Source,
    bool SameAsSeries,
    string? AnimeName,
    int? AnimeThemesId,
    string? AnimeThemesSlug,
    string? AnimeThemesUrl,
    int? AniListId,
    int? MyAnimeListId,
    string? PrimaryImageUrl);

public sealed record ThemeFinderSearchResult(
    int AnimeThemesId,
    string Name,
    string? Slug,
    int? Year,
    string? Season,
    int? AniListId,
    int? MyAnimeListId,
    string? AnimeThemesUrl,
    int Score);

public sealed class SaveSeasonThemeMappingRequest
{
    public Guid SeasonItemId { get; set; }

    public string? AnimeThemesSlug { get; set; }

    public int? AniListId { get; set; }

    public int? MyAnimeListId { get; set; }

    public bool Locked { get; set; }
}
