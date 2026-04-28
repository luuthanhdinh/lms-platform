---
name: yarp-gateway
description: >
  YARP route, transform, JWT, header forwarding, and rate-limit
  patterns for LMS.Gateway.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## Route + cluster

```jsonc
"Routes": {
  "courses": {
    "ClusterId": "course-svc",
    "Match": { "Path": "/api/courses/{**rest}" },
    "AuthorizationPolicy": "Authenticated",
    "RateLimiterPolicy": "per-tenant",
    "Transforms": [
      { "PathPattern": "/courses/{**rest}" },
      { "RequestHeader": "X-User-Id",   "Set": "" },
      { "RequestHeader": "X-Tenant-Id", "Set": "" },
      { "RequestHeader": "X-Roles",     "Set": "" },
      { "RequestHeadersCopy": "true" },
      { "RequestHeader": "Authorization", "Set": "" }
    ]
  }
}
```

Header values for `X-User-Id`/`X-Tenant-Id`/`X-Roles` are set by a
custom `ITransformProvider` from validated JWT claims — strip the
inbound `Authorization` so downstream cannot see the raw token.

## Rate limiting

- `per-tenant` policy partitions by `X-Tenant-Id`
- LLM/PDF/email routes use a stricter `per-tenant-llm` policy
- Use the .NET 9 `RateLimiter` middleware via YARP integration

## Auth

- `AddJwtBearer` with Keycloak metadata URL
- `RequireHttpsMetadata = true` in non-Dev
- Map Keycloak `realm_access.roles` → `role` claim

## CORS

- Allow-list from config; never `*`; credentials only for trusted
  origins

## Tests

- Issue stub JWT in tests; assert downstream receives forwarded
  headers AND no `Authorization` header
