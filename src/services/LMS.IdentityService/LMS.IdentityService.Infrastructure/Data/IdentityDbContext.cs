using LMS.IdentityService.Domain.Abstractions;
using LMS.IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.IdentityService.Infrastructure.Data;

public class IdentityDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<TenantConfig> TenantConfigs => Set<TenantConfig>();
    public DbSet<UserInvite> UserInvites => Set<UserInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("identity");

        // Global query filters — mandatory per absolute rules
        modelBuilder.Entity<UserProfile>()
            .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<TenantConfig>()
            .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<UserInvite>()
            .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);

        // UserProfile
        modelBuilder.Entity<UserProfile>(e =>
        {
            e.ToTable("user_profiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.KeycloakId).IsRequired();
            e.Property(x => x.Email).IsRequired();
            e.Property(x => x.DisplayName).IsRequired();
            e.Property(x => x.Bio).HasMaxLength(2000);
            e.Property(x => x.Timezone).IsRequired().HasDefaultValue("Asia/Ho_Chi_Minh");
            e.Property(x => x.Language).IsRequired().HasDefaultValue("vi");
            e.Property(x => x.Role).IsRequired();
            e.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
            e.HasIndex(x => new { x.TenantId, x.Id }).HasDatabaseName("IX_user_profiles_TenantId_Id");
            e.HasIndex(x => new { x.TenantId, x.KeycloakId }).IsUnique().HasDatabaseName("UX_UserProfile_Tenant_Keycloak");
            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique().HasDatabaseName("UX_UserProfile_Tenant_Email");
            e.HasIndex(x => new { x.TenantId, x.Role }).HasDatabaseName("IX_UserProfile_Tenant_Role");
        });

        // TenantConfig — Id == TenantId (1:1)
        modelBuilder.Entity<TenantConfig>(e =>
        {
            e.ToTable("tenant_configs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Timezone).IsRequired().HasDefaultValue("Asia/Ho_Chi_Minh");
            e.Property(x => x.AllowedEmailDomains).HasColumnType("text[]");
            e.HasIndex(x => new { x.TenantId, x.Id }).HasDatabaseName("IX_tenant_configs_TenantId_Id");
            e.HasIndex(x => x.TenantId).IsUnique().HasDatabaseName("UX_TenantConfig_TenantId");
        });

        // UserInvite
        modelBuilder.Entity<UserInvite>(e =>
        {
            e.ToTable("user_invites");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).IsRequired();
            e.Property(x => x.Token).IsRequired();
            e.Property(x => x.IsAccepted).IsRequired().HasDefaultValue(false);
            e.HasIndex(x => new { x.TenantId, x.Id }).HasDatabaseName("IX_user_invites_TenantId_Id");
            e.HasIndex(x => x.Token).IsUnique().HasDatabaseName("UX_UserInvite_Token");
            e.HasIndex(x => new { x.TenantId, x.Email })
                .HasFilter("\"IsAccepted\" = false")
                .HasDatabaseName("IX_UserInvite_Tenant_Email_Pending");
        });
    }
}
