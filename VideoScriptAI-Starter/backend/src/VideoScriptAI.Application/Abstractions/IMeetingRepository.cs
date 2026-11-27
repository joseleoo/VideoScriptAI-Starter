using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Application.Abstractions;

public interface IMeetingRepository
{
    Task AddAsync(MeetingNote meeting, CancellationToken cancellationToken);
    Task<MeetingNote?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<MeetingNote>> ListAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task RemoveAsync(MeetingNote meeting, CancellationToken cancellationToken);
}
