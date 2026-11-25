using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Domain.Entities;
using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class TranscriptionWorker(
    IServiceScopeFactory scopeFactory,
    IJobQueue queue,
    IJobCancellationRegistry cancellation,
    IJobProgressNotifier notifier,
    ILogger<TranscriptionWorker> logger,
    EngineOptions engineOptions,
    StorageOptions storageOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Guid jobId;
            try
            {
                jobId = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessJobAsync(jobId, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var audioExtractor = scope.ServiceProvider.GetRequiredService<IAudioExtractor>();
        var transcriptionEngine = scope.ServiceProvider.GetRequiredService<ITranscriptionEngine>();
        var exportService = scope.ServiceProvider.GetRequiredService<IExportService>();

        var job = await repository.GetAsync(jobId, stoppingToken);
        if (job is null)
        {
            cancellation.Remove(jobId);
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            cancellation.GetToken(jobId));

        try
        {
            await SetProgressAsync(job, repository, TranscriptionStatus.Validating, 10,
                "Validating video and engine configuration.", linked.Token);

            engineOptions.Validate();
            var modelPath = engineOptions.GetModelPath(job.Model);
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Whisper model not found.", modelPath);

            await SetProgressAsync(job, repository, TranscriptionStatus.ExtractingAudio, 30,
                "Extracting 16 kHz mono audio.", linked.Token);

            var audioDirectory = Path.Combine(storageOptions.AudioPath, job.Id.ToString("N"));
            var audioPath = await audioExtractor.ExtractWavAsync(
                job.StoredVideoPath,
                audioDirectory,
                linked.Token);

            var lastTranscriptionProgress = 45;
            var progress = new Progress<int>(value =>
            {
                lastTranscriptionProgress = Math.Clamp(45 + value * 45 / 100, 45, 90);
                job.UpdateProgress(TranscriptionStatus.Transcribing, lastTranscriptionProgress);
                _ = notifier.PublishAsync(
                    new JobProgressNotification(
                        job.Id,
                        TranscriptionStatus.Transcribing.ToString(),
                        lastTranscriptionProgress,
                        "Transcribing audio."),
                    CancellationToken.None);
            });

            var result = await transcriptionEngine.TranscribeAsync(
                new TranscriptionRequest(audioPath, modelPath, job.Language),
                progress,
                linked.Token);

            job.SetTranscript(
                result.Language,
                result.Segments.Select(segment =>
                    new TranscriptSegmentInput(segment.StartSeconds, segment.EndSeconds, segment.Text)).ToArray());

            await SetProgressAsync(job, repository, TranscriptionStatus.Exporting, 95,
                "Generating transcript exports.", linked.Token);

            foreach (var format in new[] { "txt", "srt", "vtt", "json" })
                await exportService.ExportAsync(job, format, linked.Token);

            job.Complete();
            await repository.SaveChangesAsync(linked.Token);
            await notifier.PublishAsync(
                new JobProgressNotification(job.Id, job.Status.ToString(), job.Progress, "Transcription complete."),
                linked.Token);
        }
        catch (OperationCanceledException)
        {
            job.Cancel();
            await repository.SaveChangesAsync(CancellationToken.None);
            await notifier.PublishAsync(
                new JobProgressNotification(job.Id, job.Status.ToString(), job.Progress, "Transcription cancelled."),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Transcription job {JobId} failed.", jobId);
            job.Fail(exception.Message);
            await repository.SaveChangesAsync(CancellationToken.None);
            await notifier.PublishAsync(
                new JobProgressNotification(job.Id, job.Status.ToString(), job.Progress, exception.Message),
                CancellationToken.None);
        }
        finally
        {
            cancellation.Remove(jobId);
        }
    }

    private async Task SetProgressAsync(
        TranscriptionJob job,
        IJobRepository repository,
        TranscriptionStatus status,
        int progress,
        string message,
        CancellationToken cancellationToken)
    {
        job.UpdateProgress(status, progress);
        await repository.SaveChangesAsync(cancellationToken);
        await notifier.PublishAsync(
            new JobProgressNotification(job.Id, status.ToString(), progress, message),
            cancellationToken);
    }
}
