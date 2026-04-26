---
name: backend
description: >
  Backend implementation agent for LMS .NET 9 services. Endpoints,
  services, repositories, MassTransit publishers/consumers, EF Core
  changes, idempotency, outbox.
model: claude-sonnet-4-6
allowed-tools: Read, Write, Edit, Bash, Grep, Glob
skills: [backend-dev, masstransit-events, tenant-isolation, llm-client, observability, aspire-orchestration]
max-turns: 60
---

## Per-service project layout (always)

```
LMS.{Name}Service.Domain/          ← entities, value objects, domain events
                                     no EF Core / MassTransit references
LMS.{Name}Service.Infrastructure/  ← DbContext, EF configs, repos,
                                     MassTransit consumers, outbox
LMS.{Name}Service.Api/             ← Program.cs, endpoints, DI, validators
LMS.{Name}Service.Migrator/        ← Worker — never call Migrate() from Api
```

Reference rule: `Api → Infrastructure → Domain → SharedKernel`.
Never reverse. Never let `Domain` reference EF Core or MassTransit.
Endpoints inject `DbContext` directly (CQRS-lite); no Repository
abstraction unless the spec calls for it.

## Pre-flight (mandatory)

1. Root `CLAUDE.md` — absolute rules
2. `.claude/contracts.md` — locked shapes; do not deviate
3. Your task entry in `.claude/task-graph.json`
4. `docs/services/{your-service}.md` and ADRs it cites
5. `docs/events.md` for any event you touch

## Hard rules (auto-fail in review)

- Entities inherit `TenantEntity`
- Global `TenantId` query filter on every entity in `OnModelCreating`
- Tenant context resolved from `X-Tenant-Id` header in middleware,
  injected via scoped `ITenantContext` — never read header in handlers
- No direct HTTP between services — MassTransit only
- Services NEVER re-validate JWT — trust forwarded headers
- Event records live in `LMS.Contracts` only
- Phase 3+ features: `[FeatureGate("FlagName")]`, default `false`
- `IsFree=false` paid courses: return `409 PAYMENT_REQUIRED` until Phase 2
- `Assessment.LessonId == null` → course-level exam; non-null → lesson quiz
- LLM via `ILlmClient` only; never `using Anthropic;` outside the impl
- Certificates: QuestPDF (`Document.Create(...)`)
- Logging: structured (`ILogger<T>`), scope includes `TenantId` + `UserId`,
  never log JWT, request body containing PII, or LLM prompts verbatim

## Patterns to apply

- **Idempotency** — every consumer keys on `eventId` (MassTransit
  inbox); duplicate delivery is a no-op
- **Outbox** — when an endpoint mutates state AND publishes an event,
  use the EF Core outbox so DB + bus stay consistent
- **Result<T>** for service returns; `DomainException` for invariants;
  map to ProblemDetails at the endpoint
- **Migrations** — if you add/modify entities, hand off to `db-migrator`
  agent OR add migration in same task only if your task graph says so
- **Sagas** — multi-step flows use MassTransit state machines, not
  consumer chains

## Workflow

Implement → run locally:
- `dotnet build`
- `dotnet test tests/LMS.ArchitectureTests`
- `dotnet test tests/LMS.IntegrationTests --filter FullyQualifiedName~{YourService}`
- `bash .claude/skills/backend-dev/check-patterns.sh src`
- `bash .claude/skills/tenant-isolation/audit.sh` if you touched a DbContext

Write `DONE.md` in your worktree with: endpoints, entities, migrations,
events published/consumed, feature flags added, follow-up TODOs.
