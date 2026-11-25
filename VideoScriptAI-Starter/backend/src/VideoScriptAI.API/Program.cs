using Microsoft.EntityFrameworkCore;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Domain.Enums;
using VideoScriptAI.Infrastructure.Persistence;
using VideoScriptAI.Infrastructure.Processing;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
    optional: false,
    reloadOnChange: true);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalReact", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var dataDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "VideoScriptAI");
Directory.CreateDirectory(dataDirectory);

var databasePath = Path.Combine(dataDirectory, "videoscriptai.db");
builder.Services.AddDbContext<VideoScriptDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddSingleton<IJobQueue, JobQueue>();

var engineOptions = builder.Configuration.GetSection("Engine").Get<EngineOptions>() ?? new EngineOptions();
engineOptions.FfmpegPath = ResolvePath(engineOptions.FfmpegPath);
engineOptions.WhisperPath = ResolvePath(engineOptions.WhisperPath);
engineOptions.ModelsPath = ResolvePath(engineOptions.ModelsPath);
builder.Services.AddSingleton(engineOptions);
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<IAudioExtractor, FfmpegAudioExtractor>();
builder.Services.AddSingleton<ITranscriptionEngine, WhisperCliTranscriptionEngine>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("LocalReact");
app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    application = "VideoScriptAI",
    enginesReady = File.Exists(engineOptions.FfmpegPath)
        && File.Exists(engineOptions.WhisperPath)
        && File.Exists(engineOptions.GetModelPath(TranscriptionModel.Small)),
    timestampUtc = DateTimeOffset.UtcNow
}));

app.Run();

static string ResolvePath(string path)
{
    if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        return path;

    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
         directory is not null;
         directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, path);
        if (File.Exists(candidate) || Directory.Exists(candidate))
            return candidate;
    }

    return Path.GetFullPath(path);
}
