using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class AnimeMatchHelperTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    [InlineData("K-ON!!", "k on")]
    [InlineData("Fate/stay night: UBW", "fate stay night ubw")]
    [InlineData("  Attack   on Titan  ", "attack on titan")]
    public void NormalizeSearchText_CollapsesToLettersDigitsAndSingleSpaces(string? input, string expected)
    {
        Assert.Equal(expected, AnimeMatchHelper.NormalizeSearchText(input));
    }

    [Fact]
    public void ScoreSearchCandidate_RanksExactPartialSlugAndYear()
    {
        var exact = Anime("Example Show", "other_slug", 2024);
        var partial = Anime("Example Show Second Season", "other_slug2", 2024);
        var slugOnly = Anime("Different Name", "example_show", 2024);
        var unrelated = Anime("Unrelated", "unrelated", 2024);

        Assert.Equal(130, AnimeMatchHelper.ScoreSearchCandidate(exact, "Example Show", 2024));
        Assert.Equal(85, AnimeMatchHelper.ScoreSearchCandidate(partial, "Example Show", 2023));
        Assert.Equal(55, AnimeMatchHelper.ScoreSearchCandidate(slugOnly, "Example Show", null));
        Assert.Equal(30, AnimeMatchHelper.ScoreSearchCandidate(unrelated, "Example Show", 2024));
    }

    [Fact]
    public void ScoreSearchCandidate_MatchesSynonyms()
    {
        var anime = Anime("Native Title", "some_slug", null);
        anime.Synonyms = [new AnimeThemesSynonym { Text = "English Title", Type = "English" }];

        Assert.Equal(100, AnimeMatchHelper.ScoreSearchCandidate(anime, "english title", null));
    }

    [Fact]
    public void FindMatchedTitle_ReturnsMatchingSynonymAndType()
    {
        var anime = Anime("Native", "slug", null);
        anime.Synonyms =
        [
            new AnimeThemesSynonym { Text = "Other", Type = "Short" },
            new AnimeThemesSynonym { Text = "English Title", Type = "English" },
        ];

        var match = AnimeMatchHelper.FindMatchedTitle(anime, "english title");
        Assert.Equal("English Title", match.Title);
        Assert.Equal("English", match.Type);

        Assert.Equal((null, null), AnimeMatchHelper.FindMatchedTitle(anime, "no such name"));
        Assert.Equal((null, null), AnimeMatchHelper.FindMatchedTitle(anime, "  "));
    }

    [Fact]
    public void ExtractAnimeExternalIds_ReadsAniListAndMyAnimeList()
    {
        var anime = Anime("Example", "example", null);
        anime.Resources =
        [
            new AnimeThemesResource { Site = "AniList", ExternalId = 111 },
            new AnimeThemesResource { Site = "MyAnimeList", ExternalId = 222 },
            new AnimeThemesResource { Site = "anidb", ExternalId = 333 },
            new AnimeThemesResource { Site = null, ExternalId = 444 },
        ];

        var ids = AnimeMatchHelper.ExtractAnimeExternalIds(anime);
        Assert.Equal(111, ids.AniListId);
        Assert.Equal(222, ids.MyAnimeListId);
        Assert.Equal((null, null), AnimeMatchHelper.ExtractAnimeExternalIds(null));
    }

    [Fact]
    public void IsSameAnime_PrefersIdsAndFallsBackToSlugs()
    {
        Assert.True(AnimeMatchHelper.IsSameAnime(Anime("A", "a", null, id: 5), Anime("B", "b", null, id: 5)));
        Assert.False(AnimeMatchHelper.IsSameAnime(Anime("A", "same", null, id: 5), Anime("A", "same", null, id: 6)));
        Assert.True(AnimeMatchHelper.IsSameAnime(Anime("A", "Same_Slug", null), Anime("B", "same_slug", null)));
        Assert.False(AnimeMatchHelper.IsSameAnime(Anime("A", null, null), Anime("A", null, null)));
    }

    [Fact]
    public void GetAnimePrimaryImageUrl_PrefersSmallCoverThenLargeCoverThenAny()
    {
        var anime = Anime("Example", "example", null);
        anime.Images =
        [
            new AnimeThemesImage { Facet = "Grill", Link = "https://img/grill" },
            new AnimeThemesImage { Facet = "Large Cover", Link = "https://img/large" },
            new AnimeThemesImage { Facet = "Small Cover", Link = "https://img/small" },
        ];
        Assert.Equal("https://img/small", AnimeMatchHelper.GetAnimePrimaryImageUrl(anime));

        anime.Images.RemoveAt(2);
        Assert.Equal("https://img/large", AnimeMatchHelper.GetAnimePrimaryImageUrl(anime));

        anime.Images.RemoveAt(1);
        Assert.Equal("https://img/grill", AnimeMatchHelper.GetAnimePrimaryImageUrl(anime));

        Assert.Null(AnimeMatchHelper.GetAnimePrimaryImageUrl(null));
    }

    [Fact]
    public void BuildAnimeThemesUrl_UsesSlug()
    {
        Assert.Equal("https://animethemes.moe/anime/example_show", AnimeMatchHelper.BuildAnimeThemesUrl(Anime("Example", "example_show", null)));
        Assert.Null(AnimeMatchHelper.BuildAnimeThemesUrl(Anime("Example", null, null)));
        Assert.Null(AnimeMatchHelper.BuildAnimeThemesUrl(null));
    }

    [Fact]
    public void ToThemeFinderSearchResult_MapsIdsUrlAndMatchedTitle()
    {
        var anime = Anime("Example Show", "example_show", 2024, id: 42);
        anime.Season = "Spring";
        anime.Synonyms = [new AnimeThemesSynonym { Text = "Example Show", Type = "English" }];
        anime.Resources = [new AnimeThemesResource { Site = "anilist", ExternalId = 111 }];

        var result = AnimeMatchHelper.ToThemeFinderSearchResult(anime, 130, "https://img/small", "example show");

        Assert.Equal(42, result.AnimeThemesId);
        Assert.Equal("Example Show", result.Name);
        Assert.Equal(111, result.AniListId);
        Assert.Null(result.MyAnimeListId);
        Assert.Equal("https://animethemes.moe/anime/example_show", result.AnimeThemesUrl);
        Assert.Equal(130, result.Score);
        Assert.Equal("Example Show", result.MatchedTitle);
        Assert.Equal("English", result.MatchedTitleType);
    }

    private static AnimeThemesAnime Anime(string? name, string? slug, int? year, int id = 0)
    {
        return new AnimeThemesAnime
        {
            Id = id,
            Name = name,
            Slug = slug,
            Year = year,
        };
    }
}
