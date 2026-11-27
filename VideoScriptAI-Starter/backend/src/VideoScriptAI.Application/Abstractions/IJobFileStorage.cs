using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.Application.Abstractions;

public interface IJobFileStorage
{
    Task<string> SaveUploadAsync(UploadedVideo video, CancellationToken cancellationToken);
    Task<string> SaveUrlAsync(VideoUrl videoUrl, CancellationToken cancellationToken);
    Task DeleteAsync(string path, CancellationToken cancellationToken);
}
