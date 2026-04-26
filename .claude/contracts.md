# Contracts (locked — do not deviate)
_Hash: <sha256 of this file at lock time, filled by orchestrator>_

Feature: **LMS.Gateway** — YARP reverse proxy, JWT validation (Keycloak JWKS),
per-tenant rate limiting (Redis), header forwarding, CORS, health endpoints.

Phase: **1**  ·  Service: `src/gateway/LMS.Gateway`  ·  Trust boundary: **gateway is the ONLY JWT verifier**.

---

## Forwarded headers (locked — every downstream service reads these)

| Header        | Source claim                       | Format         | Required |
|---------------|------------------------------------|----------------|----------|
| `X-User-Id`   | `sub`                              | UUID string    | yes when authenticated |
| `X-Tenant-Id` | `tenant_id` (custom Keycloak claim)| UUID string    | yes when authenticated |
| `X-Roles`     | `realm_access.roles` (mapped to `ClaimTypes.Role`) | Comma-separated, no spaces (e.g. `student,instructor`) | yes when authenticated |
| `X-Correlation-Id` | generated if absent           | GUID string    | always |

Rules:
- Gateway **strips** any inbound `X-User-Id`, `X-Tenant-Id`, `X-Roles`, `Authorization` header from the **outbound** request, then re-adds the trusted versions. Inbound forgeries must not survive.
- Anonymous routes (only `verify`) forward neither auth headers nor correlation only.
- Header names are **case-insensitive on read** but emitted exactly as above.

## Auth policy (locked)

- Scheme: `JwtBearerDefaults.AuthenticationScheme` (single).
- Authority: `Keycloak:Authority` (e.g. `http://keycloak:8080/realms/lms`).
- Audience: `Keycloak:Audience` = `lms-api`.
- `RequireHttpsMetadata` = `false` in Development, `true` otherwise.
- JWKS auto-fetched from `{Authority}/protocol/openid-connect/certs`; cached by `Microsoft.IdentityModel`.
- Default authorization policy: `RequireAuthenticatedUser`. Routes opt out via `AuthorizationPolicy: "anonymous"` in YARP config.
- Roles claim mapped to `ClaimTypes.Role` via `TokenValidationParameters.RoleClaimType = "realm_access.roles"` (handled by `JwtBearerEvents.OnTokenValidated` flattening `realm_access.roles[]`).
- 401 body: `{ "code": "UNAUTHENTICATED", "message": "..." }`.
- 403 body: `{ "code": "FORBIDDEN", "message": "..." }`.

## YARP routes (locked — Phase 1)

All routes match `/api/{prefix}/{**rest}` and proxy to a logical Aspire-discovered cluster `http://{name}`.

| Route id     | Path pattern                       | ClusterId    | Auth        |
|--------------|------------------------------------|--------------|-------------|
| `identity`   | `/api/identity/{**rest}`           | `identity`   | required    |
| `courses`    | `/api/courses/{**rest}`            | `courses`    | required    |
| `content`    | `/api/content/{**rest}`            | `content`    | required    |
| `enrollment` | `/api/enrollments/{**rest}`        | `enrollment` | required    |
| `progress`   | `/api/progress/{**rest}`           | `progress`   | required    |
| `assessment` | `/api/assessments/{**rest}`        | `assessment` | required    |
| `certificate`| `/api/certificates/{**rest}`       | `certificate`| required    |
| `verify`     | `/verify/{**rest}`                 | `certificate`| **anonymous** |

Clusters (logical names; resolved via Aspire service discovery):
`identity`, `courses`, `content`, `enrollment`, `progress`, `assessment`, `certificate`.

Notes:
- Path is **forwarded as-is** to the destination (no path strip). Downstream services own `/api/{prefix}/...` routes.
- Health, rate-limit, and auth metadata live on routes, not clusters.

## Rate-limit policy (locked)

- Storage: Redis (`StackExchange.Redis` via Aspire `WithReference(redis)`).
- Algorithm: `FixedWindowLimiter` over Redis (custom `IDistributedRateLimiter` shim or `RedisRateLimiting` package — implementer chooses; key & limits are locked).
- Window: **60 seconds**.
- Default per-tenant limit: **600 req/min** (configurable via `RateLimit:PerTenantPerMinute`).
- Anonymous (per-IP) limit on `/verify/{**rest}` and `/health*`: **60 req/min** per remote IP (`RateLimit:AnonymousPerMinute`).
- Partition keys:
  - Authenticated: `tenant:{X-Tenant-Id}` (after JWT validation).
  - Anonymous: `ip:{RemoteIpAddress}`.
- Exceeded response: `429 Too Many Requests` with body
  `{ "code": "RATE_LIMITED", "message": "Too many requests", "retryAfterSeconds": <int> }`
  and `Retry-After` header.
- Health endpoints (`/health`, `/health/live`, `/health/ready`) are **exempt** from rate limiting.
- Disabled in `Development` only when `RateLimit:Enabled=false`. Default is enabled.

## CORS policy (locked)

- Policy name: `frontend` (matches the per-service template in architecture.md).
- Allowed origins: `Cors:AllowedOrigins` array; default `["http://localhost:5173"]`.
- `AllowAnyHeader`, `AllowAnyMethod`, `AllowCredentials = true`.
- Preflight cache: 600s.
- Applied **only on the gateway** (downstream services also include the same policy per architecture.md template, but browser only ever talks to the gateway).

## Health endpoints (locked)

| Path           | Tags filter                  | Purpose                          |
|----------------|------------------------------|----------------------------------|
| `/health`      | all                          | aggregate liveness + readiness   |
| `/health/live` | (none — process up)          | liveness probe                   |
| `/health/ready`| `ready`                      | readiness probe; checks JWKS reachable + Redis reachable |

- Provided by `MapDefaultEndpoints()` from `LMS.ServiceDefaults`. Add JWKS + Redis health checks tagged `ready`.
- Exempt from auth and rate limiting.

## Events published / consumed

**None.** The gateway is a stateless proxy and does not participate in MassTransit. All async messaging stays inside services.

(Note: the architecture doc mentions an "upsert profile on new JWT" flow. In Phase 1 this is implemented by the **frontend** calling `POST /api/identity/profile` after first login, not by the gateway. The gateway never originates HTTP calls to services.)

## Configuration surface (locked keys)

```jsonc
{
  "Keycloak": { "Authority": "...", "Audience": "lms-api" },
  "Cors":     { "AllowedOrigins": ["http://localhost:5173"] },
  "RateLimit": {
    "Enabled": true,
    "PerTenantPerMinute": 600,
    "AnonymousPerMinute": 60,
    "WindowSeconds": 60
  },
  "ReverseProxy": { "Routes": { ... }, "Clusters": { ... } }
}
```

## Entity changes

**None.** Gateway has no database.

## Frontend impact

- Frontend `apiClient` base URL = gateway endpoint (already wired in AppHost via `VITE_API_BASE_URL`).
- 401 from any downstream surfaces as `code=UNAUTHENTICATED`; frontend triggers Keycloak re-login.
- 429 surfaces as `code=RATE_LIMITED`; frontend should respect `Retry-After`.

## Cross-service flows

- **Every** Phase 1 service relies on `X-User-Id`, `X-Tenant-Id`, `X-Roles`. The header names locked above are the contract for all 9 downstream services.
- Aspire `WithReference(gateway)` is used by the frontend resource only; downstream services do not reference the gateway.
- AppHost wires `gateway.WithReference(keycloak).WithReference(redis)` plus `WithReference` to **each** Phase 1 service so YARP can resolve them via service discovery.

## Open questions / non-goals

- **Phase 1 scope:** no request signing, no mTLS to backends (relies on cluster network).
- **JWKS caching TTL** uses Microsoft default (12h refresh, 1d expiration). Not tunable in Phase 1.
- **Per-route overrides** for rate limits are out of scope for Phase 1.
