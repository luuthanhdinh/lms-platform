using LMS.CertificateService.Domain.Entities;
using LMS.CertificateService.Domain.Enums;
using LMS.CertificateService.Domain.Interfaces;
using LMS.CertificateService.Domain.Repositories;
using LMS.CertificateService.Infrastructure.Data;
using LMS.Contracts.Certificate;
using LMS.Contracts.Progress;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace LMS.CertificateService.Infrastructure.Consumers;

public sealed class CourseCompletedConsumer : IConsumer<CourseCompleted>
{
    private readonly ICertificateRepository _repo;
    private readonly ICertificatePdfGenerator _pdfGenerator;
    private readonly ICertificateStorageService _storage;
    private readonly CertificateDbContext _db;
    private readonly ILogger<CourseCompletedConsumer> _logger;

    public CourseCompletedConsumer(
        ICertificateRepository repo,
        ICertificatePdfGenerator pdfGenerator,
        ICertificateStorageService storage,
        CertificateDbContext db,
        ILogger<CourseCompletedConsumer> logger)
    {
        _repo = repo;
        _pdfGenerator = pdfGenerator;
        _storage = storage;
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CourseCompleted> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = msg.TenantId,
            ["UserId"] = msg.UserId,
            ["CourseId"] = msg.CourseId
        });

        // Consumer scope: HeaderTenantContext returns Guid.Empty (no HttpContext).
        // All queries must use IgnoreQueryFilters() + explicit TenantId from message.

        // Idempotency: check for existing active certificate
        var existing = await _repo.FindByUserCourseAsync(msg.TenantId, msg.UserId, msg.CourseId, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Certificate already exists for UserId={UserId} CourseId={CourseId}, skipping", msg.UserId, msg.CourseId);
            return;
        }

        // Deterministic certId derived from (TenantId, UserId, CourseId) — prevents orphaned blobs on retry
        var certId = DeterministicGuid(msg.TenantId, msg.UserId, msg.CourseId);
        var now = DateTimeOffset.UtcNow;

        var cert = new Certificate
        {
            Id = certId,
            TenantId = msg.TenantId,
            UserId = msg.UserId,
            CourseId = msg.CourseId,
            CertificateNumber = $"CERT-{now:yyyyMM}-{certId.ToString("N")[..8].ToUpperInvariant()}",
            VerificationCode = Guid.NewGuid(),
            Status = CertificateStatus.Active,
            IssuedAt = now,
            CourseName = msg.CourseName,
            LearnerName = msg.LearnerName,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Generate PDF
        var pdfBytes = await _pdfGenerator.GenerateAsync(cert, ct);

        // Upload to blob storage
        var blobKey = $"certificates/{cert.TenantId}/{cert.Id}.pdf";
        using var stream = new MemoryStream(pdfBytes);
        await _storage.UploadAsync(blobKey, stream, ct);
        cert.PdfStorageKey = blobKey;

        // Stage cert insert
        await _repo.AddAsync(cert, ct);

        // Publish event via outbox BEFORE SaveChangesAsync for atomicity
        await context.Publish(new CertificateIssued(
            EventId: Guid.NewGuid(),
            TenantId: cert.TenantId,
            CertificateId: cert.Id,
            UserId: cert.UserId,
            CourseId: cert.CourseId,
            CertificateNumber: cert.CertificateNumber,
            VerificationCode: cert.VerificationCode,
            IssuedAt: cert.IssuedAt,
            OccurredAt: now), ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Certificate issued: {CertificateNumber} for UserId={UserId} CourseId={CourseId}",
            cert.CertificateNumber, cert.UserId, cert.CourseId);
    }

    private static Guid DeterministicGuid(Guid tenantId, Guid userId, Guid courseId)
    {
        // Version 5 UUID (SHA-1 based) — namespace = DNS namespace UUID
        var ns = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
        var name = $"{tenantId}:{userId}:{courseId}";
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        var nsBytes = ns.ToByteArray();
        // Swap to big-endian for RFC 4122
        SwapBytes(nsBytes, 0, 3); SwapBytes(nsBytes, 1, 2);
        SwapBytes(nsBytes, 4, 5); SwapBytes(nsBytes, 6, 7);
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var hash = sha1.ComputeHash([.. nsBytes, .. nameBytes]);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // version 5
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // variant RFC 4122
        var guidBytes = hash[..16];
        // Swap back to little-endian for .NET Guid
        SwapBytes(guidBytes, 0, 3); SwapBytes(guidBytes, 1, 2);
        SwapBytes(guidBytes, 4, 5); SwapBytes(guidBytes, 6, 7);
        return new Guid(guidBytes);
    }

    private static void SwapBytes(byte[] b, int i, int j) => (b[i], b[j]) = (b[j], b[i]);
}
