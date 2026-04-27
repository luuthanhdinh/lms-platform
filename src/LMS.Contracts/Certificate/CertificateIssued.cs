namespace LMS.Contracts.Certificate;

/// <summary>
/// Published by CertificateService when a certificate is successfully issued.
/// Consumers: NotificationWorker (send certificate email with PDF link).
/// </summary>
public sealed record CertificateIssued(
    /// <summary>Unique event identifier for inbox deduplication at the consumer.</summary>
    Guid EventId,

    /// <summary>Tenant that owns the certificate, user, and course. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Surrogate key of the issued certificate row.</summary>
    Guid CertificateId,

    /// <summary>Surrogate key of the student who received the certificate.</summary>
    Guid UserId,

    /// <summary>Surrogate key of the course for which the certificate was issued.</summary>
    Guid CourseId,

    /// <summary>Human-readable certificate number. Format: CERT-{yyyy}-{8-char Crockford base32 of CertificateId}.</summary>
    string CertificateNumber,

    /// <summary>
    /// Globally unique verification code for the public /verify/{verificationCode} endpoint.
    /// Carried in the event so NotificationWorker can render the verification URL without a callback.
    /// </summary>
    Guid VerificationCode,

    /// <summary>UTC instant the certificate was issued and persisted.</summary>
    DateTimeOffset IssuedAt,

    /// <summary>UTC instant this event was raised (equals IssuedAt in the normal flow).</summary>
    DateTimeOffset OccurredAt);
