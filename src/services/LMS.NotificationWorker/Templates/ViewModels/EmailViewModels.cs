namespace LMS.NotificationWorker.Templates.ViewModels;

public sealed record WelcomeViewModel(
    string PlatformName,
    string FullName,
    string LoginUrl);

public sealed record AccountDeactivatedViewModel(
    string PlatformName,
    string FullName);

public sealed record CoursePublishedViewModel(
    string CourseName,
    string CourseUrl);

public sealed record CourseArchivedViewModel(
    string CourseName);

public sealed record EnrollmentConfirmedViewModel(
    string CourseName,
    string CourseUrl);

public sealed record EnrollmentCancelledViewModel(
    string CourseName);

public sealed record CourseCompletedViewModel(
    string CourseName,
    string FullName,
    bool CertificateIssued);

public sealed record ContentProcessingCompletedViewModel(
    string ContentTitle,
    string ContentUrl);

public sealed record ContentProcessingFailedViewModel(
    string ContentTitle,
    string Reason);

public sealed record AssessmentResultViewModel(
    string AssessmentName,
    int Score,
    bool Passed,
    string? RetakeUrl);

public sealed record CertificateIssuedViewModel(
    string CourseName,
    string FullName,
    string CertificateNumber,
    string VerificationUrl);
