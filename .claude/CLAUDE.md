# LMS Platform — Claude Code Context

## Stack
**Backend:** .NET 9 · Aspire · YARP · Keycloak · MassTransit/RabbitMQ
PostgreSQL · Redis · MongoDB (ContentService only) · QuestPDF
**Frontend:** React 19 · TypeScript · Vite · TanStack Query · TanStack Router
Zustand · shadcn/ui · Tailwind CSS · Keycloak-js

## Absolute rules — never break these (backend)
- Every DB entity inherits `TenantEntity` (`Id`, `TenantId`, `CreatedAt`, `UpdatedAt`)
- Global EF Core query filter on `TenantId` in every `DbContext.OnModelCreating`
- No direct HTTP calls between services — use MassTransit events over RabbitMQ only
- JWT validated at YARP gateway only — services trust forwarded headers:
  `X-User-Id`, `X-Tenant-Id`, `X-Roles` — never re-validate JWT downstream
- All event contracts = C# records in `LMS.Contracts` — never define events inline
- Feature flags via `Microsoft.FeatureManagement` — Phase 3+ features default `false`
- `IsFree=false` courses → `409 PAYMENT_REQUIRED` (stub until Phase 2)
- `Assessment.LessonId` is nullable — `null` = course-level exam, non-null = lesson quiz
- PDF certificates use QuestPDF (not iTextSharp, not Puppeteer)
- `ILlmClient` abstraction for all LLM calls — never call Anthropic SDK directly

## Absolute rules — never break these (frontend)
- Never store JWT or refresh token in `localStorage` — use Keycloak-js in-memory only
- Every API call goes through the central `apiClient` in `src/lib/api-client.ts`
- Never fetch directly in a component — always use a TanStack Query `useQuery` hook
- `tenantId` never hardcoded — always read from Keycloak token claims
- All route-level auth guards use `<ProtectedRoute>` wrapper, not ad-hoc checks
- Form state: React Hook Form only — no controlled inputs with raw `useState`
- No inline styles — Tailwind utility classes only

## Project layout
```
src/
  LMS.AppHost/          # Aspire orchestration (registers frontend as npm resource)
  LMS.ServiceDefaults/  # Shared OTEL, health, resilience
  LMS.Contracts/        # MassTransit event records
  LMS.SharedKernel/     # TenantEntity, Result<T>, middleware
  gateway/LMS.Gateway/  # YARP + JWT validation + rate limiting
  services/LMS.{Name}Service/   # one folder per service
  frontend/                     # React app — see docs/frontend.md
    src/
      app/              # TanStack Router route tree
      features/         # feature-sliced: courses/, enrollment/, assessment/, etc.
      lib/              # api-client, keycloak, query-client
      components/ui/    # shadcn/ui base components
tests/
  LMS.IntegrationTests/
  LMS.ContractTests/
  LMS.ArchitectureTests/
docs/                   # read before touching a service
  architecture.md       # AppHost, gateway, multi-tenancy, MassTransit setup
  frontend.md           # React app structure, auth, API client, patterns
  entities.md           # all EF Core entity models + DbContext patterns
  events.md             # all 47 MassTransit event contracts
  services/{name}.md    # per-service endpoints, flows, consumers
  adr/adr-{001-031}.md  # all architecture decisions
```

## Phase 1 services (all shipped)
1. LMS.Gateway · 2. LMS.IdentityService · 3. LMS.CourseService
4. LMS.ContentService · 5. LMS.EnrollmentService · 6. LMS.ProgressService
7. LMS.AssessmentService · 8. LMS.CertificateService · 9. LMS.NotificationWorker
10. React frontend

## Before working on any service
Read `docs/architecture.md` + `docs/services/{name}.md` first.
Read relevant ADRs listed in the service doc.

## Before working on the frontend
Read `docs/frontend.md` first.
