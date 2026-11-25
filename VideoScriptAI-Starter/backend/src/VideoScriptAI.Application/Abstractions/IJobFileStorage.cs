using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.Application.Abstractions;

public interface IJobFileStorage
{
    Task<string> SaveUploadAsync(UploadedVideo video, CancellationToken cancellationToken);
}
