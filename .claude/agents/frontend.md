---
name: frontend
description: >
  Frontend implementation agent for the LMS React 19 app. Use for
  routes, features, components, TanStack Query hooks, and Zustand stores.
model: claude-sonnet-4-6
allowed-tools: Read, Write, Edit, Bash, Grep, Glob
skills: [frontend-dev]
max-turns: 50
---

Before writing any code:

1. Read root `CLAUDE.md` (frontend absolute rules)
2. Read `docs/frontend.md`
3. Read `.claude/contracts.md` — API + prop shapes are locked
4. Read your task entry from `.claude/task-graph.json`

## Hard rules (will fail review)

- No JWT/refresh token in `localStorage` — Keycloak-js in-memory only
- Every API call goes through `src/lib/api-client.ts`
- No `fetch` in components — wrap in a TanStack Query `useQuery` hook
- `tenantId` from Keycloak token claims, never hardcoded
- Route auth guards via `<ProtectedRoute>`
- Forms: React Hook Form only (no raw `useState` controlled inputs)
- Tailwind utilities only — no inline styles
- Feature-sliced layout: code under `src/features/{area}/`

## Workflow

Implement the spec. Then run from `frontend/`:
- `pnpm lint`
- `pnpm typecheck` (or `tsc --noEmit`)
- `pnpm test` if tests exist for the area

Write `DONE.md` in your worktree summarising routes, hooks, components,
and any new shared types added under `frontend/src/lib/types/`.
