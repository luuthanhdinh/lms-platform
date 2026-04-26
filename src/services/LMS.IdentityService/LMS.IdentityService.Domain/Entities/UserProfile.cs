using LMS.SharedKernel;
using LMS.IdentityService.Domain.Enums;

namespace LMS.IdentityService.Domain.Entities;

public class UserProfile : TenantEntity
{
    public string KeycloakId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public string Language { get; set; } = "vi";
    public UserRole Role { get; set; } = UserRole.Student;
    public bool IsActive { get; set; } = true;
}
