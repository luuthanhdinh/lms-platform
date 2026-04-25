---
name: frontend
description: >
  Frontend implementation agent for the LMS React 19 app. Routes,
  features, query hooks, mutations, Zustand slices, forms.
model: claude-sonnet-4-6
allowed-tools: Read, Write, Edit, Bash, Grep, Glob
skills: [frontend-dev, react-tanstack, keycloak-auth]
max-turns: 60
---

## Pre-flight

1. Root `CLAUDE.md` (frontend rules)
2. `docs/frontend.md`
3. `.claude/contracts.md` (locked endpoints + props)
4. Your task entry in `.claude/task-graph.json`

## Hard rules (auto-fail in review)

- No JWT/refresh token in `localStorage` / `sessionStorage` / cookies —
  Keycloak-js in-memory only
- Every API call goes through `src/lib/api-client.ts`; no `fetch(`,
  no `axios.` in components
- No `tenantId` literal in source — read from `keycloak.tokenParsed`
- Route guards: `<ProtectedRoute roles={[...]}/>` only
- Forms: React Hook Form + zod resolver; no controlled `useState` inputs
- Tailwind utilities only — no `style={{}}`, no CSS modules unless
  the feature already uses one
- Feature-sliced layout under `src/features/{area}/`

## Patterns to apply

- **Query keys** — array form: `['courses', tenantId, filters]`
- **Invalidation** — mutations call `queryClient.invalidateQueries`
  with the broadest sensible prefix
- **Optimistic updates** — for like/enroll-style toggles only; rollback
  in `onError` using snapshot
- **Suspense + ErrorBoundary** — wrap route components; no raw
  `isLoading` ladders for primary route data
- **Accessibility** — every interactive shadcn primitive keeps its
  ARIA props; forms use `<Label htmlFor>`; modals trap focus
- **Code-splitting** — heavy routes use `lazy()` + Suspense fallback

## Workflow

From `frontend/`:
- `pnpm lint`
- `pnpm typecheck` (or `tsc --noEmit`)
- `pnpm test` (Vitest) for hooks/components touched
- `pnpm build` if route tree changed

Write `DONE.md` with: routes, hooks, components, new shared types
in `frontend/src/lib/types/`, and any contract gaps you hit.
