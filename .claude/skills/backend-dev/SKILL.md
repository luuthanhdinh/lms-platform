---
name: backend-dev
description: >
  LMS .NET 9 backend patterns: Minimal API endpoints, EF Core
  with TenantEntity + global filter, MassTransit publishers/consumers,
  Result<T> error handling, ILlmClient abstraction.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## Endpoint pattern (thin — delegates to service)

```csharp
app.MapPost("/courses", async (
    CreateCourseRequest req,
    ICourseService svc,
    HttpContext ctx) =>
{
    var tenantId = ctx.Request.Headers["X-Tenant-Id"].ToString();
    var userId   = ctx.Request.Headers["X-User-Id"].ToString();
    var result = await svc.CreateAsync(req, tenantId, userId);
    return result.ToHttpResult();
}).RequireAuthorization();
```

## Entity pattern

All entities inherit `TenantEntity` (`Id`, `TenantId`, `CreatedAt`, `UpdatedAt`).
Every `DbContext.OnModelCreating` adds:

```csharp
modelBuilder.Entity<MyEntity>()
    .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
```

## Events

- Define record in `LMS.Contracts` — never inline
- Publish via `IPublishEndpoint`; consume via `IConsumer<TEvent>`
- No direct HTTP between services

## Error handling

Return `Result<T>` from services; throw `DomainException` for invariants.
Never throw raw `Exception`.

## Checklist before finishing

Run: `bash .claude/skills/backend-dev/check-patterns.sh`
Fix all violations before writing `DONE.md`.
