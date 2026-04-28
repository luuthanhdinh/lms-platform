using LMS.CertificateService.Domain.Entities;
using LMS.CertificateService.Domain.Interfaces;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LMS.CertificateService.Infrastructure.Data;

public class CertificateDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public CertificateDbContext(DbContextOptions<CertificateDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Certificate> Certificates => Set<Certificate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("certificates");

        modelBuilder.Entity<Certificate>(b =>
        {
            b.ToTable("certificates");

            b.HasKey(c => c.Id);

            // Global tenant query filter
            b.HasQueryFilter(c => c.TenantId == _tenantContext.TenantId);

            // Concurrency token via xmin (Postgres row version)
            b.Property<uint>("xmin")
             .HasColumnType("xid")
             .HasColumnName("xmin")
             .ValueGeneratedOnAddOrUpdate()
             .IsConcurrencyToken();

            b.Property(c => c.CertificateNumber).HasMaxLength(64).IsRequired();
            b.Property(c => c.PdfStorageKey).HasColumnType("text");
            b.Property(c => c.CourseName).HasColumnType("text");
            b.Property(c => c.LearnerName).HasColumnType("text");
            b.Property(c => c.RevocationReason).HasColumnType("text");
            b.Property(c => c.Status).HasDefaultValue(Domain.Enums.CertificateStatus.Active);

            // Column names
            b.Property(c => c.Id).HasColumnName("id");
            b.Property(c => c.TenantId).HasColumnName("tenant_id");
            b.Property(c => c.UserId).HasColumnName("user_id");
            b.Property(c => c.CourseId).HasColumnName("course_id");
            b.Property(c => c.CertificateNumber).HasColumnName("certificate_number");
            b.Property(c => c.VerificationCode).HasColumnName("verification_code");
            b.Property(c => c.Status).HasColumnName("status");
            b.Property(c => c.IssuedAt).HasColumnName("issued_at");
            b.Property(c => c.RevokedAt).HasColumnName("revoked_at");
            b.Property(c => c.RevocationReason).HasColumnName("revocation_reason");
            b.Property(c => c.PdfStorageKey).HasColumnName("pdf_storage_key");
            b.Property(c => c.CourseName).HasColumnName("course_name");
            b.Property(c => c.LearnerName).HasColumnName("learner_name");
            b.Property(c => c.CreatedAt).HasColumnName("created_at");
            b.Property(c => c.UpdatedAt).HasColumnName("updated_at");

            // Unique index on CertificateNumber scoped to TenantId
            b.HasIndex(c => new { c.TenantId, c.CertificateNumber })
             .IsUnique()
             .HasDatabaseName("ix_certificates_tenant_certificate_number");

            // Unique index on VerificationCode (globally unique, no tenant prefix)
            b.HasIndex(c => c.VerificationCode)
             .IsUnique()
             .HasDatabaseName("ix_certificates_verification_code");

            // Partial unique index: one active cert per (TenantId, UserId, CourseId) where not revoked
            b.HasIndex(c => new { c.TenantId, c.UserId, c.CourseId })
             .IsUnique()
             .HasFilter("revoked_at IS NULL")
             .HasDatabaseName("ix_certificates_tenant_user_course_active");

            // Index for list-by-user queries
            b.HasIndex(c => new { c.TenantId, c.UserId })
             .HasDatabaseName("ix_certificates_tenant_user");
        });

        // MassTransit outbox/inbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
