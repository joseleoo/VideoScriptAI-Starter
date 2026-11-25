using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Application.Configuration;

public sealed class EngineOptions
{
    public string FfmpegPath { get; set; } = string.Empty;
    public string WhisperPath { get; set; } = string.Empty;
    public string ModelsPath { get; set; } = string.Empty;
    public int WhisperThreads { get; set; } = 6;

    public string GetModelPath(TranscriptionModel model)
    {
        var fileName = model switch
        {
            TranscriptionModel.Base => "ggml-base.bin",
            TranscriptionModel.Small => "ggml-small.bin",
            TranscriptionModel.Medium => "ggml-medium.bin",
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unsupported transcription model.")
        };

        return Path.GetFullPath(Path.Combine(ModelsPath, fileName));
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FfmpegPath))
            throw new InvalidOperationException("FFmpeg path is not configured.");
        if (!File.Exists(FfmpegPath))
            throw new FileNotFoundException("FFmpeg executable not found.", FfmpegPath);
        if (string.IsNullOrWhiteSpace(WhisperPath))
            throw new InvalidOperationException("whisper.cpp path is not configured.");
        if (!File.Exists(WhisperPath))
            throw new FileNotFoundException("whisper.cpp executable not found.", WhisperPath);
        if (string.IsNullOrWhiteSpace(ModelsPath))
            throw new InvalidOperationException("Models folder is not configured.");
        if (!Directory.Exists(ModelsPath))
            throw new DirectoryNotFoundException("Models folder not found: " + ModelsPath);
        if (WhisperThreads is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(WhisperThreads), "Thread count must be between 1 and 32.");
    }
}
