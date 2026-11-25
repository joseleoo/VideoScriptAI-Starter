using Microsoft.EntityFrameworkCore;
using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Infrastructure.Persistence;

public sealed class VideoScriptDbContext(DbContextOptions<VideoScriptDbContext> options)
    : DbContext(options)
{
    public DbSet<TranscriptionJob> Jobs => Set<TranscriptionJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TranscriptionJob>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.StoredVideoPath).IsRequired();
            entity.Property(x => x.Language).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => x.CreatedAtUtc);
        });
    }
}
