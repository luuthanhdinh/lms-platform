using LMS.SharedKernel;
using LMS.IdentityService.Domain.Enums;

namespace LMS.IdentityService.Domain.Entities;

public class TenantConfig : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public string[] AllowedEmailDomains { get; set; } = [];
    public TenantPlan Plan { get; set; } = TenantPlan.Free;
}
