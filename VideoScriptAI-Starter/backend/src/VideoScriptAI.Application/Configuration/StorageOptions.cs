namespace VideoScriptAI.Application.Configuration;

public sealed record StorageOptions(string RootPath, long MaxUploadBytes)
{
    public string UploadsPath => Path.Combine(RootPath, "uploads");
    public string AudioPath => Path.Combine(RootPath, "audio");
    public string ExportsPath => Path.Combine(RootPath, "exports");
    public string DatabasePath => Path.Combine(RootPath, "videoscriptai.db");
}
