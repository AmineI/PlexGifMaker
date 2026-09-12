using PlexGifMaker.Data;

namespace PlexGifMaker.Tests.PlexGifMaker.UnitTests
{
    public class FfmpegCommandBuilderTests : IDisposable
    {
        private readonly DirectoryInfo subtitleDirectory = Directory.CreateTempSubdirectory("plexgifmaker-tests-");

        [Theory]
        [InlineData("mp4", "-map 0:a? -c:a copy -c:v libx264 -pix_fmt yuv420p")]
        [InlineData("png", "-frames:v 1 -c:v png")]
        public void BuildFfmpegCommand_UsesMp4AndPngEncodingOptions(
            string format, string expectedEncoding)
        {
            var command = FfmpegCommandBuilder.BuildFfmpegCommand(
                "http://plex.test/video.mkv",
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(5),
                $"wwwroot/gifs/clip.{format}",
                format,
                (null, "0:v:0"));

            Assert.Equal(
                format == "png"
                    ? $"-report -v debug -ss 00:00:10 -i \"http://plex.test/video.mkv\" -map 0:v:0 {expectedEncoding} \"wwwroot/gifs/clip.{format}\""
                    : $"-report -v debug -i \"http://plex.test/video.mkv\" -ss 00:00:10 -t 00:00:05 -map 0:v:0 {expectedEncoding} \"wwwroot/gifs/clip.{format}\"",
                command);
        }

        [Fact]
        public void BuildFfmpegCommand_UsesPalettePipelineForGifOutput()
        {
            var command = FfmpegCommandBuilder.BuildFfmpegCommand(
                "http://plex.test/video.mkv",
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(5),
                "wwwroot/gifs/clip.gif",
                "gif",
                (null, "0:v:0"));

            Assert.Contains("fps=20,scale=400:-1:flags=lanczos,split=2[gif_video][gif_palette]", command);
            Assert.Contains("palettegen=max_colors=256:stats_mode=full[p]", command);
            Assert.Contains("[gif_video][p]paletteuse=dither=floyd_steinberg:diff_mode=rectangle[out]", command);
            Assert.Contains("-map \"[out]\"", command);
            Assert.Contains("-i \"http://plex.test/video.mkv\" -ss 00:00:10 -t 00:00:05", command);
            Assert.DoesNotContain("-map 0:a", command);
        }

        [Fact]
        public void BuildFfmpegCommand_UsesComplexFilterForGifOutputWithSubtitles()
        {
            var subtitleFile = CreateSubtitleFile("srt");
            var subtitle = new Subtitle { Codec = "srt", Key = "/library/streams/2" };
            var escapedPath = subtitleFile.Replace("\\", "\\\\");
            var filter = FfmpegCommandBuilder.BuildVideoFilter(subtitle, "gif", subtitleDirectory.FullName, TimeSpan.FromSeconds(10));

            var command = FfmpegCommandBuilder.BuildFfmpegCommand(
                "http://plex.test/video.mkv",
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(5),
                "wwwroot/gifs/clip.gif",
                "gif",
                filter);

            Assert.Contains("-filter_complex", command);
            Assert.Contains("subtitles='" + escapedPath + "':force_style='Fontsize=24'", command);
            Assert.DoesNotContain("[v],split", command);
            Assert.Contains("split=2[gif_video][gif_palette]", command);
            Assert.Contains("[gif_video][p]paletteuse=dither=floyd_steinberg:diff_mode=rectangle[out]", command);
            Assert.Contains("-map \"[out]\"", command);
        }

        [Theory]
        [InlineData("mp4")]
        [InlineData("gif")]
        [InlineData("png")]
        public void BuildFfmpegCommand_PreservesSuppliedFilterAndOutputMap(string format)
        {
            const string filterArguments = "-lavfi \"[0:v]hflip[v]\"";
            var command = FfmpegCommandBuilder.BuildFfmpegCommand(
                "http://plex.test/video.mkv",
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                $"clip.{format}",
                format,
                (filterArguments, "[v]"));

            if (format == "gif")
            {
                Assert.Contains("[0:v]hflip", command);
                Assert.Contains("split=2[gif_video][gif_palette]", command);
                Assert.Contains("[gif_video][p]paletteuse=dither=floyd_steinberg:diff_mode=rectangle[out]", command);
            }
            else
            {
                Assert.Contains(filterArguments, command);
                Assert.Contains("-map [v]", command);
                Assert.DoesNotContain("-map 0:v", command);
            }
        }

        [Fact]
        public void BuildFfmpegCommand_QuotesPathsAndPreservesMillisecondTiming()
        {
            var command = FfmpegCommandBuilder.BuildFfmpegCommand(
                "media/source video.mkv",
                TimeSpan.FromMilliseconds(10250),
                TimeSpan.FromMilliseconds(1500),
                "output/my clip.mp4",
                "mp4",
                (null, "0:v:0"));

            Assert.Equal(
                "-report -v debug -i \"media/source video.mkv\" -ss 00:00:10.2500000 -t 00:00:01.5000000 -map 0:v:0 -map 0:a? -c:a copy -c:v libx264 -pix_fmt yuv420p \"output/my clip.mp4\"",
                command);
        }

        [Theory]
        [InlineData("mp4", null)]
        [InlineData("gif", "-vf \"fps=20,scale=400:-1:flags=lanczos\"")]
        [InlineData("png", null)]
        public void BuildVideoFilter_WithoutSubtitles_MapsSourceVideoWithoutRequiringSubtitleDirectory(
            string format, string? expectedArguments)
        {
            var missingDirectory = Path.Combine(subtitleDirectory.FullName, "does-not-exist");

            var filter = FfmpegCommandBuilder.BuildVideoFilter(null, format, missingDirectory, TimeSpan.FromSeconds(10));

            Assert.Equal((expectedArguments, "0:v:0"), filter);
            Assert.False(Directory.Exists(missingDirectory));
        }

        [Theory]
        [InlineData("mp4", "-lavfi \"subtitles='{subtitleFile}'[v]\"")]
        [InlineData("gif", "-lavfi \"subtitles='{subtitleFile}':force_style='Fontsize=24',fps=20,scale=400:-1:flags=lanczos[v]\"")]
        [InlineData("png", "-lavfi \"setpts=PTS+10/TB,subtitles='{subtitleFile}'[v]\"")]
        public void BuildVideoFilter_TextSubtitles_BurnsInFileBeforeFormatSpecificScaling(
            string format, string expectedArguments)
        {
            var subtitleFile = CreateSubtitleFile("srt");
            var subtitle = new Subtitle { Codec = "srt", Key = "/library/streams/2" };
            var escapedPath = subtitleFile.Replace("\\", "\\\\");

            var filter = FfmpegCommandBuilder.BuildVideoFilter(subtitle, format, subtitleDirectory.FullName, TimeSpan.FromSeconds(10));

            Assert.Equal(expectedArguments.Replace("{subtitleFile}", escapedPath), filter.Arguments);
            Assert.Equal("[v]", filter.OutputMap);
        }

        [Fact]
        public void BuildVideoFilter_PngTextSubtitlesWithFractionalStartTime_FormatsSetptsWithInvariantDecimal()
        {
            var subtitleFile = CreateSubtitleFile("srt");
            var subtitle = new Subtitle { Codec = "srt", Key = "/library/streams/2" };
            var escapedPath = subtitleFile.Replace("\\", "\\\\");
            var startTime = TimeSpan.FromMilliseconds(10100); // 10.1 seconds (+100ms offset)

            var filter = FfmpegCommandBuilder.BuildVideoFilter(subtitle, "png", subtitleDirectory.FullName, startTime);

            Assert.Equal($"-lavfi \"setpts=PTS+10.1/TB,subtitles='{escapedPath}'[v]\"", filter.Arguments);
            Assert.Equal("[v]", filter.OutputMap);
        }

        [Theory]
        [InlineData("mp4", "-lavfi \"[0:v][0:s:2]overlay[v]\"")]
        [InlineData("gif", "-lavfi \"[0:v][0:s:2]overlay,fps=20,scale=400:-1:flags=lanczos[v]\"")]
        [InlineData("png", "-lavfi \"[0:v][0:s:2]overlay[v]\"")]
        public void BuildVideoFilter_ImageSubtitles_OverlaysSelectedStreamBeforeFormatSpecificScaling(
            string format, string expectedArguments)
        {
            CreateSubtitleFile("sup");
            var subtitle = new Subtitle { Codec = "sup", Key = "2" };

            var filter = FfmpegCommandBuilder.BuildVideoFilter(subtitle, format, subtitleDirectory.FullName, TimeSpan.FromSeconds(10));

            Assert.Equal((expectedArguments, "[v]"), filter);
        }

        [Theory]
        [InlineData("srt", "mp4")]
        [InlineData("srt", "gif")]
        [InlineData("srt", "png")]
        [InlineData("sup", "mp4")]
        [InlineData("sup", "gif")]
        [InlineData("sup", "png")]
        public void BuildVideoFilter_MissingSelectedSubtitleFile_ThrowsWithMissingPath(string codec, string format)
        {
            var subtitle = new Subtitle { Codec = codec, Key = "0" };

            var exception = Assert.Throws<FileNotFoundException>(() =>
                FfmpegCommandBuilder.BuildVideoFilter(subtitle, format, subtitleDirectory.FullName, TimeSpan.FromSeconds(10)));

            Assert.Equal(Path.Combine(subtitleDirectory.FullName, $"subtitle.{codec}"), exception.FileName);
        }

        private string CreateSubtitleFile(string codec)
        {
            var path = Path.Combine(subtitleDirectory.FullName, $"subtitle.{codec}");
            File.WriteAllText(path, "1\n00:00:00,000 --> 00:00:01,000\nExample subtitle\n");
            return path;
        }

        public void Dispose()
        {
            subtitleDirectory.Delete(true);
        }
    }
}