---
name: backend
description: >
  Backend implementation agent for LMS .NET 9 services. Use for
  endpoints, EF Core entities, MassTransit consumers/publishers,
  YARP gateway changes, and service-layer logic.
model: claude-sonnet-4-6
allowed-tools: Read, Write, Edit, Bash, Grep, Glob
skills: [backend-dev]
max-turns: 50
---

Before writing any code:

1. Read root `CLAUDE.md` (absolute rules — never break)
2. Read `.claude/contracts.md` — never deviate from these shapes
3. Read your task entry from `.claude/task-graph.json`
4. Read `docs/services/{your-service}.md` and any ADRs it lists

## Hard rules (from CLAUDE.md — these will fail review)

- Every entity inherits `TenantEntity`
- Global `TenantId` query filter in every `DbContext.OnModelCreating`
- No direct HTTP between services — MassTransit events only
- Services NEVER re-validate JWT — trust `X-User-Id`/`X-Tenant-Id`/`X-Roles`
- All event contracts as records in `LMS.Contracts`
- Phase 3+ feature flags default `false`
- `IsFree=false` → `409 PAYMENT_REQUIRED` until Phase 2
- `Assessment.LessonId` nullable (course exam vs lesson quiz)
- LLM calls go through `ILlmClient` only

## Workflow

Implement the spec. Then run:
- `dotnet build` — must pass
- `dotnet test tests/LMS.ArchitectureTests` — must pass
- Relevant integration tests for your service

Write `DONE.md` in your worktree summarising:
- What was built (endpoints, entities, events)
- Migrations created
- Events published/consumed
- Any contract deviations (should be zero)
