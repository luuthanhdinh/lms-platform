Run a deep tenant-isolation audit across the codebase.

1. Run `bash .claude/skills/tenant-isolation/audit.sh src` — collect findings.
2. For each service's DbContext, verify a `HasQueryFilter` is set on
   every entity registered via `modelBuilder.Entity<T>()`.
3. Run architecture tests: `dotnet test tests/LMS.ArchitectureTests`.
4. Spot-check 3 random endpoints per service: trace from the route
   through service → repository → DbContext, confirm `TenantId` is
   never set from the request body and is always filtered on read.
5. Produce `.claude/tenant-audit.md` with: findings, severity
   (BLOCKER/WARN/INFO), file:line, suggested fix.
6. Print a one-screen summary.
