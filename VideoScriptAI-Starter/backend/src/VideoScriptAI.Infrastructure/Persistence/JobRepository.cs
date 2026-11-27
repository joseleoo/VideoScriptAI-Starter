using Microsoft.EntityFrameworkCore;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Domain.Entities;
using VideoScriptAI.Infrastructure.Persistence;

namespace VideoScriptAI.Infrastructure.Persistence;

public sealed class JobRepository(VideoScriptDbContext dbContext) : IJobRepository
{
    public async Task AddAsync(TranscriptionJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await dbContext.Jobs.AddAsync(job, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<TranscriptionJob?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Jobs
            .Include(x => x.Segments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TranscriptionJob>> ListAsync(CancellationToken cancellationToken)
    {
        var jobs = await dbContext.Jobs
            .Include(x => x.Segments)
            .ToListAsync(cancellationToken);

        return jobs.OrderByDescending(x => x.CreatedAtUtc).ToArray();
    }

    public async Task RemoveAsync(TranscriptionJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        dbContext.Jobs.Remove(job);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
