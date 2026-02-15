using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

using Jellyfin.Plugin.AnimeThemesSync.Services;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests
{
    public class AnimeThemesMetadataProviderTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILoggerFactory> _mockLoggerFactory;
        private readonly Mock<ILogger<AnimeThemesMetadataProvider>> _mockLogger;
        private readonly MockHttpMessageHandler _mockHttp;
        private readonly AnimeThemesMetadataProvider _provider;

        public AnimeThemesMetadataProviderTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockLogger = new Mock<ILogger<AnimeThemesMetadataProvider>>();
            _mockHttp = new MockHttpMessageHandler();

            _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(AnimeThemesMetadataProvider).FullName!)).Returns(_mockLogger.Object);
            // Also need to handle other loggers created in ctor
            _mockLoggerFactory.Setup(x => x.CreateLogger(It.Is<string>(s => s.Contains("AniListService")))).Returns(new Mock<ILogger>().Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(It.Is<string>(s => s.Contains("AnimeThemesService")))).Returns(new Mock<ILogger>().Object);

            var client = _mockHttp.ToHttpClient();
            client.BaseAddress = new Uri("https://api.animethemes.moe");
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

            var rateLimiter = new RateLimiter(new Mock<ILogger<RateLimiter>>().Object, "Test", 100);
            var aniListService = new AniListService(_mockHttpClientFactory.Object, new Mock<ILogger<AniListService>>().Object, rateLimiter);
            var animeThemesService = new AnimeThemesService(_mockHttpClientFactory.Object, new Mock<ILogger<AnimeThemesService>>().Object, rateLimiter);

            _provider = new AnimeThemesMetadataProvider(_mockHttpClientFactory.Object, _mockLoggerFactory.Object, aniListService, animeThemesService);
        }

        [Fact]
        public async Task GetMetadata_Found_ReturnsBothIds()
        {
            // Arrange
            var aniListId = 12345;
            var seriesInfo = new SeriesInfo
            {
                Name = "Test Anime",
                Year = 2021
            };
            seriesInfo.SetProviderId("AniList", aniListId.ToString());

            var animeThemesId = 999;
            var animeThemesSlug = "test_anime_slug";
            var jsonResponse = $@"{{
                ""resources"": [
                    {{
                        ""site"": ""anilist"",
                        ""external_id"": {aniListId},
                        ""anime"": [
                            {{
                                ""id"": {animeThemesId},
                                ""name"": ""Test Anime"",
                                ""slug"": ""{animeThemesSlug}"",
                                ""year"": 2021,
                                ""season"": ""Fall""
                            }}
                        ]
                    }}
                ]
            }}";

            _mockHttp.When("https://api.animethemes.moe/resource")
                .WithQueryString("filter[site]", "anilist")
                .WithQueryString("filter[external_id]", aniListId.ToString())
                .WithQueryString("include", "anime")
                .Respond("application/json", jsonResponse);

            var animeResponse = $@"{{
                ""anime"": [
                    {{
                        ""id"": {animeThemesId},
                        ""name"": ""Test Anime"",
                        ""slug"": ""{animeThemesSlug}"",
                        ""year"": 2021,
                        ""season"": ""Fall""
                    }}
                ]
            }}";

            _mockHttp.When($"https://api.animethemes.moe/anime/{animeThemesSlug}*")
                .Respond("application/json", animeResponse);

            // Act
            var result = await _provider.GetMetadata(seriesInfo, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasMetadata);
            Assert.Equal(animeThemesSlug, result.Item.GetProviderId("AnimeThemes"));
            Assert.Equal(animeThemesId.ToString(), result.Item.GetProviderId("AnimeThemesId"));
        }
    }
}
