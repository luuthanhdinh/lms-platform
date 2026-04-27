using LMS.EnrollmentService.Domain.Abstractions;
using LMS.EnrollmentService.Domain.Entities;
using LMS.EnrollmentService.Domain.Enums;
using LMS.SharedKernel;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LMS.EnrollmentService.Infrastructure.Data;

public class EnrollmentDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public EnrollmentDbContext(DbContextOptions<EnrollmentDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("enrollments");

        // MassTransit outbox / inbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<Enrollment>(e =>
        {
            e.ToTable("enrollments", "enrollments");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);

            // Optimistic concurrency via PostgreSQL system column xmin
            e.Property<uint>("xmin")
                .HasColumnType("xid")
                .HasColumnName("xmin")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            // Only one active enrollment per (tenant, user, course) at a time
            e.HasIndex(x => new { x.TenantId, x.UserId, x.CourseId })
                .IsUnique()
                .HasFilter("status = 0"); // Active = 0

            e.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.CourseId, x.Status });
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
