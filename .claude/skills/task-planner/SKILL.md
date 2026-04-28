---
name: task-planner
description: >
  Decomposition heuristics for the master agent: sizing,
  parallelism, dependency analysis, contract locking.
allowed-tools: Read, Bash, Grep, Glob
user-invocable: false
---

## Sizing rubric (per task)

| Size | Files touched | Effort |
| ---- | ------------- | ------ |
| S    | 1–3           | < 30 min agent time |
| M    | 4–10          | 30–90 min |
| L    | 10+ OR new service | > 90 min — split further |

If a task is L, split it. A subagent that runs > 60 min on a single
task is a planning failure.

## Decomposition order

1. Identify **shared boundaries** (events, endpoints, props) — these
   become `contracts.md`
2. Identify **migrations** — always their own task, no parallel reads
3. Identify **events** — own task via `events-architect`
4. Identify **publishers/consumers** — parallel after events locked
5. Identify **endpoints/UI** — parallel after contracts locked
6. Add `security-auditor` after impl
7. Add `reviewer` last (depends on all above)
8. Add `docs-writer` after reviewer
9. Pair `haiku-helper` with each Sonnet impl that lacks tests

## DAG sanity checks

- No cycles (topological sort succeeds)
- Critical path: longest dependency chain — print it
- Width: max parallel tasks at any layer — keep ≤ 5 to bound spend
- Every task has `acceptance` commands the runner can verify

## Contract-lock checklist

Before emitting `contracts.md`:
- Every event has `EventId`, `TenantId`, `OccurredAt`
- Every endpoint has request type, response type, error shape
- Every frontend hook has signature + return type
- Every entity change names migration filename

If unable to lock a boundary, raise it as an OPEN QUESTION in the
summary — do NOT emit a partial contract.
