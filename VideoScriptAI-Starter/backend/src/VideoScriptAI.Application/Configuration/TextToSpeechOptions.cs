namespace VideoScriptAI.Application.Configuration;

public sealed record TextToSpeechOptions(string PythonPath, int MaxTextLength = 100_000);
