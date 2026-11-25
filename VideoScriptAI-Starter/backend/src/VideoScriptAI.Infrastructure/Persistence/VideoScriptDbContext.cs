using Microsoft.EntityFrameworkCore;
using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Infrastructure.Persistence;

public sealed class VideoScriptDbContext(DbContextOptions<VideoScriptDbContext> options)
    : DbContext(options)
{
    public DbSet<TranscriptionJob> Jobs => Set<TranscriptionJob>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TranscriptionJob>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.StoredVideoPath).IsRequired();
            entity.Property(x => x.Language).HasMaxLength(16).IsRequired();
            entity.Property(x => x.DetectedLanguage).HasMaxLength(16).IsRequired();
            entity.Property(x => x.FullText).IsRequired();
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<TranscriptSegment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Text).IsRequired();
            entity.HasIndex(x => new { x.JobId, x.Position }).IsUnique();
            entity.HasOne<TranscriptionJob>()
                .WithMany(x => x.Segments)
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
