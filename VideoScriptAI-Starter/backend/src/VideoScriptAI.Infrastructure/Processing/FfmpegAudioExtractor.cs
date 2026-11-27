using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class FfmpegAudioExtractor(IProcessRunner processRunner, EngineOptions options) : IAudioExtractor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".mov", ".avi", ".webm",
        ".m4a", ".mp3", ".wav", ".aac", ".flac", ".ogg", ".oga", ".opus", ".wma"
    };

    public async Task<string> ExtractWavAsync(
        string videoPath,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (!File.Exists(videoPath))
            throw new FileNotFoundException("Video file not found.", videoPath);
        if (!SupportedExtensions.Contains(Path.GetExtension(videoPath)))
            throw new InvalidOperationException("Unsupported media extension: " + Path.GetExtension(videoPath));
        if (string.IsNullOrWhiteSpace(options.FfmpegPath))
            throw new InvalidOperationException("FFmpeg path is not configured.");
        if (!File.Exists(options.FfmpegPath))
            throw new FileNotFoundException("FFmpeg executable not found.", options.FfmpegPath);

        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullOutputDirectory);

        var outputFileName = Path.GetFileNameWithoutExtension(videoPath) + ".wav";
        var outputPath = Path.GetFullPath(Path.Combine(fullOutputDirectory, outputFileName));
        var directoryPrefix = fullOutputDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!outputPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved audio path is outside the output directory.");

        string[] arguments =
        {
            "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
            "-i", videoPath,
            "-vn", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le",
            outputPath
        };

        var result = await processRunner.RunAsync(options.FfmpegPath, arguments, cancellationToken);
        if (result.ExitCode != 0 || !File.Exists(outputPath))
        {
            throw new InvalidOperationException(
                $"FFmpeg audio extraction failed (exit code {result.ExitCode}): {result.StandardError.Trim()}");
        }

        return outputPath;
    }
}
