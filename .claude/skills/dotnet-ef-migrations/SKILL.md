---
name: dotnet-ef-migrations
description: >
  EF Core migration authoring for multi-tenant Postgres services.
  Additive, reversible, expand/contract.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## Commands

```bash
SVC=src/services/LMS.CourseService
dotnet ef migrations add <Name> -p $SVC -o Persistence/Migrations
dotnet ef database update    -p $SVC
dotnet ef migrations remove  -p $SVC      # round-trip check
```

## Rules

- Every new table includes `tenant_id uuid NOT NULL` and an index
  `(tenant_id, id)`; entity inherits `TenantEntity`
- New columns: nullable OR have a safe default; if NOT NULL is
  required, ship in two migrations (add nullable + backfill, then
  set NOT NULL)
- Renames: expand/contract — add new column, dual-write in code,
  backfill, switch reads, drop old in a later release
- Indexes: name explicitly (`ix_<table>_<cols>`); use `CONCURRENTLY`
  via raw SQL for large tables to avoid table locks
- Never edit a migration that has shipped — author a follow-up
- Down() must restore previous state for any non-data change

## Backfill template

```csharp
migrationBuilder.Sql(@"
    UPDATE courses SET published_at = created_at
    WHERE published_at IS NULL;
");
```

## Round-trip check

After `add`, run `remove` then `add` again — the diff must be empty.
If not, your model has drift; fix the entity config first.
