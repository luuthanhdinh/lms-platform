using LMS.CertificateService.Domain.Enums;
using LMS.SharedKernel;

namespace LMS.CertificateService.Domain.Entities;

public class Certificate : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }

    /// <summary>Human-readable, unique within tenant. Format: CERT-{yyyyMM}-{8-char uppercase hex}.</summary>
    public string CertificateNumber { get; set; } = default!;

    /// <summary>Globally unique verification code for the public /verify/{verificationCode} endpoint.</summary>
    public Guid VerificationCode { get; set; }

    public string? PdfStorageKey { get; set; }

    public CertificateStatus Status { get; set; } = CertificateStatus.Active;

    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevocationReason { get; set; }

    /// <summary>Display name of the course; may be null (Phase 1 placeholder).</summary>
    public string? CourseName { get; set; }

    /// <summary>Display name of the learner; may be null (Phase 1 placeholder).</summary>
    public string? LearnerName { get; set; }
}
