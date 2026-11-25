using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VideoScriptAI.API.Hubs;
using VideoScriptAI.API.Notifications;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Services;
using VideoScriptAI.Domain.Enums;
using VideoScriptAI.Infrastructure.Persistence;
using VideoScriptAI.Infrastructure.Processing;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
    optional: false,
    reloadOnChange: true);

const long defaultMaxUploadBytes = 2_147_483_648L;
var configuredMaxUploadBytes = builder.Configuration["Uploads:MaxBytes"];
var maxUploadBytes = long.TryParse(configuredMaxUploadBytes, out var parsedMaxUploadBytes) && parsedMaxUploadBytes > 0
    ? parsedMaxUploadBytes
    : defaultMaxUploadBytes;

builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = maxUploadBytes);

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

var engineOptions = builder.Configuration.GetSection("Engine").Get<EngineOptions>() ?? new EngineOptions();
engineOptions.FfmpegPath = ResolvePath(
    Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_FFMPEG_PATH") ?? engineOptions.FfmpegPath);
engineOptions.WhisperPath = ResolvePath(
    Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_WHISPER_PATH") ?? engineOptions.WhisperPath);
engineOptions.ModelsPath = ResolvePath(
    Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_MODELS_PATH") ?? engineOptions.ModelsPath);

var storageRoot = Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_STORAGE_PATH");
if (string.IsNullOrWhiteSpace(storageRoot))
    storageRoot = builder.Configuration["Storage:RootPath"];
if (string.IsNullOrWhiteSpace(storageRoot))
{
    storageRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VideoScriptAI");
}

var storageOptions = new StorageOptions(Path.GetFullPath(storageRoot), maxUploadBytes);
Directory.CreateDirectory(storageOptions.RootPath);
Directory.CreateDirectory(storageOptions.UploadsPath);
Directory.CreateDirectory(storageOptions.AudioPath);
Directory.CreateDirectory(storageOptions.ExportsPath);

builder.Services.AddSingleton(engineOptions);
builder.Services.AddSingleton(storageOptions);
builder.Services.AddDbContext<VideoScriptDbContext>(options =>
    options.UseSqlite($"Data Source={storageOptions.DatabasePath}"));

builder.Services.AddSingleton<IJobQueue, JobQueue>();
builder.Services.AddSingleton<IJobCancellationRegistry, JobCancellationRegistry>();
builder.Services.AddSingleton<IJobProgressNotifier, SignalRJobProgressNotifier>();
builder.Services.AddSingleton<IJobFileStorage, JobFileStorage>();
builder.Services.AddSingleton<IExportService, ExportService>();
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<IAudioExtractor, FfmpegAudioExtractor>();
builder.Services.AddSingleton<ITranscriptionEngine, WhisperCliTranscriptionEngine>();
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IJobApplicationService, TranscriptionJobService>();
builder.Services.AddHostedService<TranscriptionWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<VideoScriptDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("LocalReact");
app.MapControllers();
app.MapHub<JobsHub>("/hubs/jobs");

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
