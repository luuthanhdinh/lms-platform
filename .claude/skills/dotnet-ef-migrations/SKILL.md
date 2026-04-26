---
name: dotnet-ef-migrations
description: >
  EF Core migration authoring for multi-tenant Postgres services
  using the layered project split (Domain / Infrastructure / Api /
  Migrator). Additive, reversible, expand/contract.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## Project layout (all relational services)

```
src/services/LMS.{Name}Service/
├── LMS.{Name}Service.Domain/          ← entities, no EF Core ref
├── LMS.{Name}Service.Infrastructure/  ← DbContext, EF config, Migrations/
├── LMS.{Name}Service.Api/             ← runtime; references Infrastructure
└── LMS.{Name}Service.Migrator/        ← Worker; runs MigrateAsync at boot
```

Reference graph (one direction only):
`Migrator → Infrastructure → Domain → SharedKernel`
`Api → Infrastructure → Domain → SharedKernel`

## Migration commands

Migration project = Infrastructure. Startup project = Migrator
(it has the `IDesignTimeDbContextFactory` / Aspire connection
string wiring). The API is never used as `-s`.

```bash
SVC=src/services/LMS.CourseService
INFRA=$SVC/LMS.CourseService.Infrastructure
MIGR=$SVC/LMS.CourseService.Migrator

dotnet ef migrations add <Name> -p $INFRA -s $MIGR -o Migrations
dotnet ef migrations remove   -p $INFRA -s $MIGR     # round-trip check
dotnet run --project $MIGR                            # apply locally
```

Never run `dotnet ef database update` from the API project; the
Migrator is the only thing that touches DDL.

## Migrator template

```csharp
// LMS.{Name}Service.Migrator/Program.cs
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CourseDbContext>("courses");
builder.Services.AddHostedService<MigrationRunner>();
await builder.Build().RunAsync();

public sealed class MigrationRunner(
    IServiceProvider sp,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationRunner> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CourseDbContext>();
        log.LogInformation("Applying migrations for {Db}", db.Database.GetDbConnection().Database);
        await db.Database.MigrateAsync(ct);
        log.LogInformation("Migrations applied");
        lifetime.StopApplication();   // exit-0 so Aspire/K8s sees success
    }
}
```

## Aspire AppHost wiring

```csharp
var pg       = builder.AddPostgres("pg").AddDatabase("courses");
var migrator = builder.AddProject<Projects.LMS_CourseService_Migrator>("course-migrator")
                      .WithReference(pg).WaitFor(pg);
builder.AddProject<Projects.LMS_CourseService_Api>("course-api")
       .WithReference(pg)
       .WaitForCompletion(migrator);   // API only starts after migrator exits 0
```

## Rules

- Every new table includes `tenant_id uuid NOT NULL` and an index
  `(tenant_id, id)`; entity inherits `TenantEntity` (in Domain)
- EF config (HasQueryFilter, indexes, value converters) lives in
  `Infrastructure/Persistence/Configurations/*.cs` — never in Domain
- New columns: nullable OR have a safe default; if NOT NULL is
  required, ship in two migrations (add nullable + backfill, then
  set NOT NULL)
- Renames: expand/contract — add new column, dual-write in code,
  backfill, switch reads, drop old in a later release
- Indexes: name explicitly (`ix_<table>_<cols>`); use `CONCURRENTLY`
  via raw SQL for large tables to avoid table locks
- Never edit a migration that has shipped — author a follow-up
- `Down()` must restore previous state for any non-data change

## Backfill template

```csharp
migrationBuilder.Sql(@"
    UPDATE courses SET published_at = created_at
    WHERE published_at IS NULL;
");
```

## Round-trip check

After `add`, run `remove` then `add` again — the diff must be empty.
If not, your model has drift; fix the entity config in
`Infrastructure/Persistence/Configurations/` first.
