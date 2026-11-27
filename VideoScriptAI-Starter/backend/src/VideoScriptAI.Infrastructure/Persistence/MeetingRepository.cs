using Microsoft.EntityFrameworkCore;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Infrastructure.Persistence;

public sealed class MeetingRepository(VideoScriptDbContext dbContext) : IMeetingRepository
{
    public async Task AddAsync(MeetingNote meeting, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        await dbContext.Meetings.AddAsync(meeting, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<MeetingNote?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Meetings
            .Include(x => x.Segments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MeetingNote>> ListAsync(CancellationToken cancellationToken)
    {
        var meetings = await dbContext.Meetings
            .Include(x => x.Segments)
            .ToListAsync(cancellationToken);

        return meetings.OrderByDescending(x => x.CreatedAtUtc).ToArray();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task RemoveAsync(MeetingNote meeting, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        dbContext.Meetings.Remove(meeting);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
