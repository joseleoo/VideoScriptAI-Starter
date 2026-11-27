using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Domain.Enums;
using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class MeetingWorker(
    IServiceScopeFactory scopeFactory,
    IMeetingQueue queue,
    IMeetingCancellationRegistry cancellation,
    IMeetingProgressNotifier notifier,
    ILogger<MeetingWorker> logger,
    EngineOptions engineOptions,
    StorageOptions storageOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Guid meetingId;
            try
            {
                meetingId = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessMeetingAsync(meetingId, stoppingToken);
        }
    }

    private async Task ProcessMeetingAsync(Guid meetingId, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMeetingRepository>();
        var audioExtractor = scope.ServiceProvider.GetRequiredService<IAudioExtractor>();
        var transcriptionEngine = scope.ServiceProvider.GetRequiredService<ITranscriptionEngine>();
        var summaries = scope.ServiceProvider.GetRequiredService<ISummaryService>();

        var meeting = await repository.GetAsync(meetingId, stoppingToken);
        if (meeting is null)
        {
            cancellation.Remove(meetingId);
            return;
        }

        if (meeting.Status is MeetingStatus.Cancelled or MeetingStatus.Completed)
        {
            cancellation.Remove(meetingId);
            return;
        }

        if (string.IsNullOrWhiteSpace(meeting.AudioPath) || !File.Exists(meeting.AudioPath))
        {
            meeting.Fail("No se encontró el archivo de audio de la reunión.");
            await repository.SaveChangesAsync(stoppingToken);
            await notifier.PublishAsync(
                new MeetingProgressNotification(meeting.Id, meeting.Status.ToString(), meeting.Progress, meeting.ErrorMessage),
                stoppingToken);
            cancellation.Remove(meetingId);
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            cancellation.GetToken(meetingId));

        try
        {
            await SetProgressAsync(meeting, repository, MeetingStatus.Validating, 10,
                "Validando audio y motores locales.", linked.Token);

            engineOptions.Validate();
            var modelPath = engineOptions.GetModelPath(TranscriptionModel.Small);
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Whisper model not found.", modelPath);

            await SetProgressAsync(meeting, repository, MeetingStatus.Validating, 25,
                "Normalizando el audio a 16 kHz mono.", linked.Token);

            var audioDirectory = Path.Combine(storageOptions.AudioPath, "meetings", meeting.Id.ToString("N"));
            var audioPath = await audioExtractor.ExtractWavAsync(meeting.AudioPath, audioDirectory, linked.Token);

            var progress = new Progress<int>(value =>
            {
                if (linked.IsCancellationRequested || meeting.Status is MeetingStatus.Cancelled or MeetingStatus.Failed)
                    return;

                var scaled = Math.Clamp(45 + value * 40 / 100, 45, 85);
                meeting.UpdateProgress(MeetingStatus.Transcribing, scaled);
                _ = notifier.PublishAsync(
                    new MeetingProgressNotification(
                        meeting.Id,
                        MeetingStatus.Transcribing.ToString(),
                        scaled,
                        "Transcribiendo la reunión."),
                    CancellationToken.None);
            });

            var transcription = await transcriptionEngine.TranscribeAsync(
                new TranscriptionRequest(audioPath, modelPath, meeting.Language),
                progress,
                linked.Token);

            meeting.SetTranscript(
                transcription.Language,
                transcription.Segments.Select((segment, index) =>
                    new MeetingSegmentInput(index, segment.StartSeconds, segment.EndSeconds, segment.Text, string.Empty)).ToArray());

            await repository.SaveChangesAsync(linked.Token);

            await SetProgressAsync(meeting, repository, MeetingStatus.Summarizing, 90,
                "Generando el resumen de la reunión.", linked.Token);

            var provider = meeting.SummaryProvider;
            if (string.IsNullOrWhiteSpace(meeting.FullText))
                throw new InvalidOperationException("La transcripción está vacía; no se puede generar un resumen.");

            var summary = await GenerateSummaryWithFallbackAsync(
                summaries,
                provider,
                meeting.FullText,
                linked.Token);
            meeting.SetSummary(summary, provider);

            meeting.Complete();
            await repository.SaveChangesAsync(linked.Token);
            await notifier.PublishAsync(
                new MeetingProgressNotification(meeting.Id, meeting.Status.ToString(), meeting.Progress, "Notas de reunión listas."),
                linked.Token);
        }
        catch (OperationCanceledException)
        {
            if (meeting.Status == MeetingStatus.Cancelled)
                return;

            meeting.Cancel();
            await repository.SaveChangesAsync(CancellationToken.None);
            await notifier.PublishAsync(
                new MeetingProgressNotification(meeting.Id, meeting.Status.ToString(), meeting.Progress, "Reunión cancelada."),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "La reunión {MeetingId} falló.", meetingId);
            meeting.Fail(exception.Message);
            await repository.SaveChangesAsync(CancellationToken.None);
            await notifier.PublishAsync(
                new MeetingProgressNotification(meeting.Id, meeting.Status.ToString(), meeting.Progress, exception.Message),
                CancellationToken.None);
        }
        finally
        {
            cancellation.Remove(meetingId);
        }
    }

    private async Task<string> GenerateSummaryWithFallbackAsync(
        ISummaryService summaries,
        SummaryProvider provider,
        string transcript,
        CancellationToken cancellationToken)
    {
        try
        {
            return await summaries.GenerateAsync(provider, transcript, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException && provider != SummaryProvider.Extractive)
        {
            logger.LogWarning(exception, "El proveedor {Provider} no estuvo disponible; se usa el resumen extractivo.", provider);
            return await summaries.GenerateAsync(SummaryProvider.Extractive, transcript, cancellationToken);
        }
    }

    private async Task SetProgressAsync(
        MeetingNote meeting,
        IMeetingRepository repository,
        MeetingStatus status,
        int progress,
        string message,
        CancellationToken cancellationToken)
    {
        meeting.UpdateProgress(status, progress);
        await repository.SaveChangesAsync(cancellationToken);
        await notifier.PublishAsync(
            new MeetingProgressNotification(meeting.Id, status.ToString(), progress, message),
            cancellationToken);
    }
}
