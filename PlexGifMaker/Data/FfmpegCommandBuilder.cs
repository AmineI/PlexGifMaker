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
            string subtitlePath)
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
                filterGraph = $"subtitles='{escapedSubtitleFile}'{style}";
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
            var arguments = new List<string>
            {
                $"-report -v debug -i \"{videoFile}\"",
                $"-ss {startTime} -t {duration}"
            };

            if (filter.Arguments != null)
            {
                arguments.Add(filter.Arguments);
            }

            arguments.Add($"-map {filter.OutputMap}");
            arguments.Add(format == "gif"
                ? "-c:v gif"
                : "-map 0:a? -c:a copy -c:v libx264 -pix_fmt yuv420p");
            arguments.Add($"\"{outputPath}\"");

            return string.Join(' ', arguments);
        }
    }
}