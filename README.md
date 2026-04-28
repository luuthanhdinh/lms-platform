# LMS Platform

Cloud-native Learning Management System — .NET 9 Aspire microservices, React 19, Keycloak SSO.

## Stack

| Layer | Technology |
|---|---|
| Orchestration | .NET Aspire (AppHost) |
| Gateway | YARP + JWT validation |
| Identity | Keycloak |
| Services | .NET 9 Minimal API + EF Core + MassTransit |
| Messaging | RabbitMQ (MassTransit) |
| Databases | PostgreSQL · MongoDB (ContentService) · Redis |
| Certificates | QuestPDF |
| Frontend | React 19 · Vite · TanStack Query/Router · Zustand · shadcn/ui |

## Repository layout

```
src/
  LMS.AppHost/            Aspire orchestration entry point
  LMS.ServiceDefaults/    Shared OTEL, health checks, resilience
  LMS.Contracts/          MassTransit event records (shared contracts)
  LMS.SharedKernel/       TenantEntity, Result<T>, middleware
  gateway/LMS.Gateway/    YARP + JWT + rate limiting
  services/LMS.*/         One folder per microservice
frontend/                 React 19 SPA
tests/
  LMS.IntegrationTests/
  LMS.ContractTests/
  LMS.ArchitectureTests/
docs/
  architecture.md         AppHost, gateway, multi-tenancy, MassTransit
  frontend.md             React app structure, auth, API client patterns
  entities.md             All EF Core entity models + DbContext patterns
  events.md               All MassTransit event contracts
  services/{name}.md      Per-service endpoints and flows
  adr/                    Architecture Decision Records (ADR-001 – ADR-031)
  roadmap/                WBS, sprint roadmap, implementation plan
```

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Aspire spins up all infra containers)
- [Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling): `dotnet workload install aspire`

## Quick start

```bash
# 1. Clone
git clone <repo-url> && cd LMS

# 2. Trust the dev cert
dotnet dev-certs https --trust

# 3. Run everything via Aspire
dotnet run --project src/LMS.AppHost
```

Aspire launches:
- Keycloak, PostgreSQL, MongoDB, Redis, RabbitMQ (Docker)
- All .NET services
- React dev server (Vite on :5173)
- Aspire dashboard at https://localhost:15888

Frontend is available at http://localhost:5173  
API gateway at http://localhost:5000

## Development workflow with Claude Code

This project uses **Claude Code agents** for implementation. Each `/` command maps to a specialized agent pipeline.

### Phase 1 — build order (mandatory)

Services must be implemented in this order due to dependencies:

```
1. LMS.Gateway
2. LMS.IdentityService
3. LMS.CourseService
4. LMS.ContentService
5. LMS.EnrollmentService
6. LMS.ProgressService
7. LMS.AssessmentService
8. LMS.CertificateService
9. LMS.NotificationWorker
10. React frontend
```

### Per-service workflow

```bash
# Step 1 — decompose the work
/plan "implement LMS.Gateway"

# Step 2 — full pipeline: contracts → impl → migration → gateway routes → review
/ship "LMS.Gateway"

# Step 3 — run tests
/test

# Step 4 — review pass (security + code)
/review

# Step 5 — commit
/commit
```

### Kick-off: implement LMS.Gateway (first service)

```bash
# Read the spec before anything else
# docs/architecture.md
# docs/services/identity.md (gateway depends on Keycloak config)

/plan "implement LMS.Gateway: YARP reverse proxy, JWT validation via Keycloak JWKS,
  per-tenant rate limiting with Redis, X-User-Id/X-Tenant-Id/X-Roles header forwarding,
  CORS, health endpoint — per docs/architecture.md and ADR-003"
```

The master agent will emit a `task-graph.json` with parallel-safe tasks.  
Then run `/ship "LMS.Gateway"` to execute the full pipeline.

### Other useful commands

| Command | What it does |
|---|---|
| `/onboard` | Full project walkthrough for a new contributor |
| `/diagram` | Render a Mermaid architecture or flow diagram |
| `/explain <symbol>` | Explain a function, class, or flow |
| `/audit-tenant` | Deep multi-tenancy isolation audit |
| `/migrate <description>` | Generate an EF Core migration |
| `/standup` | Generate a standup update |
| `/catchup` | Bring you up to speed on current branch state |
| `/spec <feature>` | Write a technical spec before implementing |

## Absolute rules (enforced by review agents)

**Backend**
- Every entity inherits `TenantEntity` (`Id`, `TenantId`, `CreatedAt`, `UpdatedAt`)
- Global `TenantId` EF Core query filter in every `DbContext.OnModelCreating`
- No direct HTTP calls between services — MassTransit events only
- JWT validated at gateway only — services trust `X-User-Id`, `X-Tenant-Id`, `X-Roles` headers
- All event contracts are C# records in `LMS.Contracts`
- Feature flags via `Microsoft.FeatureManagement` — Phase 3+ features default `false`
- PDF certificates use QuestPDF only
- All LLM calls go through `ILlmClient` — never call Anthropic SDK directly

**Frontend**
- JWT never in `localStorage` — Keycloak-js in-memory only
- All API calls through `src/lib/api-client.ts`
- No direct fetching in components — always TanStack Query `useQuery`
- `tenantId` always from Keycloak token claims
- Route auth guards via `<ProtectedRoute>` only
- Form state via React Hook Form only
- No inline styles — Tailwind classes only

## Docs

| Doc | When to read |
|---|---|
| [Architecture](docs/architecture.md) | Before touching AppHost, gateway, or cross-cutting concerns |
| [Frontend](docs/frontend.md) | Before any React work |
| [Entities](docs/entities.md) | Before adding/modifying DB models |
| [Events](docs/events.md) | Before adding/modifying MassTransit events |
| [Roadmap](docs/roadmap/wbs-roadmap-plan.md) | Sprint planning and phase scope |
| [ADR index](docs/adr-index.md) | Architecture decisions |

## Phase overview

| Phase | Focus | Sprints |
|---|---|---|
| Phase 1 | Foundation + core learning services | S1–S8 (Q1–Q2) |
| Phase 2 | AI, gamification, payments, analytics | S9–S16 (Q3–Q4) |
| Phase 3 | Enterprise: marketplace, GDPR, white-label | S17–S23 (Q5–Q6) |
| Phase 4 | Innovation: virtual labs, blockchain credentials | S24+ (Q7–Q8) |
