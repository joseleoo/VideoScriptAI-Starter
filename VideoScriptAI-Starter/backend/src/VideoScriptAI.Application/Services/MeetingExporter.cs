using System.Text.Json;
using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Application.Services;

public static class MeetingExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Format(MeetingNote meeting, string format)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        return format.Trim().ToLowerInvariant() switch
        {
            "md" => ToMarkdown(meeting),
            "txt" => ToTxt(meeting),
            "json" => ToJson(meeting),
            _ => throw new ArgumentException("Unsupported export format: " + format, nameof(format))
        };
    }

    public static string ToTxt(MeetingNote meeting)
    {
        var language = string.IsNullOrWhiteSpace(meeting.DetectedLanguage) ? meeting.Language : meeting.DetectedLanguage;
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(meeting.Title);
        builder.AppendLine($"Fecha: {meeting.CreatedAtUtc:u}");
        builder.AppendLine($"Idioma: {language}");
        builder.AppendLine();
        builder.AppendLine("RESUMEN");
        builder.AppendLine(string.IsNullOrWhiteSpace(meeting.Summary) ? "(sin resumen)" : meeting.Summary);
        builder.AppendLine();
        builder.AppendLine("TRANSCRIPCIÓN");
        foreach (var segment in OrderedSegments(meeting))
        {
            var speaker = string.IsNullOrWhiteSpace(segment.Speaker) ? "" : segment.Speaker + ": ";
            builder.AppendLine($"[{FormatTime(segment.StartSeconds)}] {speaker}{segment.Text}");
        }

        return builder.ToString();
    }

    public static string ToMarkdown(MeetingNote meeting)
    {
        var language = string.IsNullOrWhiteSpace(meeting.DetectedLanguage) ? meeting.Language : meeting.DetectedLanguage;
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"# {meeting.Title}");
        builder.AppendLine();
        builder.AppendLine($"**Fecha:** {meeting.CreatedAtUtc:u}  ");
        builder.AppendLine($"**Idioma:** {language}  ");
        builder.AppendLine($"**Resumen con:** {meeting.SummaryProvider}");
        builder.AppendLine();
        builder.AppendLine("## Resumen");
        builder.AppendLine();
        builder.AppendLine(string.IsNullOrWhiteSpace(meeting.Summary) ? "_Sin resumen._" : meeting.Summary);
        builder.AppendLine();
        builder.AppendLine("## Transcripción");
        builder.AppendLine();
        builder.AppendLine("| Tiempo | Hablante | Texto |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var segment in OrderedSegments(meeting))
        {
            var speaker = segment.Speaker.Replace("|", "\\|");
            var text = segment.Text.Replace("|", "\\|").Replace(Environment.NewLine, " ");
            builder.AppendLine($"| {FormatTime(segment.StartSeconds)} | {speaker} | {text} |");
        }

        return builder.ToString();
    }

    public static string ToJson(MeetingNote meeting)
    {
        var payload = new
        {
            id = meeting.Id,
            title = meeting.Title,
            source = meeting.Source.ToString(),
            createdAtUtc = meeting.CreatedAtUtc,
            language = string.IsNullOrWhiteSpace(meeting.DetectedLanguage) ? meeting.Language : meeting.DetectedLanguage,
            summaryProvider = meeting.SummaryProvider.ToString(),
            summary = meeting.Summary,
            fullText = meeting.FullText,
            segments = OrderedSegments(meeting).Select(segment => new
            {
                position = segment.Position,
                startSeconds = segment.StartSeconds,
                endSeconds = segment.EndSeconds,
                speaker = segment.Speaker,
                text = segment.Text
            }).ToArray()
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static IEnumerable<MeetingSegment> OrderedSegments(MeetingNote meeting) =>
        meeting.Segments.OrderBy(segment => segment.Position);

    private static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            return "00:00";

        var value = TimeSpan.FromMilliseconds(Math.Round(seconds * 1000d));
        return $"{(long)value.TotalMinutes:00}:{value.Seconds:00}";
    }
}
