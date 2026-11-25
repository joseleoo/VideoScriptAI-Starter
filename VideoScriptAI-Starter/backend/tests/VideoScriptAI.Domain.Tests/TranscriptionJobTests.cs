using VideoScriptAI.Domain.Entities;
using VideoScriptAI.Domain.Enums;
using Xunit;

namespace VideoScriptAI.Domain.Tests;

public sealed class TranscriptionJobTests
{
    [Fact]
    public void New_job_starts_pending_with_zero_progress()
    {
        var job = new TranscriptionJob(
            "sample.mp4",
            "C:\\VideoScriptAI\\uploads\\sample.mp4",
            "auto",
            TranscriptionModel.Small);

        Assert.Equal(TranscriptionStatus.Pending, job.Status);
        Assert.Equal(0, job.Progress);
    }

    [Fact]
    public void SetTranscript_builds_positioned_segments_and_full_text()
    {
        var job = new TranscriptionJob(
            "sample.mp4",
            "C:\\VideoScriptAI\\uploads\\sample.mp4",
            "auto",
            TranscriptionModel.Small);

        job.SetTranscript("es", new[]
        {
            new TranscriptSegmentInput(0, 1.5, " Hola "),
            new TranscriptSegmentInput(1.5, 3.25, "mundo")
        });

        Assert.Equal("es", job.DetectedLanguage);
        Assert.Equal("Hola mundo", job.FullText);
        Assert.Equal(2, job.Segments.Count);
        Assert.Equal(0, job.Segments.First().Position);
        Assert.Equal("Hola", job.Segments.First().Text);
    }

    [Fact]
    public void Complete_sets_completed_status_and_full_progress()
    {
        var job = new TranscriptionJob(
            "sample.mp4",
            "C:\\VideoScriptAI\\uploads\\sample.mp4",
            "es",
            TranscriptionModel.Small);

        job.Complete();

        Assert.Equal(TranscriptionStatus.Completed, job.Status);
        Assert.Equal(100, job.Progress);
        Assert.NotNull(job.CompletedAtUtc);
    }
}
