namespace VideoScriptAI.Application.Dtos;

public sealed record TtsVoiceDto(
    string Provider,
    string Id,
    string Name,
    string Locale,
    string Gender);

public sealed record SynthesizeSpeechRequest(
    string? Text,
    string? Provider,
    string? VoiceId);

public sealed record SynthesizedSpeechDto(
    string FileName,
    string ContentType,
    string Path);
