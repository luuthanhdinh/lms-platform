using LMS.SharedKernel;
using LMS.IdentityService.Domain.Enums;

namespace LMS.IdentityService.Domain.Entities;

public class UserInvite : TenantEntity
{
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsAccepted { get; set; }
}
