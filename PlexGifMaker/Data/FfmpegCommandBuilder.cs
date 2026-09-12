using System.Globalization;

namespace PlexGifMaker.Data
{
    internal static class FfmpegCommandBuilder
    {
        private static readonly HashSet<string> ImageBasedSubtitleFormats = new(StringComparer.OrdinalIgnoreCase)
        {
            "sup"
        };

        internal static (string? Arguments, string OutputMap) BuildVideoFilter(
            Subtitle? subtitle,
            string format,
            string subtitlePath,
            TimeSpan startTime)
        {
            var isGif = format == "gif";
            if (subtitle == null)
            {
                return (isGif ? "-vf \"fps=20,scale=400:-1:flags=lanczos\"" : null, "0:v:0");
            }

            var subtitleFile = Path.Combine(subtitlePath, $"subtitle.{subtitle.Codec}");
            if (!File.Exists(subtitleFile))
            {
                throw new FileNotFoundException("No supported subtitle file found.", subtitleFile);
            }

            string filterGraph;
            if (ImageBasedSubtitleFormats.Contains(subtitle.Codec ?? "srt"))
            {
                var streamIndex = int.TryParse(subtitle.Key, out var index) ? index : 0;
                filterGraph = $"[0:v][0:s:{streamIndex}]overlay";
            }
            else
            {
                var escapedSubtitleFile = subtitleFile.Replace("\\", "\\\\");
                var style = isGif ? ":force_style='Fontsize=24'" : string.Empty;
                var timestampOffset = format == "png"
                    ? $"setpts=PTS+{startTime.TotalSeconds.ToString("0.#######", CultureInfo.InvariantCulture)}/TB,"
                    : string.Empty;
                filterGraph = $"{timestampOffset}subtitles='{escapedSubtitleFile}'{style}";
            }

            if (isGif)
            {
                filterGraph += ",fps=20,scale=400:-1:flags=lanczos";
            }

            return ($"-lavfi \"{filterGraph}[v]\"", "[v]");
        }

        internal static string BuildFfmpegCommand(
            string videoFile,
            TimeSpan startTime,
            TimeSpan duration,
            string outputPath,
            string format,
            (string? Arguments, string OutputMap) filter)
        {
            if (format == "gif")
            {
                return BuildGifCommand(videoFile, startTime, duration, outputPath, filter);
            }

            var arguments = new List<string>
            {
                format == "png"
                    ? $"-report -v debug -ss {startTime} -i \"{videoFile}\""
                    : $"-report -v debug -i \"{videoFile}\"",
                format == "png" ? string.Empty : $"-ss {startTime} -t {duration}"
            };

            if (filter.Arguments != null)
            {
                arguments.Add(filter.Arguments);
            }

            arguments.Add($"-map {filter.OutputMap}");
            arguments.Add(format switch
            {
                "gif" => "-c:v gif",
                "png" => "-frames:v 1 -c:v png",
                _ => "-map 0:a? -c:a copy -c:v libx264 -pix_fmt yuv420p"
            });
            arguments.Add($"\"{outputPath}\"");

            return string.Join(' ', arguments.Where(argument => !string.IsNullOrWhiteSpace(argument)));
        }

        private static string BuildGifCommand(
            string videoFile,
            TimeSpan startTime,
            TimeSpan duration,
            string outputPath,
            (string? Arguments, string OutputMap) filter)
        {
            var videoFilter = filter.Arguments == null
                ? "fps=20,scale=400:-1:flags=lanczos"
                : ExtractFilterGraph(filter.Arguments);

            var filterGraph = $"{videoFilter},split=2[gif_video][gif_palette];[gif_palette]palettegen=max_colors=256:stats_mode=full[p];[gif_video][p]paletteuse=dither=floyd_steinberg:diff_mode=rectangle[out]";

            var arguments = new List<string>
            {
                "-report -v debug",
                $"-i \"{videoFile}\"",
                $"-ss {startTime} -t {duration}",
                $"-filter_complex \"{filterGraph}\"",
                "-map \"[out]\"",
                $"\"{outputPath}\""
            };

            return string.Join(' ', arguments);
        }

        private static string ExtractFilterGraph(string filterArguments)
        {
            const string lavfiPrefix = "-lavfi \"";
            if (filterArguments.StartsWith(lavfiPrefix, StringComparison.Ordinal) && filterArguments.EndsWith("[v]\"", StringComparison.Ordinal))
            {
                return filterArguments[lavfiPrefix.Length..^4];
            }

            const string videoFilterPrefix = "-vf \"";
            if (filterArguments.StartsWith(videoFilterPrefix, StringComparison.Ordinal) && filterArguments.EndsWith("\"", StringComparison.Ordinal))
            {
                return filterArguments[videoFilterPrefix.Length..^1];
            }

            return filterArguments;
        }
    }
}