using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AnimeThemesSync.Services;
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
    }
}
