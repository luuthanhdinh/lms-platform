using LMS.CourseService.Domain.Abstractions;
using LMS.CourseService.Domain.Entities;
using LMS.SharedKernel;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LMS.CourseService.Infrastructure.Data;

public class CourseDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public CourseDbContext(DbContextOptions<CourseDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseSection> CourseSections => Set<CourseSection>();
    public DbSet<CourseLesson> CourseLessons => Set<CourseLesson>();
    public DbSet<CoursePrerequisite> CoursePrerequisites => Set<CoursePrerequisite>();
    public DbSet<CourseSnapshot> CourseSnapshots => Set<CourseSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox / inbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<Course>(e =>
        {
            e.ToTable("courses", "courses");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Category).HasMaxLength(80).IsRequired();
            e.Property(x => x.Language).HasMaxLength(10).IsRequired();
            e.Property(x => x.Tags)
                .HasColumnType("text[]")
                .HasDefaultValue(Array.Empty<string>());
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.InstructorId });
            e.HasIndex(x => new { x.TenantId, x.Category });
            e.HasIndex(x => x.Tags).HasMethod("gin");
        });

        modelBuilder.Entity<CourseSection>(e =>
        {
            e.ToTable("course_sections", "courses");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.CourseId, x.Order }).IsUnique();
        });

        modelBuilder.Entity<CourseLesson>(e =>
        {
            e.ToTable("course_lessons", "courses");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.SectionId, x.Order }).IsUnique();
            e.HasIndex(x => x.CourseId);
            e.HasIndex(x => x.ContentItemId);
        });

        modelBuilder.Entity<CoursePrerequisite>(e =>
        {
            e.ToTable("course_prerequisites", "courses");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);
            e.HasIndex(x => new { x.CourseId, x.PrerequisiteCourseId }).IsUnique();
        });

        modelBuilder.Entity<CourseSnapshot>(e =>
        {
            e.ToTable("course_snapshots", "courses");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);
            e.Property(x => x.StructureJson).HasColumnType("jsonb").IsRequired();
            e.HasIndex(x => new { x.CourseId, x.Version }).IsUnique();
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
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
