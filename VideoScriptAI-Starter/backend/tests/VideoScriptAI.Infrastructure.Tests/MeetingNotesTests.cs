using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Services;
using VideoScriptAI.Domain.Entities;
using VideoScriptAI.Domain.Enums;
using VideoScriptAI.Infrastructure.Persistence;
using VideoScriptAI.Infrastructure.Processing;
using Xunit;

namespace VideoScriptAI.Infrastructure.Tests;

public sealed class MeetingNotesTests
{
    [Fact]
    public async Task Extractive_summary_keeps_relevant_sentences_in_order()
    {
        var provider = new ExtractiveSummaryProvider();

        var summary = await provider.GenerateAsync(
            "El contrato se firmó ayer. " +
            "El contrato incluye cláusulas nuevas. " +
            "El contrato fue revisado por el equipo. " +
            "Hoy hace buen tiempo. " +
            "Mañana lloverá.",
            CancellationToken.None);

        Assert.Contains("extractivo", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("El contrato se firmó ayer.", summary);
        Assert.DoesNotContain("Mañana lloverá.", summary);
    }

    [Fact]
    public void Meeting_exporter_includes_summary_speakers_and_json()
    {
        var meeting = new MeetingNote("Sprint review", MeetingSource.Upload, "es", SummaryProvider.Ollama);
        meeting.SetTranscript("es", new[]
        {
            new MeetingSegmentInput(0, 0, 3, "Revisamos el sprint", "Ana")
        });
        meeting.SetSummary("Se cerró el sprint.", SummaryProvider.Ollama);

        var markdown = MeetingExporter.ToMarkdown(meeting);
        Assert.Contains("# Sprint review", markdown);
        Assert.Contains("| Ana | Revisamos el sprint |", markdown);
        Assert.Contains("Se cerró el sprint.", markdown);

        using var json = System.Text.Json.JsonDocument.Parse(MeetingExporter.ToJson(meeting));
        Assert.Equal("Se cerró el sprint.", json.RootElement.GetProperty("summary").GetString());
        Assert.Equal("Ana", json.RootElement.GetProperty("segments")[0].GetProperty("speaker").GetString());
    }

    [Fact]
    public async Task Meeting_file_storage_deletes_stored_audio_and_rejects_outside_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"vsa-meetings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "meetings"));

        try
        {
            var storage = new MeetingFileStorage(new StorageOptions(root, 1024 * 1024));
            var path = Path.Combine(root, "meetings", "sample.wav");
            await File.WriteAllTextAsync(path, "audio");

            await storage.DeleteAsync(path, CancellationToken.None);
            Assert.False(File.Exists(path));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                storage.DeleteAsync(Path.Combine(root, "outside.wav"), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
