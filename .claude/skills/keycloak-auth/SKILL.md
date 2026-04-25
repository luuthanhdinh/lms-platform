---
name: keycloak-auth
description: >
  Keycloak-js + ProtectedRoute patterns on the frontend; trust
  boundary expectations for backend.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## Frontend

- `keycloak-js` initialized once in `src/lib/keycloak.ts`
- Token kept in memory only — `keycloak.token`, refreshed via
  `keycloak.updateToken(30)` before requests
- `apiClient` request interceptor attaches `Authorization: Bearer`
- On `onTokenExpired`, attempt silent refresh; on failure, redirect
  to login

```ts
export const useTenantId = () => {
  const k = useKeycloak();
  return k.tokenParsed?.tenant_id as string;
};
```

- `ProtectedRoute` wraps each protected route element; checks both
  authenticated state AND required roles from `tokenParsed.roles`
- Never `localStorage.setItem('token', ...)` — fail review

## Backend (trust boundary)

- Services NEVER add `AddJwtBearer` — gateway already validated
- Services read `X-User-Id`, `X-Tenant-Id`, `X-Roles` from headers
  via `TenantMiddleware` + `UserContextMiddleware`
- Authorization policies map to `X-Roles`:

```csharp
options.AddPolicy("Instructor", p =>
    p.RequireAssertion(ctx =>
        ctx.User.HasClaim("role", "Instructor")));
```

- Tests issue stub headers, not real JWTs (see xunit-integration)
