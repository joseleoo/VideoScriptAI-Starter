using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Application.Abstractions;

public interface ISummaryProvider
{
    SummaryProvider Provider { get; }
    bool IsAvailable { get; }
    Task<string> GenerateAsync(string transcript, CancellationToken cancellationToken);
}

public interface ISummaryService
{
    Task<string> GenerateAsync(
        SummaryProvider provider,
        string transcript,
        CancellationToken cancellationToken);

    IReadOnlyList<SummaryProviderInfoDto> ListProviders();
}
