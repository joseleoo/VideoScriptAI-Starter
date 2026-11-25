using Microsoft.EntityFrameworkCore;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Infrastructure.Persistence;
using VideoScriptAI.Infrastructure.Processing;

var builder = WebApplication.CreateBuilder(args);

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
    timestampUtc = DateTimeOffset.UtcNow
}));

app.Run();
