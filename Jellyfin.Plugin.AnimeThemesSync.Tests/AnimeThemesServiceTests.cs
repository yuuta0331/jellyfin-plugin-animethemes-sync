using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using AnimeThemesSync.Shared;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;
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
                            { ""id"": 1, ""text"": ""けいおん!!"", ""synonymable_type"": ""Native"" }
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

        [Fact]
        public async Task GetAnimeBySlug_FreshPersistentCache_DoesNotRequestProvider()
        {
            var slug = "persistent-cache-only-" + Guid.NewGuid().ToString("N");
            var cache = new Mock<ISeasonFinderDataStore>();
            cache.Setup(i => i.GetApiFetchCache("animethemes:slug:" + slug)).Returns(new ApiFetchCacheEntry
            {
                CacheKey = "animethemes:slug:" + slug,
                Provider = "AnimeThemes",
                PayloadJson = "{\"id\":123,\"name\":\"Persistent\",\"slug\":\"" + slug + "\",\"year\":2024,\"season\":\"Spring\"}",
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
            });
            var limiter = new RateLimiter(new Mock<ILogger<RateLimiter>>().Object, "AnimeThemes", 80);
            var service = new AnimeThemesService(_mockHttpClientFactory.Object, _mockLogger.Object, limiter, cache.Object);

            var result = await service.GetAnimeBySlug(slug, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Persistent", result.Name);
            _mockHttpClientFactory.Verify(i => i.CreateClient("AnimeThemes"), Times.Never);
        }

        [Fact]
        public async Task GetAnimeBySlug_FreshPersistentHit_IsServedFromMemoryOnRepeat()
        {
            var slug = "persistent-then-memory-" + Guid.NewGuid().ToString("N");
            var cache = new Mock<ISeasonFinderDataStore>();
            cache.Setup(i => i.GetApiFetchCache("animethemes:slug:" + slug)).Returns(new ApiFetchCacheEntry
            {
                CacheKey = "animethemes:slug:" + slug,
                Provider = "AnimeThemes",
                PayloadJson = "{\"id\":777,\"name\":\"Persistent\",\"slug\":\"" + slug + "\"}",
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
            });
            var limiter = new RateLimiter(new Mock<ILogger<RateLimiter>>().Object, "AnimeThemes", 80);
            var service = new AnimeThemesService(_mockHttpClientFactory.Object, _mockLogger.Object, limiter, cache.Object);

            var first = await service.GetAnimeBySlug(slug, CancellationToken.None);
            var second = await service.GetAnimeBySlug(slug, CancellationToken.None);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal("Persistent", second.Name);
            cache.Verify(i => i.GetApiFetchCache("animethemes:slug:" + slug), Times.Once);
            _mockHttpClientFactory.Verify(i => i.CreateClient("AnimeThemes"), Times.Never);
        }

        [Fact]
        public async Task GetAnimeBySlug_RateLimited_RetriesTwiceThenUsesStaleCache()
        {
            var slug = "rate-limited-" + Guid.NewGuid().ToString("N");
            var cache = new Mock<ISeasonFinderDataStore>();
            cache.Setup(i => i.GetApiFetchCache("animethemes:slug:" + slug)).Returns(new ApiFetchCacheEntry
            {
                CacheKey = "animethemes:slug:" + slug,
                Provider = "AnimeThemes",
                PayloadJson = "{\"id\":321,\"name\":\"Stale\",\"slug\":\"" + slug + "\"}",
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-31).ToString("O"),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1).ToString("O"),
            });
            var requests = 0;
            _mockHttp.When("https://api.animethemes.moe/anime/" + slug + "*").Respond(_ =>
            {
                requests++;
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return response;
            });
            var limiter = new RateLimiter(new Mock<ILogger<RateLimiter>>().Object, "AnimeThemes", 80);
            var service = new AnimeThemesService(_mockHttpClientFactory.Object, _mockLogger.Object, limiter, cache.Object);

            var result = await service.GetAnimeBySlug(slug, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Stale", result.Name);
            Assert.Equal(3, requests);
        }
    }
}

