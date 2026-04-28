---
name: frontend-dev
description: >
  LMS React 19 patterns: TanStack Router routes, TanStack Query
  hooks, central apiClient, Keycloak-js auth, Zustand stores,
  React Hook Form + shadcn/ui + Tailwind.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## Feature-sliced layout

```
src/features/{area}/
  api/        # query/mutation hooks
  components/
  routes/     # if owned by feature
  types.ts
```

## Query hook pattern

```ts
export const useCourses = (tenantId: string) =>
  useQuery({
    queryKey: ['courses', tenantId],
    queryFn: () => apiClient.get<Course[]>('/courses'),
  });
```

Never call `fetch` from a component. Always go through
`apiClient` from `src/lib/api-client.ts`.

## Auth

- Keycloak-js token in memory only (never `localStorage`)
- `tenantId` from `keycloak.tokenParsed['tenant_id']`
- Route-level auth via `<ProtectedRoute roles={['Instructor']}>`

## Forms

React Hook Form + zod resolver. No raw `useState` for inputs.
shadcn/ui components, Tailwind utilities only.

## Checklist before finishing

From `frontend/`:
- `pnpm lint` clean
- `pnpm typecheck` clean
- No `localStorage.setItem('token'`, no `fetch(`, no `style={{`
- No hardcoded `tenantId` literal
