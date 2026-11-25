namespace VideoScriptAI.Application.Abstractions;

public interface IAudioExtractor
{
    Task<string> ExtractWavAsync(string videoPath, string outputDirectory,
        CancellationToken cancellationToken);
}
