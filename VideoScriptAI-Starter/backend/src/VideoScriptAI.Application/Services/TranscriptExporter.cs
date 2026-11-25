using System.Globalization;
using System.Text;
using System.Text.Json;
using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Application.Services;

public static class TranscriptExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Format(TranscriptionJob job, string format)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        return format.Trim().ToLowerInvariant() switch
        {
            "txt" => ToTxt(job),
            "srt" => ToSrt(job),
            "vtt" => ToVtt(job),
            "json" => ToJson(job),
            _ => throw new ArgumentException("Unsupported export format: " + format, nameof(format))
        };
    }

    public static string ToTxt(TranscriptionJob job) =>
        job.FullText.TrimEnd() + Environment.NewLine;

    public static string ToSrt(TranscriptionJob job)
    {
        var builder = new StringBuilder();
        foreach (var (segment, index) in OrderedSegments(job).Select((segment, index) => (segment, index)))
        {
            builder.AppendLine((index + 1).ToString(CultureInfo.InvariantCulture));
            builder.AppendLine($"{FormatSrtTimestamp(segment.StartSeconds)} --> {FormatSrtTimestamp(segment.EndSeconds)}");
            builder.AppendLine(segment.Text);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static string ToVtt(TranscriptionJob job)
    {
        var builder = new StringBuilder();
        builder.AppendLine("WEBVTT");
        builder.AppendLine();

        foreach (var segment in OrderedSegments(job))
        {
            builder.AppendLine($"{FormatVttTimestamp(segment.StartSeconds)} --> {FormatVttTimestamp(segment.EndSeconds)}");
            builder.AppendLine(segment.Text);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static string ToJson(TranscriptionJob job)
    {
        var payload = new
        {
            language = string.IsNullOrWhiteSpace(job.DetectedLanguage) ? job.Language : job.DetectedLanguage,
            model = job.Model.ToString().ToLowerInvariant(),
            fullText = job.FullText,
            segments = OrderedSegments(job).Select(segment => new
            {
                startSeconds = segment.StartSeconds,
                endSeconds = segment.EndSeconds,
                text = segment.Text
            }).ToArray()
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string FormatSrtTimestamp(double seconds) =>
        FormatTimestamp(ToTimeSpan(seconds), ",");

    public static string FormatVttTimestamp(double seconds) =>
        FormatTimestamp(ToTimeSpan(seconds), ".");

    private static IEnumerable<TranscriptSegment> OrderedSegments(TranscriptionJob job) =>
        job.Segments.OrderBy(x => x.Position);

    private static TimeSpan ToTimeSpan(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));

        var milliseconds = (long)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static string FormatTimestamp(TimeSpan value, string millisecondSeparator) =>
        FormattableString.Invariant(
            $"{(long)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}{millisecondSeparator}{value.Milliseconds:000}");
}
