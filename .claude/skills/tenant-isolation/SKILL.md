---
name: tenant-isolation
description: >
  Multi-tenant safety patterns + automated leak audit. Apply on
  any DbContext, query, or background job change.
allowed-tools: Read, Write, Edit, Bash, Grep, Glob
user-invocable: false
---

## Invariants

- `TenantId` resolved from `ITenantContext` (scoped, populated by
  `TenantMiddleware` from `X-Tenant-Id`)
- Global query filter on EVERY entity:
  ```csharp
  modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenant.Id);
  ```
- Insert path sets `TenantId` in `SaveChangesInterceptor` — handlers
  never set it manually (prevents copy-paste mistakes)
- Background jobs (MassTransit consumers, Hangfire, etc.) build their
  own scope with the `TenantId` from the message envelope before
  resolving the DbContext
- Cross-tenant aggregations (admin reports) use a dedicated
  `AdminDbContext` that intentionally bypasses the filter, behind
  `[Authorize(Policy = "PlatformAdmin")]`

## Audit checklist

Run `bash .claude/skills/tenant-isolation/audit.sh src` and fix all
findings. Common leaks to look for manually:

- `IgnoreQueryFilters()` calls without justification comment
- Raw SQL (`FromSqlRaw`, `ExecuteSqlRaw`) without `WHERE tenant_id`
- DTO mappers that copy `TenantId` from request body (must come from
  `ITenantContext`)
- Cache keys that omit `TenantId` (Redis: `t:{tenantId}:...`)
- File paths / blob keys missing tenant prefix

## Test pattern

For every endpoint, write a paired test:
- Tenant A creates resource → Tenant B `GET` returns 404
- Tenant B `PUT/DELETE` returns 404 (not 403, to avoid existence leak)
