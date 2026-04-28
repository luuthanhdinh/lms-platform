---
name: react-tanstack
description: >
  TanStack Query + Router patterns: query keys, invalidation,
  optimistic updates, Suspense + ErrorBoundary, route loaders.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## Query keys

Array form, namespaced, tenant-aware:

```ts
['courses', tenantId, { filter: 'published' }] as const
```

Centralize keys in `src/features/<area>/api/keys.ts`:

```ts
export const courseKeys = {
  all: (t: string) => ['courses', t] as const,
  list: (t: string, f: Filter) => [...courseKeys.all(t), f] as const,
  byId: (t: string, id: string) => [...courseKeys.all(t), id] as const,
};
```

## Hooks

```ts
export const useCourse = (id: string) => {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: courseKeys.byId(tenantId, id),
    queryFn: ({ signal }) => apiClient.get<Course>(`/courses/${id}`, { signal }),
  });
};
```

- Always pass `signal` for cancellation
- `staleTime` per data domain — set in `query-client.ts` defaults

## Mutations + invalidation

```ts
const qc = useQueryClient();
return useMutation({
  mutationFn: (input: Publish) =>
    apiClient.post(`/courses/${input.id}/publish`),
  onSuccess: (_, v) => qc.invalidateQueries({
    queryKey: courseKeys.byId(tenantId, v.id),
  }),
});
```

## Optimistic updates (use sparingly)

Snapshot → mutate cache → rollback in `onError` → invalidate in
`onSettled`. Only for toggle-style UX.

## Suspense + ErrorBoundary

Wrap each route with both. `useSuspenseQuery` for primary route data
to avoid `isLoading` ladders.

## Router loaders

For SEO/initial-paint-critical pages, prefetch in TanStack Router
loader using `queryClient.ensureQueryData(...)`.
