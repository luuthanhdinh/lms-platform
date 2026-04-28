using LMS.ProgressService.Domain.Abstractions;
using LMS.ProgressService.Domain.Entities;
using LMS.SharedKernel;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LMS.ProgressService.Infrastructure.Data;

public class ProgressDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public ProgressDbContext(DbContextOptions<ProgressDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<LessonProgress> LessonProgress => Set<LessonProgress>();
    public DbSet<CourseProgress> CourseProgress => Set<CourseProgress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("progress");

        // MassTransit outbox / inbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<LessonProgress>(e =>
        {
            e.ToTable("lesson_progress", "progress");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);

            // Optimistic concurrency via PostgreSQL system column xmin
            e.Property<uint>("xmin")
                .HasColumnType("xid")
                .HasColumnName("xmin")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            // One progress row per student per lesson
            e.HasIndex(x => new { x.TenantId, x.UserId, x.LessonId })
                .IsUnique();

            // List lessons for a course
            e.HasIndex(x => new { x.TenantId, x.UserId, x.CourseId });
        });

        modelBuilder.Entity<CourseProgress>(e =>
        {
            e.ToTable("course_progress", "progress");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);

            // Optimistic concurrency via PostgreSQL system column xmin
            e.Property<uint>("xmin")
                .HasColumnType("xid")
                .HasColumnName("xmin")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            // One progress row per student per course
            e.HasIndex(x => new { x.TenantId, x.UserId, x.CourseId })
                .IsUnique();

            // List all students for a course (analytics)
            e.HasIndex(x => new { x.TenantId, x.CourseId });
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Guard: never persist an entity with an empty TenantId
        foreach (var entry in ChangeTracker.Entries<TenantEntity>()
            .Where(e => e.State == EntityState.Added && e.Entity.TenantId == Guid.Empty))
        {
            throw new InvalidOperationException(
                $"TenantId must be set before saving {entry.Entity.GetType().Name}");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
