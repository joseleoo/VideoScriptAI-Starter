using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Application.Abstractions;

public interface IJobRepository
{
    Task AddAsync(TranscriptionJob job, CancellationToken cancellationToken);
    Task<TranscriptionJob?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TranscriptionJob>> ListAsync(CancellationToken cancellationToken);
    Task RemoveAsync(TranscriptionJob job, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
