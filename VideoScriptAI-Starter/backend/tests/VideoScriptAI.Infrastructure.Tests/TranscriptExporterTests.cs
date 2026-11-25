using VideoScriptAI.Application.Services;
using VideoScriptAI.Domain.Entities;
using VideoScriptAI.Domain.Enums;
using Xunit;

namespace VideoScriptAI.Infrastructure.Tests;

public sealed class TranscriptExporterTests
{
    [Fact]
    public void Timestamps_round_milliseconds_and_roll_over_hours()
    {
        Assert.Equal("00:00:01,500", TranscriptExporter.FormatSrtTimestamp(1.5));
        Assert.Equal("01:01:02,035", TranscriptExporter.FormatSrtTimestamp(3662.0346));
        Assert.Equal("01:01:02.035", TranscriptExporter.FormatVttTimestamp(3662.0346));
    }

    [Fact]
    public void Exports_txt_srt_vtt_and_json()
    {
        var job = NewJob();
        job.SetTranscript("en", new[]
        {
            new TranscriptSegmentInput(0, 1.25, "Hello"),
            new TranscriptSegmentInput(1.25, 62.5, "World")
        });

        Assert.Contains("Hello World", TranscriptExporter.ToTxt(job));

        var srt = TranscriptExporter.ToSrt(job);
        Assert.Contains("00:00:00,000 --> 00:00:01,250", srt);
        Assert.Contains("00:00:01,250 --> 00:01:02,500", srt);

        var vtt = TranscriptExporter.ToVtt(job);
        Assert.StartsWith("WEBVTT", vtt);
        Assert.Contains("00:00:01.250 --> 00:01:02.500", vtt);

        var json = TranscriptExporter.ToJson(job);
        Assert.Contains("\"language\": \"en\"", json);
        Assert.Contains("\"model\": \"small\"", json);
        Assert.Contains("\"text\": \"Hello\"", json);
    }

    private static TranscriptionJob NewJob() =>
        new("sample.mp4", "C:\\VideoScriptAI\\uploads\\sample.mp4", "auto", TranscriptionModel.Small);
}
