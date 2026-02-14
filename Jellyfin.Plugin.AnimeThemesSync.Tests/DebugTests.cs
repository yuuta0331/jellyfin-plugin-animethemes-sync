using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AnimeThemesSync.Services;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests
{
    public class DebugTests
    {
        private readonly ITestOutputHelper _output;

        public DebugTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Test_GetAnimeByExternalId_Live()
        {
            // Setup real dependencies
            var httpClientFactory = new SimpleHttpClientFactory();
            var logger = new TestLogger<AnimeThemesService>(_output);
            var rateLimiterLogger = new TestLogger<RateLimiter>(_output);
            var rateLimiter = new RateLimiter(rateLimiterLogger, "Test", 100);
            var service = new AnimeThemesService(httpClientFactory, logger, rateLimiter);

            // Test Case 1: AniList 12079 (Problematic from logs) -> "Black Rock Shooter" ?
            _output.WriteLine("Testing AniList ID: 12079");
            var result1 = await service.GetAnimeByExternalId("anilist", 12079, CancellationToken.None);
            if (result1 == null)
            {
                _output.WriteLine("result1 is NULL (Not Found)");
            }
            else
            {
                _output.WriteLine($"Found: {result1.Name}, Themes: {result1.AnimeThemes?.Count ?? 0}");
            }

            // Test Case 2: MAL 12079 (Problematic from logs) -> "Magi: The Kingdom of Magic" ?
            _output.WriteLine("Testing MAL ID: 12079");
            var result2 = await service.GetAnimeByExternalId("myanimelist", 12079, CancellationToken.None);
            if (result2 == null)
            {
                _output.WriteLine("result2 is NULL (Not Found)");
            }
            else
            {
                _output.WriteLine($"Found: {result2.Name}, Themes: {result2.AnimeThemes?.Count ?? 0}");
            }

            // Test Case 3: AniList 97766 (Problematic from logs) -> "Gamers!"
            _output.WriteLine("Testing AniList ID: 97766");
            var result3 = await service.GetAnimeByExternalId("anilist", 97766, CancellationToken.None);
            if (result3 == null)
            {
                _output.WriteLine("result3 is NULL (Not Found)");
            }
            else
            {
                _output.WriteLine($"Found: {result3.Name}, Themes: {result3.AnimeThemes?.Count ?? 0}");
            }

            // Test Case 3b: MAL 34280 ("Gamers!")
            _output.WriteLine("Testing MAL ID: 34280");
            var result3b = await service.GetAnimeByExternalId("myanimelist", 34280, CancellationToken.None);
            if (result3b == null)
            {
                _output.WriteLine("result3b is NULL (Not Found)");
            }
            else
            {
                _output.WriteLine($"Found: {result3b.Name}, Themes: {result3b.AnimeThemes?.Count ?? 0}");
            }

            // Test Case 4: AniList 150672 (Problematic from logs) -> "Oshi no Ko"
            _output.WriteLine("Testing AniList ID: 150672");
            var result4 = await service.GetAnimeByExternalId("anilist", 150672, CancellationToken.None);
            if (result4 == null)
            {
                _output.WriteLine("result4 is NULL (Not Found)");
            }
            else
            {
                _output.WriteLine($"Found: {result4.Name}, Themes: {result4.AnimeThemes?.Count ?? 0}");
            }
            // Test Case 4b: MAL 52034 ("Oshi no Ko")
            _output.WriteLine("Testing MAL ID: 52034");
            var result4b = await service.GetAnimeByExternalId("myanimelist", 52034, CancellationToken.None);
            if (result4b == null)
            {
                _output.WriteLine("result4b is NULL (Not Found)");
            }
            else
            {
                _output.WriteLine($"Found: {result4b.Name}, Themes: {result4b.AnimeThemes?.Count ?? 0}");
            }
        }

        [Fact]
        public async Task Test_GetAnimeByExternalId_WithIncludes_Manual()
        {
            var client = new SimpleHttpClientFactory().CreateClient("AnimeThemes");
            // Try Oshi no Ko (AniList 150672) with expanded includes
            // include=anime.animethemes.animethemeentries.videos
            var url = "https://api.animethemes.moe/resource?filter[site]=anilist&filter[external_id]=150672&include=anime.animethemes.animethemeentries.videos";

            _output.WriteLine($"Testing URL: {url}");
            var response = await client.GetAsync(url);
            _output.WriteLine($"Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _output.WriteLine($"Content Length: {content.Length}");
                if (content.Contains("\"animethemes\":[{"))
                {
                    _output.WriteLine("SUCCESS: Found animethemes in JSON");
                }
                else
                {
                    _output.WriteLine("FAILURE: Did not find animethemes in JSON");
                }
            }
        }

        [Fact]
        public async Task Test_GetAnime_ViaFilter_WithIncludes()
        {
            var client = new SimpleHttpClientFactory().CreateClient("AnimeThemes");
            // Try Oshi no Ko (AniList 150672) using /anime endpoint
            // GET /anime?filter[has]=resources&filter[site]=anilist&filter[external_id]=150672&include=animethemes.animethemeentries.videos
            var url = "https://api.animethemes.moe/anime?filter[has]=resources&filter[site]=anilist&filter[external_id]=150672&include=animethemes.animethemeentries.videos";

            _output.WriteLine($"Testing URL: {url}");
            var response = await client.GetAsync(url);
            _output.WriteLine($"Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _output.WriteLine($"Content Length: {content.Length}");
                if (content.Contains("\"animethemes\":[{"))
                {
                    _output.WriteLine("SUCCESS: Found animethemes in JSON");
                }
                else
                {
                    _output.WriteLine("FAILURE: Did not find animethemes in JSON");
                }
                _output.WriteLine(content.Substring(0, Math.Min(content.Length, 1000)));
            }
        }

        [Fact]
        public async Task Test_GetResource_With_AnimeThemes_Include()
        {
            var client = new SimpleHttpClientFactory().CreateClient("AnimeThemes");
            // Try Oshi no Ko (AniList 150672) - shallower include
            var url = "https://api.animethemes.moe/resource?filter[site]=anilist&filter[external_id]=150672&include=anime.animethemes";

            _output.WriteLine($"Testing URL: {url}");
            var response = await client.GetAsync(url);
            _output.WriteLine($"Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _output.WriteLine($"Content Length: {content.Length}");
                if (content.Contains("\"animethemes\":[{"))
                {
                    _output.WriteLine("SUCCESS: Found animethemes in JSON");
                }
                else
                {
                    _output.WriteLine("FAILURE: Did not find animethemes in JSON");
                }
            }
        }

        [Fact]
        public async Task Test_GetAnimeBySlug_Includes_Debug()
        {
            var client = new SimpleHttpClientFactory().CreateClient("AnimeThemes");
            var slug = "gamers";

            // 1. Full includes (Failing)
            var url1 = $"https://api.animethemes.moe/anime/{slug}?include=images,resources,animethemes.animethemeentries.videos";
            _output.WriteLine($"Testing Full Includes: {url1}");
            var response1 = await client.GetAsync(url1);
            _output.WriteLine($"Status: {response1.StatusCode}");

            // 2. Minimal includes (Themes only)
            var url2 = $"https://api.animethemes.moe/anime/{slug}?include=animethemes.animethemeentries.videos";
            _output.WriteLine($"Testing Minimal Includes: {url2}");
            var response2 = await client.GetAsync(url2);
            _output.WriteLine($"Status: {response2.StatusCode}");

            // 3. No Videos
            var url3 = $"https://api.animethemes.moe/anime/{slug}?include=animethemes.animethemeentries";
            _output.WriteLine($"Testing No Videos: {url3}");
            var response3 = await client.GetAsync(url3);
            _output.WriteLine($"Status: {response3.StatusCode}");
        }

        private class SimpleHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name)
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                return client;
            }
        }

        private class TestLogger<T> : ILogger<T>
        {
            private readonly ITestOutputHelper _output;

            public TestLogger(ITestOutputHelper output)
            {
                _output = output;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                _output.WriteLine($"[{logLevel}] {message}");
                if (exception != null)
                {
                    _output.WriteLine(exception.ToString());
                }
            }
        }
    }
}
