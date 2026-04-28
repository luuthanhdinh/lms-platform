# LMS.Gateway

**Responsibility:** YARP reverse proxy that serves as the sole JWT trust boundary. Validates incoming JWTs against Keycloak, forwards authenticated requests to downstream Phase 1 services via Aspire-discovered clusters, enriches each request with trusted headers (`X-User-Id`, `X-Tenant-Id`, `X-Roles`, `X-Correlation-Id`), and enforces per-tenant rate limiting via Redis.

**Port / AppHost resource name:** `gateway` / port 5000 (development)

**Key dependencies:**
- Keycloak (JWKS OIDC discovery, realm `lms`)
- Redis (per-tenant rate limiting)
- All Phase 1 services (YARP upstream clusters via Aspire service discovery): `identity`, `courses`, `content`, `enrollment`, `progress`, `assessment`, `certificate`

**Architecture decisions:** [See .claude/contracts.md for locked contract](../../.claude/contracts.md). Gateway-specific ADR authoring deferred pending Phase 2 (e.g., ADR-Gateway-Header-Trust-Boundary, Redis rate-limit migration, certificate pinning).

---

## Endpoints (proxied routes)

All routes match `/api/{prefix}/{**rest}` and proxy to logical Aspire-discovered clusters.

| Route id | Path pattern | Upstream cluster | Auth |
|---|---|---|---|
| `identity` | `/api/identity/{**rest}` | `identity` | required |
| `courses` | `/api/courses/{**rest}` | `courses` | required |
| `content` | `/api/content/{**rest}` | `content` | required |
| `enrollment` | `/api/enrollments/{**rest}` | `enrollment` | required |
| `progress` | `/api/progress/{**rest}` | `progress` | required |
| `assessment` | `/api/assessments/{**rest}` | `assessment` | required |
| `certificate` | `/api/certificates/{**rest}` | `certificate` | required |
| `verify` | `/verify/{**rest}` | `certificate` | anonymous |

**Path forwarding:** Proxied as-is to destination (no path strip). Downstream services own `/api/{prefix}/...` routes.

---

## Health endpoints

| Path | Tags | Purpose |
|---|---|---|
| `/health` | all | Aggregate liveness + readiness |
| `/health/live` | (none — process up) | Liveness probe; no external dependencies checked |
| `/health/ready` | `ready` | Readiness probe; requires JWKS reachable + Redis reachable |

Provided by `MapDefaultEndpoints()` from `LMS.ServiceDefaults`. JWKS and Redis health checks tagged `ready`. All three endpoints exempt from auth and rate limiting.

---

## Forwarded headers (downstream trust contract)

Every Phase 1 service reads these headers and **never re-validates JWT**:

| Header | Source | Format | Required |
|---|---|---|---|
| `X-User-Id` | JWT `sub` claim | UUID string | yes when authenticated |
| `X-Tenant-Id` | JWT `tenant_id` custom claim | UUID string | yes when authenticated |
| `X-Roles` | JWT `realm_access.roles[]` flattened | Comma-separated, no spaces (e.g., `student,instructor`) | yes when authenticated |
| `X-Correlation-Id` | Inbound or generated UUID | GUID string | always |

**Header stripping:** Inbound `X-User-Id`, `X-Tenant-Id`, `X-Roles`, `Authorization` are stripped from outbound proxied request, then replaced with JWT-derived trusted values. Inbound forgeries do not survive.

**Anonymous routes:** `/verify/{**rest}` forwards neither auth headers. Correlation ID always included.

---

## Auth policy

- Scheme: `JwtBearerDefaults.AuthenticationScheme` (single).
- Authority: `Keycloak:Authority` (e.g., `http://keycloak:8080/realms/lms`).
- Audience: `Keycloak:Audience` = `lms-api`.
- `RequireHttpsMetadata` = `false` in Development, `true` otherwise.
- JWKS auto-fetched from `{Authority}/protocol/openid-connect/certs`; cached by `Microsoft.IdentityModel` (default 12h refresh, 1d expiration).
- Roles claim mapped to `ClaimTypes.Role` via `JwtBearerEvents.OnTokenValidated` flattening `realm_access.roles[]`.
- Default authorization policy: `RequireAuthenticatedUser`. Routes opt out via `AuthorizationPolicy: "anonymous"` in YARP appsettings.

**Error responses:**
- 401 (unauthenticated): `{ "code": "UNAUTHENTICATED", "message": "Authentication required" }`
- 403 (insufficient permissions): `{ "code": "FORBIDDEN", "message": "Insufficient permissions" }`

---

## Rate limiting

**Algorithm:** Fixed-window limiter over Redis.

**Partition keys:**
- Authenticated: `tenant:{X-Tenant-Id}` (after JWT validation).
- Anonymous: `ip:{RemoteIpAddress}`.

**Limits:**
- Per-tenant: **600 req/min** (configurable via `RateLimit:PerTenantPerMinute`).
- Anonymous (per-IP on `/verify/{**rest}` and `/health*`): **60 req/min** (configurable via `RateLimit:AnonymousPerMinute`).
- Window: **60 seconds** (`RateLimit:WindowSeconds`).

**Health endpoints** (`/health`, `/health/live`, `/health/ready`) are **exempt** from rate limiting.

**Exceeded response:** `429 Too Many Requests` with body `{ "code": "RATE_LIMITED", "message": "Too many requests", "retryAfterSeconds": <int> }` and `Retry-After` header.

**Disabled in Development** when `RateLimit:Enabled=false`. Default is enabled.

---

## CORS

**Policy name:** `frontend` (applied only on gateway; browser only talks to gateway).

**Allowed origins:** `Cors:AllowedOrigins` array; default `["http://localhost:5173"]`.

**Configuration:**
- `AllowAnyHeader` ✓
- `AllowAnyMethod` ✓
- `AllowCredentials` ✓ (required for Keycloak cookie flows)
- Preflight cache: 600s

---

## Events published / consumed

**None.** Gateway is a stateless proxy with no database. All async messaging stays inside downstream services. Gateway never originates HTTP calls.

---

## Configuration

```jsonc
{
  "Keycloak": {
    "Authority": "http://keycloak:8080/realms/lms",
    "Audience": "lms-api"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  },
  "RateLimit": {
    "Enabled": true,
    "PerTenantPerMinute": 600,
    "AnonymousPerMinute": 60,
    "WindowSeconds": 60
  },
  "ReverseProxy": {
    "Routes": {
      "identity":    { "ClusterId": "identity",    "Match": { "Path": "/api/identity/{**rest}" }, "AuthorizationPolicy": "default" },
      "courses":     { "ClusterId": "courses",     "Match": { "Path": "/api/courses/{**rest}" }, "AuthorizationPolicy": "default" },
      "content":     { "ClusterId": "content",     "Match": { "Path": "/api/content/{**rest}" }, "AuthorizationPolicy": "default" },
      "enrollment":  { "ClusterId": "enrollment",  "Match": { "Path": "/api/enrollments/{**rest}" }, "AuthorizationPolicy": "default" },
      "progress":    { "ClusterId": "progress",    "Match": { "Path": "/api/progress/{**rest}" }, "AuthorizationPolicy": "default" },
      "assessment":  { "ClusterId": "assessment",  "Match": { "Path": "/api/assessments/{**rest}" }, "AuthorizationPolicy": "default" },
      "certificate": { "ClusterId": "certificate", "Match": { "Path": "/api/certificates/{**rest}" }, "AuthorizationPolicy": "default" },
      "verify":      { "ClusterId": "certificate", "Match": { "Path": "/verify/{**rest}" }, "AuthorizationPolicy": "anonymous" }
    },
    "Clusters": {
      "identity":    { "Destinations": { "d1": { "Address": "http://identity" } } },
      "courses":     { "Destinations": { "d1": { "Address": "http://courses" } } },
      "content":     { "Destinations": { "d1": { "Address": "http://content" } } },
      "enrollment":  { "Destinations": { "d1": { "Address": "http://enrollment" } } },
      "progress":    { "Destinations": { "d1": { "Address": "http://progress" } } },
      "assessment":  { "Destinations": { "d1": { "Address": "http://assessment" } } },
      "certificate": { "Destinations": { "d1": { "Address": "http://certificate" } } }
    }
  }
}
```

---

## Key flows

1. **Authenticated request:** Inbound → YARP route match → header strip (`X-User-Id`, `X-Tenant-Id`, `X-Roles`, `Authorization`) → JWT validation → header injection (from claims) → correlation ID injection → upstream proxy
2. **Unauthenticated protected route:** 401 with `code=UNAUTHENTICATED`
3. **Rate limit exceeded:** 429 with `code=RATE_LIMITED`, `retryAfterSeconds`, and `Retry-After` header
4. **Anonymous route (`/verify`):** Request → YARP route match → header strip → no auth → correlation ID injection → upstream proxy

---

## Frontend integration

- Frontend `apiClient` base URL points to gateway (configured via `VITE_API_BASE_URL` in AppHost).
- 401 from any downstream surfaces as `code=UNAUTHENTICATED`; frontend triggers Keycloak re-login.
- 429 surfaces as `code=RATE_LIMITED`; frontend respects `Retry-After` header.

---

## Phase 2 TODOs

- Replace in-memory rate limiter with Redis-backed `RedisRateLimiting` package or equivalent `IDistributedRateLimiter`.
- Scope `DangerousAcceptAnyServerCertificateValidator` to Development only in JWKS health client (currently always used via `HttpClientHandler`).
- Add startup validation for non-empty `Keycloak:Authority`; fail fast on misconfiguration.
- Consider authoring ADR-Gateway-Header-Trust-Boundary (formalize forwarded header contract, TLS-in-transit, inbound header sanitization guarantees).
- Explore per-route rate-limit overrides (e.g., `/verify` stricter than `/api/courses`).

