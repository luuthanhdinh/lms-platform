using LMS.AssessmentService.Domain.Abstractions;
using LMS.AssessmentService.Domain.Entities;
using LMS.SharedKernel;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LMS.AssessmentService.Infrastructure.Data;

public class AssessmentDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AssessmentDbContext(DbContextOptions<AssessmentDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AssessmentAttempt> AssessmentAttempts => Set<AssessmentAttempt>();
    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("assessments");

        // MassTransit outbox / inbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        var optionsSerializerOptions = new JsonSerializerOptions();

        modelBuilder.Entity<Assessment>(e =>
        {
            e.ToTable("assessments", "assessments");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);

            e.Property<uint>("xmin")
                .HasColumnType("xid")
                .HasColumnName("xmin")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            e.HasIndex(x => new { x.TenantId, x.CourseId });
            e.HasIndex(x => new { x.TenantId, x.LessonId })
                .HasFilter("lesson_id IS NOT NULL");

            e.HasMany(a => a.Questions)
                .WithOne(q => q.Assessment)
                .HasForeignKey(q => q.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Question>(e =>
        {
            e.ToTable("questions", "assessments");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);

            e.Property<uint>("xmin")
                .HasColumnType("xid")
                .HasColumnName("xmin")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            e.HasIndex(x => new { x.TenantId, x.AssessmentId, x.Order });

            // Store QuestionOption[] as jsonb
            e.Property(q => q.Options)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<QuestionOption[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<QuestionOption>());
        });

        modelBuilder.Entity<AssessmentAttempt>(e =>
        {
            e.ToTable("assessment_attempts", "assessments");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);

            e.Property<uint>("xmin")
                .HasColumnType("xid")
                .HasColumnName("xmin")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            e.HasIndex(x => new { x.TenantId, x.UserId, x.AssessmentId });
            e.HasIndex(x => new { x.TenantId, x.AssessmentId });

            e.HasMany(a => a.Answers)
                .WithOne()
                .HasForeignKey(ans => ans.AttemptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AttemptAnswer>(e =>
        {
            e.ToTable("attempt_answers", "assessments");
            e.HasQueryFilter(x => x.TenantId == _tenantContext.TenantId);

            e.Property<uint>("xmin")
                .HasColumnType("xid")
                .HasColumnName("xmin")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            e.HasIndex(x => new { x.TenantId, x.AttemptId });
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
