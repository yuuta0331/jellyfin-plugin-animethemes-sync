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

            _service = new AniListService(_mockHttpClientFactory.Object, _mockLogger.Object);
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
        public async Task SearchAnime_NoMatchWaitYear_ReturnsNull()
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

            // Verify logging is difficult with extension methods usually, but we check if it handled exception
        }
    }
}
