# IdentityService

**Port:** 5101 | **DB:** `lms_identity` (PostgreSQL) | **ADRs:** ADR-001, ADR-002

## Responsibility
User profile management and tenant configuration.
Authentication is Keycloak — this service only stores supplementary profile data.

## Endpoints

```
GET  /api/identity/profile/me              → UserProfile
PUT  /api/identity/profile/me              ← UpdateProfileRequest
POST /api/identity/profile                 ← UpsertProfileRequest   # idempotent upsert
GET  /api/identity/users                   → PagedResult<UserSummary> # org-admin/admin; filters: role, active, search; pagination
POST /api/identity/users/invite            ← InviteUserRequest
PATCH /api/identity/users/{id}/role        ← { role }              # org-admin/admin
PATCH /api/identity/users/{id}/deactivate                          # org-admin/admin
GET  /api/identity/tenant                  → TenantConfig
PUT  /api/identity/tenant                  ← UpdateTenantRequest   # org-admin/admin
```

## Key flows

**First login upsert:** `POST /api/identity/profile` is idempotent — creates a new `UserProfile` or updates an existing one.
`KeycloakId` field maps to JWT `sub` claim, `TenantId` from `tenant_id` claim.
Only publishes `UserRegistered` on first creation (not on updates).

**Invite flow:**
1. Org-admin calls `POST /api/identity/users/invite`
2. Service creates `UserInvite` record with token
3. Invite email sent via NotificationWorker consumer (Phase 2+)
4. User clicks link → registers in Keycloak → gateway upserts profile (which marks invite accepted)
5. `UserRegistered` event published on successful profile upsert

## Events published
- `UserRegistered` — on first successful upsert
- `UserDeactivated` — on deactivation

## Events consumed
None in Phase 1.

## Security

IdentityService does **not** validate JWTs. Instead, it trusts the headers forwarded by the gateway:
- `X-User-Id` — authenticated user's ID
- `X-Tenant-Id` — tenant context
- `X-Roles` — comma-separated role list

All endpoints check `X-Tenant-Id` and `X-User-Id` presence via `ITenantContext` injected middleware.
Authorization checks (admin/org-admin) read `X-Roles` directly.

## Master tenant seeding

On first database migration, the Migrator creates a default `TenantConfig` with:
- TenantId: `00000000-0000-0000-0000-000000000001` (or `Seeding:MasterTenantId` config value)
- Name: "Master Tenant"
- Plan: Free

All subsequent multi-tenant isolation relies on this seeded tenant record.

## Entities
See `docs/entities.md` → IdentityService section.
