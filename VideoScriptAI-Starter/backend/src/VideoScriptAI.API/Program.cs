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
builder.Services.AddHttpClient();
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
Directory.CreateDirectory(storageOptions.SpeechPath);
Directory.CreateDirectory(storageOptions.ExportsPath);

var maxTextLength = builder.Configuration.GetValue("TextToSpeech:MaxTextLength", 100_000);
if (int.TryParse(Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_TTS_MAX_TEXT_LENGTH"), out var envMaxTextLength) && envMaxTextLength > 0)
    maxTextLength = envMaxTextLength;

var speechOptions = new TextToSpeechOptions(
    Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_PYTHON_PATH")
    ?? builder.Configuration["TextToSpeech:PythonPath"]
    ?? "python",
    Math.Max(1, maxTextLength));

var summaryOptions = builder.Configuration.GetSection("Summary").Get<SummaryOptions>() ?? new SummaryOptions();
summaryOptions.DefaultProvider =
    Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_SUMMARY_PROVIDER") ?? summaryOptions.DefaultProvider;
summaryOptions.OllamaBaseUrl =
    Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_OLLAMA_URL") ?? summaryOptions.OllamaBaseUrl;
summaryOptions.OllamaModel =
    Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_OLLAMA_MODEL") ?? summaryOptions.OllamaModel;
summaryOptions.OpenAiBaseUrl =
    Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_OPENAI_BASE_URL") ?? summaryOptions.OpenAiBaseUrl;
summaryOptions.OpenAiModel =
    Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_OPENAI_MODEL") ?? summaryOptions.OpenAiModel;
summaryOptions.OpenAiApiKey =
    Environment.GetEnvironmentVariable("VIDEOSCRIPTAI_OPENAI_API_KEY") ?? summaryOptions.OpenAiApiKey;

Directory.CreateDirectory(storageOptions.MeetingsPath);

builder.Services.AddSingleton(engineOptions);
builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton(speechOptions);
builder.Services.AddSingleton(summaryOptions);
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
builder.Services.AddSingleton<ITextToSpeechService, TextToSpeechService>();
builder.Services.AddSingleton<IMeetingQueue, MeetingQueue>();
builder.Services.AddSingleton<IMeetingCancellationRegistry, MeetingCancellationRegistry>();
builder.Services.AddSingleton<IMeetingProgressNotifier, SignalRMeetingProgressNotifier>();
builder.Services.AddSingleton<IMeetingFileStorage, MeetingFileStorage>();
builder.Services.AddSingleton<IMeetingExportService, MeetingExportService>();
builder.Services.AddSingleton<ISummaryProvider, OllamaSummaryProvider>();
builder.Services.AddSingleton<ISummaryProvider, OpenAiSummaryProvider>();
builder.Services.AddSingleton<ISummaryProvider, ExtractiveSummaryProvider>();
builder.Services.AddSingleton<ISummaryService, SummaryService>();

if (OperatingSystem.IsWindows())
    builder.Services.AddSingleton<IAudioRecorder, WasapiAudioRecorder>();
else
    builder.Services.AddSingleton<IAudioRecorder, UnsupportedAudioRecorder>();

builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IJobApplicationService, TranscriptionJobService>();
builder.Services.AddScoped<IMeetingRepository, MeetingRepository>();
builder.Services.AddScoped<IMeetingApplicationService, MeetingService>();
builder.Services.AddHostedService<TranscriptionWorker>();
builder.Services.AddHostedService<MeetingWorker>();

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
    recordingAvailable = OperatingSystem.IsWindows(),
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
