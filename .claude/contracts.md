# Contracts (locked — do not deviate)
_Hash: <sha256 of this file at lock time, filled by orchestrator>_

Feature: **LMS.IdentityService** — User profile, tenant configuration, role assignment, and invite flow. Second Phase 1 service.

Phase: **1**  ·  Service: `src/services/LMS.IdentityService/`  ·  Trust boundary: **reads `X-User-Id`/`X-Tenant-Id`/`X-Roles` from gateway only — never validates JWT**.

Port (logical): `identity` (Aspire service discovery; YARP cluster `identity` → `http://identity` already locked in `src/gateway/LMS.Gateway/appsettings.json`).
DB: `lms_identity` (PostgreSQL).
ADRs: ADR-001 (Keycloak realm strategy), ADR-002 (MFA enforcement).

---

## Trust model (locked)

Service trusts only the following headers, set by `LMS.Gateway`:

| Header        | Format       | Required for non-anonymous endpoints |
|---------------|--------------|--------------------------------------|
| `X-User-Id`   | UUID         | yes                                  |
| `X-Tenant-Id` | UUID         | yes                                  |
| `X-Roles`     | comma list   | yes (e.g. `student`, `org-admin`)    |
| `X-Correlation-Id` | GUID    | always (forwarded by gateway)        |

- **No JWT validation in this service.** The `Authorization` header is stripped by YARP and must NOT be re-honoured.
- Missing/malformed `X-User-Id` or `X-Tenant-Id` on protected endpoints → `401 { "code": "UNAUTHENTICATED" }`.
- Role gate failures → `403 { "code": "FORBIDDEN" }`.
- All EF queries pass through the global `TenantId` filter; the resolved tenant comes from `X-Tenant-Id` via a scoped `ITenantContext` provider.

## Roles (locked enum values, lowercase wire form)

| Wire value    | UserRole enum    |
|---------------|------------------|
| `student`     | `Student`        |
| `instructor`  | `Instructor`     |
| `admin`       | `Admin`          |
| `org-admin`   | `OrgAdmin`       |

Authorization rules:
- `GET /api/identity/profile/me`, `PUT /api/identity/profile/me`, `POST /api/identity/profile` → any authenticated role.
- `GET /api/identity/users`, `POST /api/identity/users/invite`, `PATCH /api/identity/users/{id}/role`, `PATCH /api/identity/users/{id}/deactivate`, `PUT /api/identity/tenant` → `org-admin` OR `admin`.
- `GET /api/identity/tenant` → any authenticated role.

---

## HTTP endpoints (locked)

All endpoints prefixed `/api/identity`. All return `application/json`. Errors use `{ "code": "<UPPER_SNAKE>", "message": "...", "details"?: {...} }`. Validation errors use RFC7807 `ValidationProblem`.

### Profile

```
GET  /api/identity/profile/me                  → 200 UserProfileDto | 404 PROFILE_NOT_FOUND
PUT  /api/identity/profile/me                  ← UpdateProfileRequest      → 200 UserProfileDto | 400 ValidationProblem | 404 PROFILE_NOT_FOUND
POST /api/identity/profile                     ← UpsertProfileRequest      → 200 UserProfileDto (existing) | 201 UserProfileDto (created) | 400 ValidationProblem
```

`POST /api/identity/profile` is **idempotent**. Called by the frontend on first login (gateway never originates calls — see `.claude/contracts.md` for gateway). Matches on `(TenantId, KeycloakId)`. On create, publishes `UserRegistered`. On update, no event.

### User administration (`org-admin` | `admin`)

```
GET   /api/identity/users                      ?role=&active=&search=&page=&pageSize=
                                               → 200 PagedResult<UserSummaryDto>
POST  /api/identity/users/invite               ← InviteUserRequest   → 201 UserInviteDto | 400 ValidationProblem | 409 INVITE_DUPLICATE
PATCH /api/identity/users/{id}/role            ← { "role": "instructor" }
                                               → 200 UserProfileDto | 400 ValidationProblem | 404 USER_NOT_FOUND
PATCH /api/identity/users/{id}/deactivate      → 204 | 404 USER_NOT_FOUND | 409 ALREADY_DEACTIVATED
```

### Tenant configuration

```
GET /api/identity/tenant                       → 200 TenantConfigDto | 404 TENANT_NOT_FOUND
PUT /api/identity/tenant                       ← UpdateTenantRequest → 200 TenantConfigDto | 400 ValidationProblem
```

### DTO shapes (locked)

```csharp
public record UserProfileDto(
    Guid Id, Guid TenantId, string KeycloakId, string Email, string DisplayName,
    string? AvatarUrl, string? Bio, string Timezone, string Language,
    string Role, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record UserSummaryDto(
    Guid Id, string Email, string DisplayName, string Role, bool IsActive,
    DateTimeOffset CreatedAt);

public record UpsertProfileRequest(
    string KeycloakId, string Email, string DisplayName,
    string? AvatarUrl, string? Timezone, string? Language);

public record UpdateProfileRequest(
    string DisplayName, string? AvatarUrl, string? Bio,
    string? Timezone, string? Language);

public record InviteUserRequest(string Email, string Role);

public record UserInviteDto(
    Guid Id, string Email, string Role, string Token,
    DateTimeOffset ExpiresAt, bool IsAccepted, DateTimeOffset CreatedAt);

public record TenantConfigDto(
    Guid Id, string Name, string? LogoUrl, string Timezone,
    string[] AllowedEmailDomains, string Plan,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record UpdateTenantRequest(
    string Name, string? LogoUrl, string? Timezone, string[]? AllowedEmailDomains);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
```

Validation:
- `Email` — RFC5322 + (when tenant has `AllowedEmailDomains`) domain whitelist enforced on `InviteUserRequest`.
- `DisplayName` — 1–120 chars.
- `Timezone` — IANA TZ id (`TimeZoneInfo.TryConvertIanaIdToWindowsId`-validated).
- `Language` — ISO 639-1 (`vi`, `en`, …).
- `Role` (incoming string) — must map to `UserRole` enum (case-insensitive, kebab → enum).

---

## EF Core entities (locked)

All entities inherit `TenantEntity` (`Id: Guid`, `TenantId: Guid`, `CreatedAt: DateTimeOffset`, `UpdatedAt: DateTimeOffset`). DbContext: `IdentityDbContext` in `LMS.IdentityService.Infrastructure`. Schema: `identity`. Global query filter on `TenantId == _tenantContext.TenantId` for **every** entity.

### `user_profiles`

| Column        | Type                | Notes                                    |
|---------------|---------------------|------------------------------------------|
| Id            | uuid (PK)           |                                          |
| TenantId      | uuid (idx, filter)  |                                          |
| KeycloakId    | text (NOT NULL)     | unique within tenant: `(TenantId, KeycloakId)` |
| Email         | citext / text lower | unique within tenant: `(TenantId, Email)`|
| DisplayName   | text                |                                          |
| AvatarUrl     | text? nullable      |                                          |
| Bio           | text? nullable      | max 2000 chars                           |
| Timezone      | text NOT NULL       | default `Asia/Ho_Chi_Minh`               |
| Language      | text NOT NULL       | default `vi`                             |
| Role          | int (UserRole)      | default `Student`                        |
| IsActive      | bool NOT NULL       | default `true`                           |
| CreatedAt/UpdatedAt | timestamptz   |                                          |

Indexes:
- `UX_UserProfile_Tenant_Keycloak` UNIQUE on `(TenantId, KeycloakId)`
- `UX_UserProfile_Tenant_Email` UNIQUE on `(TenantId, Email)`
- `IX_UserProfile_Tenant_Role` on `(TenantId, Role)`

### `tenant_configs`

| Column              | Type           | Notes                                       |
|---------------------|----------------|---------------------------------------------|
| Id                  | uuid (PK)      | equals TenantId (1:1 with tenant)           |
| TenantId            | uuid           | == Id (filter still applies)                |
| Name                | text NOT NULL  |                                             |
| LogoUrl             | text? nullable |                                             |
| Timezone            | text NOT NULL  | default `Asia/Ho_Chi_Minh`                  |
| AllowedEmailDomains | text[]         | default `{}`                                |
| Plan                | int (TenantPlan) | default `Free`                            |
| CreatedAt/UpdatedAt | timestamptz    |                                             |

Indexes:
- `UX_TenantConfig_TenantId` UNIQUE on `(TenantId)`

### `user_invites`

| Column       | Type           | Notes                                       |
|--------------|----------------|---------------------------------------------|
| Id           | uuid (PK)      |                                             |
| TenantId     | uuid (filter)  |                                             |
| Email        | text NOT NULL  |                                             |
| Role         | int (UserRole) |                                             |
| Token        | text NOT NULL  | 32-hex `Guid.NewGuid().ToString("N")`       |
| ExpiresAt    | timestamptz    | default `now() + 7 days`                    |
| IsAccepted   | bool NOT NULL  | default `false`                             |
| CreatedAt/UpdatedAt | timestamptz |                                            |

Indexes:
- `UX_UserInvite_Token` UNIQUE on `(Token)`
- `IX_UserInvite_Tenant_Email_Pending` on `(TenantId, Email)` `WHERE IsAccepted = false`

### Migration

- Initial migration filename: `20260427000001_InitialIdentity.cs` in `LMS.IdentityService.Migrator/Migrations/`.
- Migration is run by the Aspire-hosted `LMS.IdentityService.Migrator` job; AppHost gates `LMS.IdentityService.Api` on `WaitForCompletion(migrator)`.

---

## MassTransit events (locked — defined in `LMS.Contracts`)

These records ALREADY exist (by docs/events.md spec) — the events-architect task is to **add** them to `LMS.Contracts` if not yet present, otherwise verify shape match. No re-definition inside the service.

```csharp
public record UserRegistered(
    Guid UserId, Guid TenantId, string Email,
    string DisplayName, DateTimeOffset OccurredAt);

public record UserDeactivated(
    Guid UserId, Guid TenantId, Guid DeactivatedBy,
    DateTimeOffset OccurredAt);
```

**Published by IdentityService:**
- `UserRegistered` — on first successful upsert (insert path of `POST /api/identity/profile`).
- `UserDeactivated` — on `PATCH /api/identity/users/{id}/deactivate` success.

**Consumed by IdentityService:** none in Phase 1.

Publishing rules:
- Use `IPublishEndpoint`. Event publish happens **inside the same EF transaction** via the MassTransit transactional outbox (added in this service per `masstransit-events` skill).
- `OccurredAt = DateTimeOffset.UtcNow` at publish time.
- Idempotency: re-running `POST /api/identity/profile` for an existing `(TenantId, KeycloakId)` MUST NOT republish `UserRegistered`.

---

## YARP gateway route (already locked — verification only)

Route id `identity` → `/api/identity/{**rest}` → cluster `identity` → `http://identity`. Already present in `src/gateway/LMS.Gateway/appsettings.json`. **No gateway changes required** for this feature. The `gateway-ops` task ONLY adds the AppHost `WithReference(identity)` wiring.

---

## Aspire AppHost wiring (locked surface)

In `src/LMS.AppHost/Program.cs`:

```
var identityDb = postgres.AddDatabase("lms_identity");
var identityMigrator = builder.AddProject<Projects.LMS_IdentityService_Migrator>("identity-migrator")
    .WithReference(identityDb).WaitFor(identityDb);
var identity = builder.AddProject<Projects.LMS_IdentityService_Api>("identity")
    .WithReference(identityDb).WithReference(rabbit).WithReference(redis)
    .WaitForCompletion(identityMigrator);
gateway.WithReference(identity);
```

(Resource names `identity-migrator` and `identity` are locked because YARP cluster destination is `http://identity`.)

---

## Feature flags

**None introduced.** All endpoints in this service are Phase 1 / always-on.

---

## Frontend impact

Out of scope for this feature plan (frontend hooks for profile/me will be planned with the React frontend feature). DTO shapes above are the contract the frontend will bind to.

---

## Cross-service flows

- `UserRegistered` → consumed by `NotificationWorker` (welcome email) per `docs/events.md`.
- `UserDeactivated` → consumed by `EnrollmentService` (suspend active enrollments) per `docs/events.md`.
- Neither consumer is implemented in this feature; only the publish side and contract shape are locked here.

---

## Decisions (locked — approved by product owner 2026-04-27)

1. **Tenant bootstrap** — On application startup (Migrator run), if no `TenantConfig` row exists, seed a **master tenant** with a fixed well-known `TenantId` (config key `Seeding:MasterTenantId`, default `00000000-0000-0000-0000-000000000001`). This master tenant is the default org. Subsequent tenant rows are created via an admin operation (out of scope Phase 1). First user upsert into a tenant that lacks a `TenantConfig` still returns `409 TENANT_NOT_PROVISIONED` (master tenant is pre-seeded so day-1 logins always succeed).

2. **Invite acceptance** — No dedicated `/accept` endpoint in Phase 1. `POST /api/identity/profile` upsert automatically marks the most-recent pending `UserInvite` for `(TenantId, Email)` as `IsAccepted = true` if found. Documented behaviour, not a bug.

3. **Service layout** — 4-project layout at `src/services/LMS.IdentityService/` confirmed.
