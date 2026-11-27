using System.Text;
using System.Text.Json;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class ExtractiveSummaryProvider : ISummaryProvider
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "la", "que", "el", "en", "y", "a", "los", "se", "del", "las", "un", "por", "con", "una",
        "su", "para", "es", "al", "lo", "como", "más", "pero", "sus", "le", "ya", "o", "este", "sí",
        "this", "the", "and", "for", "that", "with", "you", "are", "was", "have", "has", "from", "but"
    };

    public SummaryProvider Provider => SummaryProvider.Extractive;
    public bool IsAvailable => true;

    public Task<string> GenerateAsync(string transcript, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        var sentences = System.Text.RegularExpressions.Regex
            .Split(transcript, @"(?<=[.!?])\s+|\n+")
            .Select(sentence => sentence.Trim())
            .Where(sentence => sentence.Length > 0)
            .ToArray();

        if (sentences.Length == 0)
            throw new InvalidOperationException("La transcripción no contiene texto para resumir.");

        if (sentences.Length <= 3)
            return Task.FromResult(string.Join(" ", sentences));

        var frequencies = sentences
            .SelectMany(SplitWords)
            .Where(word => word.Length > 2 && !StopWords.Contains(word))
            .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var ranked = sentences
            .Select((sentence, index) => new
            {
                Sentence = sentence,
                Index = index,
                Score = SplitWords(sentence)
                    .Where(word => frequencies.ContainsKey(word))
                    .Sum(word => frequencies[word]) / Math.Sqrt(Math.Max(sentence.Length, 1))
            })
            .OrderByDescending(item => item.Score)
            .Take(Math.Clamp((int)Math.Ceiling(sentences.Length * 0.25d), 3, 8))
            .OrderBy(item => item.Index)
            .Select(item => item.Sentence);

        var result = new StringBuilder();
        result.AppendLine("Resumen automático (extractivo):");
        foreach (var sentence in ranked)
            result.AppendLine("- " + sentence);

        return Task.FromResult(result.ToString().Trim());
    }

    private static IEnumerable<string> SplitWords(string value) =>
        System.Text.RegularExpressions.Regex.Matches(value, @"[\p{L}\p{N}]+")
            .Select(match => match.Value);
}
