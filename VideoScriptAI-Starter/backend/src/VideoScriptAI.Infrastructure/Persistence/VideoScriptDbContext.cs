using Microsoft.EntityFrameworkCore;
using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Infrastructure.Persistence;

public sealed class VideoScriptDbContext(DbContextOptions<VideoScriptDbContext> options)
    : DbContext(options)
{
    public DbSet<TranscriptionJob> Jobs => Set<TranscriptionJob>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();
    public DbSet<MeetingNote> Meetings => Set<MeetingNote>();
    public DbSet<MeetingSegment> MeetingSegments => Set<MeetingSegment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TranscriptionJob>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.StoredVideoPath).IsRequired();
            entity.Property(x => x.SourceUrl).HasMaxLength(2048);
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

        modelBuilder.Entity<MeetingNote>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Language).HasMaxLength(16).IsRequired();
            entity.Property(x => x.DetectedLanguage).HasMaxLength(16).IsRequired();
            entity.Property(x => x.AudioPath).HasMaxLength(1024);
            entity.Property(x => x.FullText).IsRequired();
            entity.Property(x => x.Summary).IsRequired();
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<MeetingSegment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Text).IsRequired();
            entity.Property(x => x.Speaker).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.MeetingId, x.Position }).IsUnique();
            entity.HasOne<MeetingNote>()
                .WithMany(x => x.Segments)
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
