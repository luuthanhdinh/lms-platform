using LMS.IdentityService.Domain.Abstractions;
using LMS.IdentityService.Domain.Entities;
using LMS.IdentityService.Domain.Enums;
using LMS.IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ITenantContext, MigratorTenantContext>();
builder.AddNpgsqlDataSource("lms-identity");
builder.Services.AddDbContext<IdentityDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

var host = builder.Build();
await host.StartAsync();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

// Run migrations
logger.LogInformation("Applying migrations for IdentityDbContext");
await db.Database.MigrateAsync();
logger.LogInformation("Migrations applied successfully");

// Seed master tenant if no configs exist
var masterTenantId = Guid.Parse(
    builder.Configuration["Seeding:MasterTenantId"] ?? "00000000-0000-0000-0000-000000000001");

var hasTenant = await db.TenantConfigs
    .IgnoreQueryFilters()
    .AnyAsync(t => t.Id == masterTenantId);

if (!hasTenant)
{
    logger.LogInformation("Seeding master tenant {TenantId}", masterTenantId);
    db.TenantConfigs.Add(new TenantConfig
    {
        Id = masterTenantId,
        TenantId = masterTenantId,
        Name = "Master Tenant",
        Timezone = "Asia/Ho_Chi_Minh",
        AllowedEmailDomains = [],
        Plan = TenantPlan.Free,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    });
    await db.SaveChangesAsync();
    logger.LogInformation("Master tenant seeded");
}
else
{
    logger.LogInformation("Master tenant already exists, skipping seed");
}

await host.StopAsync();

// Minimal ITenantContext for migrator — bypasses global filter comparisons
// by using Guid.Empty which will never match real tenant rows
internal sealed class MigratorTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
    public Guid UserId => Guid.Empty;
    public string[] Roles => [];
}
