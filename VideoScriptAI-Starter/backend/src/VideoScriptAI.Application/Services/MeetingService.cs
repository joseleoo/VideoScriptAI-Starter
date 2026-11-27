using System.Text.RegularExpressions;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Domain.Entities;
using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Application.Services;

public sealed partial class MeetingService(
    IMeetingRepository repository,
    IMeetingFileStorage fileStorage,
    IMeetingQueue queue,
    IMeetingCancellationRegistry cancellation,
    IMeetingProgressNotifier notifier,
    IAudioRecorder recorder,
    ISummaryService summaries,
    IMeetingExportService exporter) : IMeetingApplicationService
{
    public async Task<MeetingDto> CreateFromUploadAsync(
        UploadedAudio audio,
        string title,
        string language,
        string summaryProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);

        var normalizedLanguage = NormalizeLanguage(language);
        var provider = ParseProvider(summaryProvider);
        var storedPath = await fileStorage.SaveUploadAsync(audio, cancellationToken);
        var meeting = new MeetingNote(title, MeetingSource.Upload, normalizedLanguage, provider);
        meeting.AttachAudio(storedPath);

        await repository.AddAsync(meeting, cancellationToken);
        await queue.EnqueueAsync(meeting.Id, cancellationToken);
        await PublishAsync(meeting, "Reunión en cola de procesamiento.", cancellationToken);

        return Map(meeting);
    }

    public async Task<MeetingDto> StartRecordingAsync(
        string title,
        string language,
        string summaryProvider,
        CancellationToken cancellationToken)
    {
        if (recorder.IsRecording)
            throw new InvalidOperationException("Ya hay una grabación en curso.");

        var normalizedLanguage = NormalizeLanguage(language);
        var provider = ParseProvider(summaryProvider);
        var meeting = new MeetingNote(title, MeetingSource.Live, normalizedLanguage, provider);

        await repository.AddAsync(meeting, cancellationToken);

        try
        {
            await recorder.StartAsync(meeting.Id, cancellationToken);
        }
        catch
        {
            meeting.Fail("No se pudo iniciar la captura de audio del sistema.");
            await repository.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        await PublishAsync(meeting, "Grabando audio del sistema.", cancellationToken);
        return Map(meeting);
    }

    public async Task<MeetingDto?> StopRecordingAsync(CancellationToken cancellationToken)
    {
        var status = recorder.GetStatus();
        if (!status.IsRecording || status.MeetingId is null)
            return null;

        var meeting = await repository.GetAsync(status.MeetingId.Value, cancellationToken);
        if (meeting is null)
            return null;

        var audioPath = await recorder.StopAsync(cancellationToken);
        meeting.AttachAudio(audioPath);
        await repository.SaveChangesAsync(cancellationToken);
        await queue.EnqueueAsync(meeting.Id, cancellationToken);
        await PublishAsync(meeting, "Grabación finalizada. Procesando reunión.", cancellationToken);

        return Map(meeting);
    }

    public RecordingStatusDto GetRecordingStatus()
    {
        var status = recorder.GetStatus();
        return new RecordingStatusDto(
            status.IsRecording,
            status.MeetingId,
            status.StartedAtUtc,
            status.BytesWritten);
    }

    public async Task<MeetingDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var meeting = await repository.GetAsync(id, cancellationToken);
        return meeting is null ? null : Map(meeting);
    }

    public async Task<IReadOnlyList<MeetingDto>> ListAsync(CancellationToken cancellationToken)
    {
        var meetings = await repository.ListAsync(cancellationToken);
        return meetings.Select(Map).ToArray();
    }

    public async Task<MeetingDto?> UpdateSegmentsAsync(
        Guid id,
        UpdateMeetingSegmentsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var meeting = await repository.GetAsync(id, cancellationToken);
        if (meeting is null)
            return null;

        meeting.UpdateSegments(request.Segments.Select(segment =>
            new MeetingSegmentInput(
                segment.Position,
                segment.StartSeconds,
                segment.EndSeconds,
                segment.Text,
                segment.Speaker)).ToArray());

        await repository.SaveChangesAsync(cancellationToken);
        return Map(meeting);
    }

    public async Task<MeetingDto?> UpdateSummaryAsync(
        Guid id,
        string summary,
        CancellationToken cancellationToken)
    {
        var meeting = await repository.GetAsync(id, cancellationToken);
        if (meeting is null)
            return null;

        meeting.SetSummary(summary, meeting.SummaryProvider);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(meeting);
    }

    public async Task<MeetingDto?> RegenerateSummaryAsync(
        Guid id,
        string? summaryProvider,
        CancellationToken cancellationToken)
    {
        var meeting = await repository.GetAsync(id, cancellationToken);
        if (meeting is null)
            return null;

        var provider = string.IsNullOrWhiteSpace(summaryProvider)
            ? meeting.SummaryProvider
            : ParseProvider(summaryProvider);

        meeting.UpdateProgress(MeetingStatus.Summarizing, Math.Max(meeting.Progress, 90));
        await repository.SaveChangesAsync(cancellationToken);
        await PublishAsync(meeting, "Generando resumen.", cancellationToken);

        try
        {
            string summary;
            try
            {
                summary = await summaries.GenerateAsync(provider, meeting.FullText, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException && provider != SummaryProvider.Extractive)
            {
                summary = await summaries.GenerateAsync(SummaryProvider.Extractive, meeting.FullText, cancellationToken);
                provider = SummaryProvider.Extractive;
            }

            meeting.SetSummary(summary, provider);
            meeting.Complete();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            meeting.Fail("No se pudo generar el resumen: " + exception.Message);
        }

        await repository.SaveChangesAsync(cancellationToken);
        await PublishAsync(meeting, meeting.ErrorMessage ?? "Resumen actualizado.", cancellationToken);
        return Map(meeting);
    }

    public async Task<MeetingDto?> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var meeting = await repository.GetAsync(id, cancellationToken);
        if (meeting is null ||
            meeting.Status != MeetingStatus.Failed ||
            string.IsNullOrWhiteSpace(meeting.AudioPath))
        {
            return null;
        }

        meeting.Retry();
        cancellation.Remove(id);
        await repository.SaveChangesAsync(cancellationToken);
        await queue.EnqueueAsync(id, cancellationToken);
        await PublishAsync(meeting, "Reunión en cola de procesamiento.", cancellationToken);
        return Map(meeting);
    }

    public async Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var meeting = await repository.GetAsync(id, cancellationToken);
        if (meeting is null)
            return false;

        if (meeting.Status is MeetingStatus.Completed
            or MeetingStatus.Failed
            or MeetingStatus.Cancelled)
        {
            return true;
        }

        var status = recorder.GetStatus();
        if (status.IsRecording && status.MeetingId == id)
            await recorder.CancelAsync(cancellationToken);

        cancellation.Cancel(id);
        meeting.Cancel();
        await repository.SaveChangesAsync(cancellationToken);
        await PublishAsync(meeting, "Reunión cancelada.", cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var meeting = await repository.GetAsync(id, cancellationToken);
        if (meeting is null)
            return false;

        var status = recorder.GetStatus();
        if (status.IsRecording && status.MeetingId == id)
            await recorder.CancelAsync(cancellationToken);

        cancellation.Cancel(id);

        if (!string.IsNullOrWhiteSpace(meeting.AudioPath))
        {
            try
            {
                await fileStorage.DeleteAsync(meeting.AudioPath, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
            }
        }

        await repository.RemoveAsync(meeting, cancellationToken);
        return true;
    }

    public async Task<ExportFileDto?> PrepareExportAsync(
        Guid id,
        string format,
        CancellationToken cancellationToken)
    {
        var meeting = await repository.GetAsync(id, cancellationToken);
        return meeting is null ? null : await exporter.ExportAsync(meeting, format, cancellationToken);
    }

    private async Task PublishAsync(
        MeetingNote meeting,
        string message,
        CancellationToken cancellationToken) =>
        await notifier.PublishAsync(
            new MeetingProgressNotification(
                meeting.Id,
                meeting.Status.ToString(),
                meeting.Progress,
                message),
            cancellationToken);

    private static MeetingDto Map(MeetingNote meeting) => new(
        meeting.Id,
        meeting.Title,
        meeting.Source.ToString(),
        meeting.Language,
        meeting.DetectedLanguage,
        meeting.SummaryProvider.ToString(),
        meeting.Status.ToString(),
        meeting.Progress,
        meeting.ErrorMessage,
        meeting.CreatedAtUtc,
        meeting.CompletedAtUtc,
        meeting.FullText,
        meeting.Summary,
        meeting.Segments
            .OrderBy(segment => segment.Position)
            .Select(segment => new MeetingSegmentDto(
                segment.Position,
                segment.StartSeconds,
                segment.EndSeconds,
                segment.Text,
                segment.Speaker))
            .ToArray());

    private static string NormalizeLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        var value = language.Trim();
        if (!LanguagePattern().IsMatch(value))
            throw new ArgumentException("Invalid language code: " + language, nameof(language));

        return value.ToLowerInvariant();
    }

    private static SummaryProvider ParseProvider(string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        if (!Enum.TryParse<SummaryProvider>(provider, ignoreCase: true, out var parsed))
            throw new ArgumentException("Unsupported summary provider: " + provider, nameof(provider));

        return parsed;
    }

    [GeneratedRegex(@"^(auto|[a-z]{2}(?:-[a-z0-9]{2,8})?)$", RegexOptions.IgnoreCase)]
    private static partial Regex LanguagePattern();
}
