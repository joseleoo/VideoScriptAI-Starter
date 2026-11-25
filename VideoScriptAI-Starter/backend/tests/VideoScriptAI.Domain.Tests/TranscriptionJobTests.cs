using VideoScriptAI.Domain.Entities;
using VideoScriptAI.Domain.Enums;

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
