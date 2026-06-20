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
    string? ThumbImageUrl,
    int ThemeVideos,
    int ThemeSongs,
    int Extras,
    long TotalBytes,
    bool HasSavedFiles,
    DateTimeOffset DateCreated,
    DateTimeOffset? LatestEpisodeDateCreated,
    string LinkStatus,
    bool HasDirectLink,
    bool HasManualSeasonLink);

public sealed record ThemeBrowserItemResult(
    Guid ItemId,
    string Name,
    string Type,
    string? AnimeThemesSlug,
    string? AnimeThemesUrl,
    List<ThemeBrowserThemeRow> Themes,
    List<ThemeBrowserThemeGroup>? Groups = null);

public sealed record ThemeBrowserThemeGroup(
    Guid ItemId,
    Guid? SeriesItemId,
    Guid? SeasonItemId,
    string Name,
    string Type,
    int? SeasonNumber,
    string Status,
    string Source,
    bool SameAsSeries,
    string? AnimeName,
    string? AnimeThemesSlug,
    string? AnimeThemesUrl,
    string? PrimaryImageUrl,
    string? BackdropImageUrl,
    string? ThumbImageUrl,
    string? EmptyMessage,
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
    long TotalBytes,
    int SeriesItems = 0,
    int MovieItems = 0,
    int SeasonItems = 0,
    int SavedItems = 0,
    int ManualSeasonMappings = 0,
    int AutoSeasonMappings = 0,
    int DirectSeasonMappings = 0,
    int SeriesSharedSeasons = 0,
    int UnmatchedSeasons = 0);

public sealed record ThemeDeleteResult(
    int FilesDeleted,
    long BytesDeleted);

public sealed record SeasonThemeMappingImportResult(
    int Imported,
    int Skipped,
    List<string> Errors);

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
    int Score,
    string? PrimaryImageUrl,
    string? MediaFormat,
    string? MatchedTitle,
    string? MatchedTitleType);

public sealed class SaveSeasonThemeMappingRequest
{
    public Guid SeasonItemId { get; set; }

    public string? AnimeThemesSlug { get; set; }

    public int? AniListId { get; set; }

    public int? MyAnimeListId { get; set; }

    public bool Locked { get; set; }
}

public sealed class ImportSeasonThemeMappingsRequest
{
    public List<ImportSeasonThemeMappingRow> Mappings { get; set; } = [];
}

public sealed class ImportSeasonThemeMappingRow
{
    public Guid SeasonItemId { get; set; }

    public string? AnimeThemesSlug { get; set; }

    public int? AniListId { get; set; }

    public int? MyAnimeListId { get; set; }

    public bool? Locked { get; set; }

    public string? Status { get; set; }
}
