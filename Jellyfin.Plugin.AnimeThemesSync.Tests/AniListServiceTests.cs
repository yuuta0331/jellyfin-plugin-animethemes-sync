using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AnimeThemesSync.Services;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests
{
    public class AniListServiceTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<AniListService>> _mockLogger;
        private readonly MockHttpMessageHandler _mockHttp;
        private readonly AniListService _service;

        public AniListServiceTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<AniListService>>();
            _mockHttp = new MockHttpMessageHandler();

            var client = _mockHttp.ToHttpClient();
            client.BaseAddress = new Uri("https://graphql.anilist.co");
            _mockHttpClientFactory.Setup(x => x.CreateClient("AniList")).Returns(client);

            var rateLimiterLogger = new Mock<ILogger<RateLimiter>>();
            var rateLimiter = new RateLimiter(rateLimiterLogger.Object, "AniList", 90);

            _service = new AniListService(_mockHttpClientFactory.Object, _mockLogger.Object, rateLimiter);
        }

        [Fact]
        public async Task SearchAnime_ExactMatch_ReturnsIds()
        {
            // Arrange
            var animeName = "Neon Genesis Evangelion";
            var year = 1995;
            var responseJson = @"{
                ""data"": {
                    ""Page"": {
                        ""media"": [
                            {
                                ""id"": 30,
                                ""idMal"": 30,
                                ""title"": { ""english"": ""Neon Genesis Evangelion"" },
                                ""startDate"": { ""year"": 1995 }
                            }
                        ]
                    }
                }
            }";

            _mockHttp.When("https://graphql.anilist.co")
                .Respond("application/json", responseJson);

            // Act
            var result = await _service.SearchAnime(animeName, year, CancellationToken.None);

            // Assert
            Assert.Equal(30, result.AniListId);
            Assert.Equal(30, result.MalId);
        }

        [Fact]
        public async Task SearchAnime_TolerantMatch_ReturnsIds()
        {
            // Arrange
            var animeName = "Some Anime";
            var searchYear = 2020;
            var actualYear = 2021; // Within +1 tolerance
            var responseJson = $@"{{
                ""data"": {{
                    ""Page"": {{
                        ""media"": [
                            {{
                                ""id"": 100,
                                ""idMal"": 200,
                                ""title"": {{ ""english"": ""Some Anime"" }},
                                ""startDate"": {{ ""year"": {actualYear} }}
                            }}
                        ]
                    }}
                }}
            }}";

            _mockHttp.When("https://graphql.anilist.co")
                .Respond("application/json", responseJson);

            // Act
            var result = await _service.SearchAnime(animeName, searchYear, CancellationToken.None);

            // Assert
            Assert.Equal(100, result.AniListId);
            Assert.Equal(200, result.MalId);
        }

        [Fact]
        public async Task SearchAnime_NoMatchWithYear_ReturnsNull()
        {
            // Arrange
            var animeName = "Future Anime";
            var searchYear = 2020;
            var actualYear = 2025; // Outside tolerance
            var responseJson = $@"{{
                ""data"": {{
                    ""Page"": {{
                        ""media"": [
                            {{
                                ""id"": 999,
                                ""idMal"": 999,
                                ""title"": {{ ""english"": ""Future Anime"" }},
                                ""startDate"": {{ ""year"": {actualYear} }}
                            }}
                        ]
                    }}
                }}
            }}";

            _mockHttp.When("https://graphql.anilist.co")
                .Respond("application/json", responseJson);

            // Act
            var result = await _service.SearchAnime(animeName, searchYear, CancellationToken.None);

            // Assert
            Assert.Null(result.AniListId);
            Assert.Null(result.MalId);
        }

        [Fact]
        public async Task SearchAnime_ApiError_ReturnsNullAndLogsError()
        {
            // Arrange
            _mockHttp.When("https://graphql.anilist.co")
                .Respond(HttpStatusCode.InternalServerError);

            // Act
            var result = await _service.SearchAnime("Error Anime", 2020, CancellationToken.None);

            // Assert
            Assert.Null(result.AniListId);
            Assert.Null(result.MalId);
        }

        // =====================================================
        //  New: Title scoring & year composite matching tests
        // =====================================================

        [Fact]
        public async Task SearchAnime_MultipleResults_SelectsBestByYearAndTitle()
        {
            // Arrange: "Black Rock Shooter" returns OVA (2010), TV (2012), Dawn Fall (2022)
            // Searching for year=2012 should match the TV version
            var responseJson = @"{
                ""data"": {
                    ""Page"": {
                        ""media"": [
                            {
                                ""id"": 11285,
                                ""idMal"": 11285,
                                ""title"": { ""romaji"": ""Black★Rock Shooter (TV)"", ""english"": ""Black★Rock Shooter"", ""native"": ""ブラック★ロックシューター (TV)"" },
                                ""startDate"": { ""year"": 2012 }
                            },
                            {
                                ""id"": 7059,
                                ""idMal"": 7059,
                                ""title"": { ""romaji"": ""Black★Rock Shooter"", ""english"": ""Black★Rock Shooter"", ""native"": ""ブラック★ロックシューター"" },
                                ""startDate"": { ""year"": 2010 }
                            },
                            {
                                ""id"": 131547,
                                ""idMal"": 49895,
                                ""title"": { ""romaji"": ""Black★Rock Shooter: Dawn Fall"", ""english"": ""Black Rock Shooter: Dawn Fall"", ""native"": ""ブラック★★ロックシューター DAWN FALL"" },
                                ""startDate"": { ""year"": 2022 }
                            }
                        ]
                    }
                }
            }";

            _mockHttp.When("https://graphql.anilist.co")
                .Respond("application/json", responseJson);

            // Act — searching with year 2012
            var result = await _service.SearchAnime("ブラック★ロックシューター", 2012, CancellationToken.None);

            // Assert — should select TV (2012) id=11285
            Assert.Equal(11285, result.AniListId);
            Assert.Equal(11285, result.MalId);
        }

        [Fact]
        public async Task SearchAnime_NoYear_SelectsBestByTitle()
        {
            // Arrange: searching without year should pick the exact title match
            var responseJson = @"{
                ""data"": {
                    ""Page"": {
                        ""media"": [
                            {
                                ""id"": 7059,
                                ""idMal"": 7059,
                                ""title"": { ""romaji"": ""Black★Rock Shooter"", ""english"": ""Black★Rock Shooter"", ""native"": ""ブラック★ロックシューター"" },
                                ""startDate"": { ""year"": 2010 }
                            },
                            {
                                ""id"": 11285,
                                ""idMal"": 11285,
                                ""title"": { ""romaji"": ""Black★Rock Shooter (TV)"", ""english"": ""Black Rock Shooter (TV)"", ""native"": ""ブラック★ロックシューター (TV)"" },
                                ""startDate"": { ""year"": 2012 }
                            }
                        ]
                    }
                }
            }";

            _mockHttp.When("https://graphql.anilist.co")
                .Respond("application/json", responseJson);

            // Act — no year, search by "ブラック★ロックシューター"
            var result = await _service.SearchAnime("ブラック★ロックシューター", null, CancellationToken.None);

            // Assert — should pick exact native title match (OVA, id=7059)
            Assert.Equal(7059, result.AniListId);
            Assert.Equal(7059, result.MalId);
        }

        [Fact]
        public async Task SearchAnime_BlackRockShooter_TV_WithYear()
        {
            // Arrange: user searches "Black Rock Shooter" with year=2012
            // The OVA (2010) has exact name match but wrong year
            // The TV (2012) has "(TV)" suffix but correct year
            var responseJson = @"{
                ""data"": {
                    ""Page"": {
                        ""media"": [
                            {
                                ""id"": 7059,
                                ""idMal"": 7059,
                                ""title"": { ""romaji"": ""Black★Rock Shooter"", ""english"": ""Black★Rock Shooter"" },
                                ""startDate"": { ""year"": 2010 }
                            },
                            {
                                ""id"": 11285,
                                ""idMal"": 11285,
                                ""title"": { ""romaji"": ""Black★Rock Shooter (TV)"", ""english"": ""Black Rock Shooter (TV)"" },
                                ""startDate"": { ""year"": 2012 }
                            }
                        ]
                    }
                }
            }";

            _mockHttp.When("https://graphql.anilist.co")
                .Respond("application/json", responseJson);

            // Act
            var result = await _service.SearchAnime("Black Rock Shooter", 2012, CancellationToken.None);

            // Assert — year 2012 match (TV) should win over exact title match (OVA 2010)
            // OVA: titleScore=5 (normalized match) + yearPenalty=200 (2 years off) = 205
            // TV:  titleScore=15 (normalized containment) + yearPenalty=0 (exact year) = 15
            Assert.Equal(11285, result.AniListId);
        }

        // =====================================================
        //  Unit tests for static scoring methods
        // =====================================================

        [Fact]
        public void ScoreTitle_ExactMatch_Returns0()
        {
            var title = new AniListService.AniListTitle
            {
                Romaji = "Neon Genesis Evangelion",
                English = "Neon Genesis Evangelion"
            };

            var score = AniListService.ScoreTitle(title, "Neon Genesis Evangelion");
            Assert.Equal(0, score);
        }

        [Fact]
        public void ScoreTitle_NormalizedMatch_Returns5()
        {
            // ★ is stripped during normalization
            var title = new AniListService.AniListTitle
            {
                Romaji = "Black★Rock Shooter",
                English = "Black★Rock Shooter"
            };

            var score = AniListService.ScoreTitle(title, "Black Rock Shooter");
            Assert.Equal(5, score);
        }

        [Fact]
        public void ScoreTitle_ContainmentMatch_Returns10()
        {
            var title = new AniListService.AniListTitle
            {
                Romaji = "Black★Rock Shooter (TV)"
            };

            var score = AniListService.ScoreTitle(title, "Black★Rock Shooter");
            Assert.Equal(10, score);
        }

        [Fact]
        public void ScoreTitle_NullTitle_Returns50()
        {
            var score = AniListService.ScoreTitle(null, "Anything");
            Assert.Equal(50, score);
        }

        [Fact]
        public void NormalizeTitle_RemovesSpecialChars()
        {
            Assert.Equal("Black Rock Shooter", AniListService.NormalizeTitle("Black★Rock Shooter"));
            Assert.Equal("Test Title", AniListService.NormalizeTitle("Test! Title?"));
            Assert.Equal("ブラック ロックシューター", AniListService.NormalizeTitle("ブラック★ロックシューター"));
        }

        [Fact]
        public void ScoreCandidate_ExactTitleExactYear_Returns0()
        {
            var media = new AniListService.AniListMedia
            {
                Id = 1,
                Title = new AniListService.AniListTitle { Romaji = "Test" },
                StartDate = new AniListService.AniListDate { Year = 2020 }
            };

            var score = AniListService.ScoreCandidate(media, "Test", 2020);
            Assert.Equal(0, score); // title=0 + year=0
        }

        [Fact]
        public void ScoreCandidate_ExactTitleWrongYear_HighPenalty()
        {
            var media = new AniListService.AniListMedia
            {
                Id = 1,
                Title = new AniListService.AniListTitle { Romaji = "Test" },
                StartDate = new AniListService.AniListDate { Year = 2015 }
            };

            var score = AniListService.ScoreCandidate(media, "Test", 2020);
            Assert.Equal(500, score); // title=0 + year=5*100=500
        }

        [Fact]
        public void SelectBestMatch_PicksLowestScore()
        {
            var candidates = new System.Collections.Generic.List<AniListService.AniListMedia>
            {
                new AniListService.AniListMedia
                {
                    Id = 1, IdMal = 1,
                    Title = new AniListService.AniListTitle { Romaji = "Wrong Title" },
                    StartDate = new AniListService.AniListDate { Year = 2020 }
                },
                new AniListService.AniListMedia
                {
                    Id = 2, IdMal = 2,
                    Title = new AniListService.AniListTitle { Romaji = "Correct Title" },
                    StartDate = new AniListService.AniListDate { Year = 2020 }
                },
            };

            var best = AniListService.SelectBestMatch(candidates, "Correct Title", 2020);
            Assert.Equal(2, best.Media.Id);
            Assert.Equal(0, best.Score);
        }
    }
}
