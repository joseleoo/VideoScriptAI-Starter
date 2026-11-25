using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Application.Abstractions;

public interface IExportService
{
    Task<ExportFileDto> ExportAsync(
        TranscriptionJob job,
        string format,
        CancellationToken cancellationToken);
}
