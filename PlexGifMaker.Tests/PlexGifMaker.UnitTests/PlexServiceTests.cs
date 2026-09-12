using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PlexGifMaker.Data;
using PlexGifMaker.Pages.Components;
using System.Net;

namespace PlexGifMaker.Tests.PlexGifMaker.UnitTests
{
    public class PlexServiceTests
    {
        [Theory]
        [InlineData("ass", false)]
        [InlineData("ass", true)]
        [InlineData("ASS", true)]
        [InlineData("ssa", true)]
        public void ParseSubtitles_IgnoresAegisubExtradata(string codec, bool hasExtradata)
        {
            var content = """
                [Script Info]
                ScriptType: v4.00+

                [Events]
                Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
                Dialogue: 31,0:02:53.05,0:02:54.94,Default,,0,0,0,,Test subtitle
                """;
            if (hasExtradata)
            {
                content += "\n\n[Aegisub Extradata]\nData: 0,test,test";
            }
            var items = PlexService.ParseSubtitles(content, codec);

            var item = Assert.Single(items);
            Assert.Equal(173050, item.StartTime);
            Assert.Equal(174940, item.EndTime);
            Assert.Contains("Test subtitle", item.Lines);
        }

        [Fact]
        public void ParseSubtitles_IgnoresAssCommentsAndPreservesFormatting()
        {
            var content = """
                [Script Info]
                ScriptType: v4.00+
                WrapStyle: 0

                [Events]
                Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
                Dialogue: 31,0:00:01.00,0:00:02.00,Default,,0,0,0,,{\i1}First, line\NSecond line

                [Aegisub Extradata]
                Data: 0,test,test
                """;

            var item = Assert.Single(PlexService.ParseSubtitles(content, "ass"));

            Assert.Equal(new[] { "{\\i1}First, line", "Second line" }, item.Lines);
            Assert.Equal(new[] { "First, line", "Second line" }, item.PlaintextLines);
        }

        [Fact]
        public void ParseSubtitles_PreservesSrtParsing()
        {
            var content = "1\n00:00:01,000 --> 00:00:02,000\nTest subtitle\n\n";

            var item = Assert.Single(PlexService.ParseSubtitles(content, "srt"));

            Assert.Equal(1000, item.StartTime);
            Assert.Equal(2000, item.EndTime);
            Assert.Contains("Test subtitle", item.Lines);
        }

        [Fact]
        public async Task GetEpisodesAsync_ReturnsCorrectEpisodesCount()
        {
            // Arrange
            var expectedUri = new Uri($"http://plex.test/library/metadata/0/allLeaves?X-Plex-Token=");

            var handlerMock = PlexServiceTestsHelpers.SetupMockHttpMessageHandler(PlexServiceTestsHelpers.Content, HttpStatusCode.OK);
            var httpClientFactoryMock = PlexServiceTestsHelpers.SetupMockHttpClientFactory(handlerMock);
            var loggerMock = new Mock<ILogger<PlexService>>();
            var service = new PlexService(httpClientFactoryMock.Object, loggerMock.Object);

            // Act
            var episodes = await service.GetEpisodesAsync("0", false);
            Assert.Equal(5, episodes.Count);
            PlexServiceTestsHelpers.VerifyMockHttpMessageHandler(handlerMock, expectedUri.ToString());
        }

        [Fact]
        public async Task GetLibraries_ReturnsCorrectLibraries()
        {
            // Arrange
            var baseUri = "http://plex.test";
            var token = "3TcQZEzVWANSs1gs_sXs";
            var expectedUri = $"{baseUri}/library/sections?X-Plex-Token={token}";

            var handlerMock = PlexServiceTestsHelpers.SetupMockHttpMessageHandler(PlexServiceTestsHelpers.Content1, HttpStatusCode.OK);
            var httpClientFactoryMock = PlexServiceTestsHelpers.SetupMockHttpClientFactory(handlerMock);
            var loggerMock = new Mock<ILogger<PlexService>>();
            var service = new PlexService(httpClientFactoryMock.Object, loggerMock.Object);

            // Set the configuration
            service.SetConfiguration(baseUri, token);

            // Act
            var libraries = await service.GetLibrariesAsync();

            // Assert
            Assert.NotNull(libraries);
            Assert.Equal(2, libraries.Count);
            Assert.Equal("Movies", libraries[0].Title);
            Assert.Equal("TV Shows", libraries[1].Title);

            // Verify the correct URL was called
            PlexServiceTestsHelpers.VerifyMockHttpMessageHandler(handlerMock, expectedUri);
        }
        [Fact]
        public async Task GetShowsAsync_ReturnsCorrectShowsCount()
        {
            // Arrange
            var expectedUri = new Uri($"http://plex.test/library/sections/1/all?X-Plex-Token=");
            var handlerMock = PlexServiceTestsHelpers.SetupMockHttpMessageHandler(PlexServiceTestsHelpers.Content, HttpStatusCode.OK);
            var httpClientFactoryMock = PlexServiceTestsHelpers.SetupMockHttpClientFactory(handlerMock);
            var loggerMock = new Mock<ILogger<PlexService>>();
            var service = new PlexService(httpClientFactoryMock.Object, loggerMock.Object);

            // Act
            var shows = await service.GetShowsAsync("1");

            // Assert
            Assert.Equal(5, shows.Count);
            PlexServiceTestsHelpers.VerifyMockHttpMessageHandler(handlerMock, expectedUri.ToString());
        }

        [Fact]
        public async Task GetSubtitleOptionsAsync_ReturnsCorrectSubtitles()
        {
            // Arrange
            var plexToken = "3TcQZEzVWANSs1gs_sXs";
            var expectedUri = new Uri($"http://plex.test/library/metadata/8405?X-Plex-Token={plexToken}");
            var handlerMock = PlexServiceTestsHelpers.SetupMockHttpMessageHandler(PlexServiceTestsHelpers.Content2, HttpStatusCode.OK);
            var httpClientFactoryMock = PlexServiceTestsHelpers.SetupMockHttpClientFactory(handlerMock);
            var loggerMock = new Mock<ILogger<PlexService>>();
            var service = new PlexService(httpClientFactoryMock.Object, loggerMock.Object);

            // Set the configuration with the test token
            service.SetConfiguration("http://plex.test", plexToken);

            // Act
            var subtitles = await service.GetSubtitleOptionsAsync("8405");

            // Assert
            Assert.NotNull(subtitles);
            Assert.True(subtitles.Count > 0);
            PlexServiceTestsHelpers.VerifyMockHttpMessageHandler(handlerMock, expectedUri.ToString());
        }

        [Theory]
        [InlineData("Full Subtitles", "English (ASS) - Full Subtitles")]
        [InlineData("Signs/Songs", "English (ASS) - Signs/Songs")]
        [InlineData(null, "English (ASS)")]
        [InlineData("", "English (ASS)")]
        [InlineData("   ", "English (ASS)")]
        public async Task SubtitleSelector_DisplaysTrackTitle(string? title, string expectedLabel)
        {
            var stream = new System.Xml.Linq.XElement("Stream",
                new System.Xml.Linq.XAttribute("streamType", "3"),
                new System.Xml.Linq.XAttribute("language", "English"),
                new System.Xml.Linq.XAttribute("codec", "ass"),
                new System.Xml.Linq.XAttribute("displayTitle", "English (ASS)"));
            if (title != null)
            {
                stream.SetAttributeValue("title", title);
            }

            var handlerMock = PlexServiceTestsHelpers.SetupMockHttpMessageHandler(stream.ToString(), HttpStatusCode.OK);
            var factoryMock = PlexServiceTestsHelpers.SetupMockHttpClientFactory(handlerMock);
            var service = new PlexService(factoryMock.Object, new Mock<ILogger<PlexService>>().Object);
            var subtitles = await service.GetSubtitleOptionsAsync("8405");

            Assert.Equal(title, Assert.Single(subtitles)!.Title);

            using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var component = await renderer.RenderComponentAsync<SubtitleSelector>(ParameterView.FromDictionary(
                    new Dictionary<string, object?> { [nameof(SubtitleSelector.SubtitleOptions)] = subtitles }));
                return component.ToHtmlString();
            });

            Assert.Contains($"<option value=\"0\">{WebUtility.HtmlEncode(expectedLabel)}</option>", html);
        }

        [Fact]
        public async Task GetCurrentlyPlayingMediaAsync_ReturnsCurrentlyPlayingMedia()
        {
            // Arrange
            var baseUri = "http://plex.test";
            var token = "3TcQZEzVWANSs1gs_sXs";
            var expectedUri = $"{baseUri}/status/sessions?X-Plex-Token={token}";

            var handlerMock = PlexServiceTestsHelpers.SetupMockHttpMessageHandler(PlexServiceTestsHelpers.SessionsContent, HttpStatusCode.OK);
            var httpClientFactoryMock = PlexServiceTestsHelpers.SetupMockHttpClientFactory(handlerMock);
            var loggerMock = new Mock<ILogger<PlexService>>();
            var service = new PlexService(httpClientFactoryMock.Object, loggerMock.Object);

            // Set the configuration
            service.SetConfiguration(baseUri, token);

            // Act
            var currentlyPlaying = await service.GetCurrentlyPlayingMediaAsync();

            // Assert
            Assert.NotNull(currentlyPlaying);
            Assert.Equal("8407", currentlyPlaying.EpisodeId);
            Assert.Equal("Part 1", currentlyPlaying.EpisodeTitle);
            Assert.Equal("episode", currentlyPlaying.MediaType);
            Assert.Equal("2", currentlyPlaying.LibraryId);
            Assert.Equal("8405", currentlyPlaying.ShowId);
            Assert.Equal("The 10th Kingdom", currentlyPlaying.ShowTitle);
            Assert.Equal(123456, currentlyPlaying.ViewOffset);
            Assert.Equal(TimeSpan.FromMilliseconds(123456), currentlyPlaying.CurrentTime);
            Assert.NotNull(currentlyPlaying.CurrentSubtitle);
            Assert.Equal("36868", currentlyPlaying.CurrentSubtitle.Id);
            Assert.Equal("srt", currentlyPlaying.CurrentSubtitle.Codec);
            Assert.Equal("English", currentlyPlaying.CurrentSubtitle.Language);
            Assert.Equal("English (SRT)", currentlyPlaying.CurrentSubtitle.DisplayTitle);
            Assert.Equal("/library/streams/36868", currentlyPlaying.CurrentSubtitle.Key);

            // Verify the correct URL was called
            PlexServiceTestsHelpers.VerifyMockHttpMessageHandler(handlerMock, expectedUri);
        }

        [Fact]
        public async Task GetCurrentlyPlayingMediaAsync_ReturnsNullWhenNoMediaPlaying()
        {
            // Arrange
            var baseUri = "http://plex.test";
            var token = "3TcQZEzVWANSs1gs_sXs";
            var expectedUri = $"{baseUri}/status/sessions?X-Plex-Token={token}";

            var handlerMock = PlexServiceTestsHelpers.SetupMockHttpMessageHandler(PlexServiceTestsHelpers.EmptySessionsContent, HttpStatusCode.OK);
            var httpClientFactoryMock = PlexServiceTestsHelpers.SetupMockHttpClientFactory(handlerMock);
            var loggerMock = new Mock<ILogger<PlexService>>();
            var service = new PlexService(httpClientFactoryMock.Object, loggerMock.Object);

            // Set the configuration
            service.SetConfiguration(baseUri, token);

            // Act
            var currentlyPlaying = await service.GetCurrentlyPlayingMediaAsync();

            // Assert
            Assert.Null(currentlyPlaying);

            // Verify the correct URL was called
            PlexServiceTestsHelpers.VerifyMockHttpMessageHandler(handlerMock, expectedUri);
        }
    }
}