using VideoScriptAI.Domain.Entities;
using VideoScriptAI.Domain.Enums;
using Xunit;

namespace VideoScriptAI.Domain.Tests;

public sealed class MeetingNoteTests
{
    [Fact]
    public void Live_meeting_starts_recording_and_moves_to_pending_when_audio_is_attached()
    {
        var meeting = new MeetingNote("Daily", MeetingSource.Live, "es", SummaryProvider.Ollama);

        Assert.Equal(MeetingStatus.Recording, meeting.Status);
        Assert.Equal("Daily", meeting.Title);

        meeting.AttachAudio("C:\\VideoScriptAI\\meetings\\daily.wav");

        Assert.Equal(MeetingStatus.Pending, meeting.Status);
        Assert.Equal(0, meeting.Progress);
    }

    [Fact]
    public void Transcript_includes_speaker_labels_and_updates_keep_them()
    {
        var meeting = NewMeeting();
        meeting.SetTranscript("es", new[]
        {
            new MeetingSegmentInput(0, 0, 2, "Hola equipo", "Ana"),
            new MeetingSegmentInput(1, 2, 4, "Vamos a empezar", "Luis")
        });

        Assert.Equal($"Ana: Hola equipo{Environment.NewLine}Luis: Vamos a empezar", meeting.FullText);

        meeting.UpdateSegments(new[]
        {
            new MeetingSegmentInput(0, 0, 2, "Hola a todos", "Ana")
        });

        Assert.Contains("Ana: Hola a todos", meeting.FullText);
        Assert.Contains("Luis: Vamos a empezar", meeting.FullText);
        Assert.Equal("Luis", meeting.Segments.Last().Speaker);
    }

    [Fact]
    public void Complete_and_set_summary_store_the_result()
    {
        var meeting = NewMeeting();

        meeting.SetSummary("Resumen de la reunión.", SummaryProvider.Extractive);
        meeting.Complete();

        Assert.Equal(MeetingStatus.Completed, meeting.Status);
        Assert.Equal(100, meeting.Progress);
        Assert.Equal("Resumen de la reunión.", meeting.Summary);
        Assert.Equal(SummaryProvider.Extractive, meeting.SummaryProvider);
        Assert.NotNull(meeting.CompletedAtUtc);
    }

    [Fact]
    public void Retry_clears_failed_conversion_and_prepares_the_meeting_again()
    {
        var meeting = NewMeeting();
        meeting.AttachAudio("C:\\VideoScriptAI\\meetings\\daily.wav");
        meeting.SetTranscript("es", new[] { new MeetingSegmentInput(0, 0, 2, "Parcial", string.Empty) });
        meeting.SetSummary("Resumen parcial", SummaryProvider.Extractive);
        meeting.Fail("Fallo al transcribir");

        meeting.Retry();

        Assert.Equal(MeetingStatus.Pending, meeting.Status);
        Assert.Equal(0, meeting.Progress);
        Assert.Null(meeting.ErrorMessage);
        Assert.Null(meeting.CompletedAtUtc);
        Assert.Empty(meeting.Segments);
        Assert.Equal(string.Empty, meeting.FullText);
        Assert.Equal(string.Empty, meeting.Summary);
    }

    private static MeetingNote NewMeeting() =>
        new("Reunión de prueba", MeetingSource.Upload, "es", SummaryProvider.Extractive);
}
