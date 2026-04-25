# IdentityService

**Port:** 5101 | **DB:** `lms_identity` (PostgreSQL) | **ADRs:** ADR-001, ADR-002

## Responsibility
User profile management and tenant configuration.
Authentication is Keycloak — this service only stores supplementary profile data.

## Endpoints

```
GET  /api/identity/profile/me              → UserProfile
PUT  /api/identity/profile/me              ← UpdateProfileRequest
POST /api/identity/profile                 ← UpsertProfileRequest   # idempotent, called by gateway on new JWT
GET  /api/identity/users                   → UserSummary[]          # org-admin/admin only
POST /api/identity/users/invite            ← InviteUserRequest
PATCH /api/identity/users/{id}/role        ← { role }              # org-admin/admin
PATCH /api/identity/users/{id}/deactivate                          # org-admin/admin
GET  /api/identity/tenant                  → TenantConfig
PUT  /api/identity/tenant                  ← UpdateTenantRequest   # org-admin/admin
```

## Key flows

**First login upsert:** Gateway detects new JWT sub → calls `POST /api/identity/profile`.
Service creates `UserProfile` with `KeycloakId = sub`, `TenantId` from `tenant_id` claim.
Idempotent — safe to call on every login.

**Invite flow:**
1. Org-admin calls `POST /api/identity/users/invite`
2. Service creates `UserInvite` record with token
3. Publishes to NotificationWorker → sends invite email
4. User clicks link → registers in Keycloak → gateway upserts profile with correct role

## Events published
- `UserRegistered` — on first successful upsert
- `UserDeactivated` — on deactivation

## Events consumed
None in Phase 1.

## Entities
See `docs/entities.md` → IdentityService section.
