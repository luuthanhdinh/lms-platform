namespace LMS.AssessmentService.Domain.Abstractions;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid UserId { get; }
    string[] Roles { get; }
}
