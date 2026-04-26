Investigate a performance issue: $ARGUMENTS

`$ARGUMENTS`: the symptom or hot path
(e.g. "course list endpoint slow above 1k courses").

Process:

1. **Define "slow"** — what's the current metric (p50/p95) and the
   target? If unknown, ask. No optimizing without a number.
2. **Profile, don't guess**:
   - Backend: add a custom Activity span around the suspect block;
     run the scenario; check OTEL trace in Aspire dashboard
   - Database: enable EF Core sensitive logging in dev, capture the
     generated SQL, run `EXPLAIN ANALYZE` against the local Postgres
   - Frontend: React DevTools profiler + Network tab; check query
     dedup and stale-time
3. **Look for the usual suspects**:
   - **N+1**: missing `.Include()` or projection
   - **Missing index**: column in `WHERE`/`ORDER BY` without one;
     compound index `(tenant_id, <col>)` is almost always right
   - **Cartesian explosion**: multiple `Include()` of collections —
     split into separate queries
   - **Sync over async** in handlers
   - **Unbounded result sets** — every list endpoint must paginate
   - **Cache-miss patterns**: keys missing `tenant_id` prefix
   - **LLM**: call uncached, model too large for the task, output
     tokens unbounded
   - **Frontend**: query key churn (re-creating arrays inline),
     missing `staleTime`, render in a parent that re-renders too often
4. **Propose fix with measurement plan** — show before metric,
   expected after, how you'll verify.
5. **Never** add caching as the first answer. Fix the query first.

Output: short report — bottleneck (file:line), evidence, proposed
fix, est. impact, verification step. Do NOT apply yet unless trivial
and obvious; for non-trivial fixes, follow up with `/plan`.
