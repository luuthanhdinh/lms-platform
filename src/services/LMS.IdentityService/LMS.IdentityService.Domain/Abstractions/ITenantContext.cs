namespace LMS.IdentityService.Domain.Abstractions;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid UserId { get; }
    string[] Roles { get; }
}
