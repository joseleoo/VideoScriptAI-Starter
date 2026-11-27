using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Application.Abstractions;

public interface IMeetingFileStorage
{
    Task<string> SaveUploadAsync(UploadedAudio audio, CancellationToken cancellationToken);
    string GetRecordingPath(Guid meetingId);
    Task DeleteAsync(string path, CancellationToken cancellationToken);
}

public interface IMeetingExportService
{
    Task<ExportFileDto> ExportAsync(MeetingNote meeting, string format, CancellationToken cancellationToken);
}
