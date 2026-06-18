using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AnimeThemesSync.Shared;
using AnimeThemesSync.Shared.Services;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests
{
    public class AnimeThemesServiceTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<AnimeThemesService>> _mockLogger;
        private readonly MockHttpMessageHandler _mockHttp;
        private readonly AnimeThemesService _service;

        public AnimeThemesServiceTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<AnimeThemesService>>();
            _mockHttp = new MockHttpMessageHandler();

            var client = _mockHttp.ToHttpClient();
            client.BaseAddress = new Uri("https://api.animethemes.moe");
            _mockHttpClientFactory.Setup(x => x.CreateClient("AnimeThemes")).Returns(client);

            var rateLimiterLogger = new Mock<ILogger<RateLimiter>>();
            var rateLimiter = new RateLimiter(rateLimiterLogger.Object, "TestService", 100);

            _service = new AnimeThemesService(_mockHttpClientFactory.Object, _mockLogger.Object, rateLimiter);
        }

        [Fact]
        public async Task GetAnimeByExternalId_Found_ReturnsAnime()
        {
            // Arrange
            var externalId = 30;
            var jsonResponse = @"{
                ""resources"": [
                    {
                        ""site"": ""anilist"",
                        ""external_id"": 30,
                        ""anime"": [
                            {
                                ""id"": 123,
                                ""name"": ""Neon Genesis Evangelion"",
                                ""slug"": ""neon_genesis_evangelion"",
                                ""images"": [],
                                ""resources"": [],
                                ""animethemes"": [
                                    {
                                        ""type"": ""OP"",
                                        ""slug"": ""OP1"",
                                        ""animethemeentries"": [
                                            {
                                                ""version"": 1,
                                                ""videos"": [
                                                    {
                                                        ""basename"": ""OP1.webm"",
                                                        ""link"": ""https://animethemes.moe/video/OP1.webm"",
                                                        ""resolution"": 1080
                                                    }
                                                ]
                                            }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }";

            _mockHttp.When("https://api.animethemes.moe/resource*")
                .Respond("application/json", jsonResponse);

            var animeResponse = @"{
                ""anime"": [
                    {
                        ""id"": 123,
                        ""name"": ""Neon Genesis Evangelion"",
                        ""slug"": ""neon_genesis_evangelion"",
                        ""animethemes"": [
                            {
                                ""type"": ""OP"",
                                ""slug"": ""OP1"",
                                ""animethemeentries"": [
                                    {
                                        ""version"": 1,
                                        ""videos"": [
                                            {
                                                ""basename"": ""OP1.webm"",
                                                ""link"": ""https://animethemes.moe/video/OP1.webm"",
                                                ""resolution"": 1080
                                            }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }";

            _mockHttp.When("https://api.animethemes.moe/anime/neon_genesis_evangelion*")
                .Respond("application/json", animeResponse);

            // Act
            var result = await _service.GetAnimeByExternalId("anilist", externalId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Neon Genesis Evangelion", result.Name);
            Assert.NotNull(result.AnimeThemes);
            Assert.Single(result.AnimeThemes);
            Assert.Equal("OP", result.AnimeThemes[0].Type);
        }

        [Fact]
        public async Task GetAnimeByExternalId_NotFound_ReturnsNull()
        {
            // Arrange
            var externalId = 99999;
            var jsonResponse = @"{ ""resources"": [] }";

            _mockHttp.When("https://api.animethemes.moe/resource*")
                .Respond("application/json", jsonResponse);

            // Act
            var result = await _service.GetAnimeByExternalId("anilist", externalId, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SearchAnimeByTitle_ReturnsAnimeCandidates()
        {
            // Arrange
            var jsonResponse = @"{
                ""anime"": [
                    {
                        ""id"": 456,
                        ""name"": ""K-On!!"",
                        ""slug"": ""k_on_2010"",
                        ""year"": 2010,
                        ""season"": ""Spring"",
                        ""media_format"": ""TV"",
                        ""synonyms"": [
                            { ""id"": 1, ""text"": ""けいおん!!"", ""type"": ""Native"" }
                        ],
                        ""images"": [
                            { ""id"": 2, ""facet"": ""Small Cover"", ""link"": ""https://example.test/k-on.avif"" }
                        ],
                        ""resources"": [
                            { ""site"": ""AniList"", ""external_id"": 7791 },
                            { ""site"": ""MyAnimeList"", ""external_id"": 7791 }
                        ]
                    }
                ],
                ""links"": { ""next"": null },
                ""meta"": { ""current_page"": 1, ""per_page"": 15, ""total"": 1 }
            }";

            _mockHttp.When("https://api.animethemes.moe/anime*")
                .Respond("application/json", jsonResponse);

            // Act
            var result = await _service.SearchAnimeByTitle("K-On", 2010, CancellationToken.None);

            // Assert
            var anime = Assert.Single(result);
            Assert.Equal(456, anime.Id);
            Assert.Equal("K-On!!", anime.Name);
            Assert.Equal("k_on_2010", anime.Slug);
            Assert.Equal(2010, anime.Year);
            Assert.Equal("Spring", anime.Season);
            Assert.Equal("TV", anime.MediaFormat);
            Assert.NotNull(anime.Resources);
            Assert.Equal(2, anime.Resources!.Count);
            Assert.NotNull(anime.Images);
            Assert.Single(anime.Images);
            Assert.NotNull(anime.Synonyms);
            var synonym = Assert.Single(anime.Synonyms);
            Assert.Equal("けいおん!!", synonym.Text);
            Assert.Equal("Native", synonym.Type);
            _mockHttp.VerifyNoOutstandingExpectation();
        }
    }
}

