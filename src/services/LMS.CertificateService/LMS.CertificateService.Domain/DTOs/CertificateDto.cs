using LMS.CertificateService.Domain.Enums;

namespace LMS.CertificateService.Domain.DTOs;

public record CertificateDto(
    Guid Id,
    Guid UserId,
    Guid CourseId,
    string CertificateNumber,
    Guid VerificationCode,
    CertificateStatus Status,
    string? CourseName,
    string? LearnerName,
    DateTimeOffset IssuedAt,
    DateTimeOffset? RevokedAt,
    string? RevocationReason);

public record CertificateVerifyDto(
    string CertificateNumber,
    Guid CourseId,
    DateTimeOffset IssuedAt,
    DateTimeOffset? RevokedAt,
    bool IsValid);
