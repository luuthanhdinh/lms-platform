# `.claude/` — Master + Parallel-Subagent Workflow

A multi-agent system for the LMS platform: **one Opus master plans**,
**many Sonnet/Haiku subagents implement in parallel git worktrees**,
**a reviewer gates the merge**.

```
              ┌─── master (Opus) ────┐
              │  plans only          │  → contracts.md + task-graph.json
              └──────────┬───────────┘
                         │
        ┌────────────────┼────────────────┐
        ▼                ▼                ▼
   db-migrator     events-architect    gateway-ops      ◄─ schema/contracts layer
        │                │                │
        └────────┬───────┴────────┬───────┘
                 ▼                ▼
            backend × N      frontend × N                ◄─ implementation layer
                 │                │
                 └────────┬───────┘
                          ▼
                   security-auditor
                          ▼
                       reviewer
                          ▼
                     docs-writer
```

---

## Layout

```
.claude/
├── README.md                    ← you are here
├── CLAUDE.md                    ← operator overrides (loaded into every turn)
├── settings.json                ← permissions, hooks, model
├── orchestrate.py               ← parallel runner (DAG executor)
├── agents/                      ← 10 role-specialised agents
│   ├── master.md                  Opus  · planner, no code
│   ├── backend.md                 Sonnet · .NET impl
│   ├── frontend.md                Sonnet · React impl
│   ├── db-migrator.md             Sonnet · EF Core migrations
│   ├── events-architect.md        Sonnet · MassTransit + sagas
│   ├── gateway-ops.md             Sonnet · YARP routes + auth
│   ├── tester.md                  Sonnet · xUnit/integration/contract
│   ├── security-auditor.md        Sonnet · tenant isolation + OWASP
│   ├── reviewer.md                Sonnet · final code review
│   ├── docs-writer.md             Haiku  · keeps docs/ in sync
│   └── haiku-helper.md            Haiku  · tests/types/docstrings
├── commands/                    ← user-invocable slash commands (see Catalog below)
└── skills/                      ← reusable patterns auto-loaded by agents
    ├── backend-dev/               .NET endpoint/service patterns + check-patterns.sh
    ├── frontend-dev/              React/Vite/Tailwind patterns
    ├── code-reviewer/             review checklist
    ├── task-planner/              decomposition heuristics (master)
    ├── tenant-isolation/          + executable audit.sh
    ├── masstransit-events/        publish/consume/saga/outbox/idempotency
    ├── dotnet-ef-migrations/      additive, expand/contract
    ├── xunit-integration/         WebApplicationFactory + Testcontainers
    ├── react-tanstack/            query keys, invalidation, Suspense
    ├── keycloak-auth/             keycloak-js + ProtectedRoute + trust boundary
    ├── llm-client/                ILlmClient, prompt caching, quotas
    ├── observability/             OTEL, structured logs, health
    ├── yarp-gateway/              routes, transforms, rate limit
    └── questpdf-certs/            certificate templates
```

Generated artifacts (gitignored or short-lived):

```
.claude/contracts.md             ← locked shared boundaries (per feature)
.claude/task-graph.json          ← DAG of subagent tasks
.claude/run-summary.json         ← orchestrator output (timings, failures)
.claude/review-report.md         ← reviewer output
.claude/security-report.md       ← security-auditor output
.claude/tenant-audit.md          ← /audit-tenant output
.claude-worktrees/T1-*           ← per-task git worktrees + agent logs
```

---

## Core idea

Parallel subagents only stay coordinated if **shared boundaries are
locked before any of them start**. The master agent's only job is to
write `contracts.md` (events, endpoints, hook signatures, entity
shapes) and a DAG of tasks (`task-graph.json`) that says *which
agent owns which files*.

The orchestrator:
1. Hashes `contracts.md` at start.
2. Spawns each task in its own `git worktree` on its own branch.
3. Watches per-task timeouts.
4. **Fails any task that mutated `contracts.md`** (hash mismatch).
5. Cascade-skips tasks whose deps failed.
6. Writes `run-summary.json`.

Then `security-auditor` → `reviewer` → optional `docs-writer`.

---

## Command catalog

34 slash commands, grouped by what you're trying to do. Each is a
markdown file under `commands/`; many delegate to a specific agent
+ skill so the file stays short.

### Multi-agent pipeline (the headliners)
| Command | Purpose |
|---|---|
| [/plan](commands/plan.md) `<feature>` | Master agent plans only. Writes `contracts.md` + `task-graph.json`. Stops. |
| [/ship](commands/ship.md) `<feature>` | `/plan` → wait for approval → `orchestrate.py` → reviewer. Full pipeline. |
| [/review](commands/review.md) | Re-run reviewer on the last task graph's branches. |

### Daily flow
| Command | Purpose |
|---|---|
| [/catchup](commands/catchup.md) | Status briefing: branch, recent commits, open PRs, in-flight subagent runs. |
| [/triage](commands/triage.md) | Ranks the next thing to work on (review-blocked, failed tasks, assigned issues, drift). |
| [/focus](commands/focus.md) | Lists unfinished work in current branch (TODO/FIXME/skipped tests/`any`) by severity. |
| [/standup](commands/standup.md) | Yesterday/Today/Blockers from git + PR history. |
| [/dump](commands/dump.md) | Local Markdown dump of branch state for handoff (no secrets, no auto-upload). |

### Author + ship code
| Command | Purpose |
|---|---|
| [/spec](commands/spec.md) `<feature>` | Tech spec to `docs/specs/spec-YYYYMMDD-{slug}.md` with the full LMS template. |
| [/fix](commands/fix.md) `<symptom>` | Repro → failing test FIRST → minimal patch → verify. |
| [/refactor](commands/refactor.md) `<smell>` | Pin behavior with characterization tests, small reversible steps, no behavior change. |
| [/explain](commands/explain.md) `<file\|symbol\|flow>` | Read-only walkthrough with file:line citations, ≤25 lines. |
| [/test](commands/test.md) `[scope]` | Auto-scopes tests to changed files (dotnet + pnpm in parallel). |
| [/coverage](commands/coverage.md) `[svc]` | Runs coverage; surfaces uncovered scenarios (not a percent). |
| [/perf](commands/perf.md) `<symptom>` | Profile, don't guess. N+1, indexes, EXPLAIN ANALYZE. Cache last. |
| [/bench](commands/bench.md) `<Type.Method>` | BenchmarkDotNet skeleton + run; allocations + ratio vs baseline. |
| [/commit](commands/commit.md) `[subject]` | Smart conventional commit; refuses secrets/binaries; never `--amend`. |
| [/pr](commands/pr.md) `[--draft]` | Push + open PR with structured body (Summary/Changes/Test plan/Contracts). |

### Backend specifics (LMS-aware)
| Command | Purpose |
|---|---|
| [/scaffold-service](commands/scaffold-service.md) `<Name>` | 4-project layout (Domain/Infra/Api/Migrator), refs, NuGet, AppHost wiring, initial migration. |
| [/migrate](commands/migrate.md) `<svc> <Name>` | Generate one EF migration via `db-migrator` (uses `-p Infra -s Migrator`). |
| [/event-add](commands/event-add.md) `<Event> [pub] [cons,...]` | Lock a new MassTransit event via `events-architect`; updates `docs/events.md`. |
| [/tenant-test](commands/tenant-test.md) `<METHOD> <path>` | Generate the 404-not-403 paired isolation test for any endpoint. |
| [/audit-tenant](commands/audit-tenant.md) | Cross-codebase tenant-leak audit (script + architecture tests + spot-check). |
| [/flag](commands/flag.md) `add\|remove\|list <Flag> [svc]` | Add/remove `[FeatureGate]`; Phase 3+ defaults `false`. |
| [/log](commands/log.md) `<svc> [filter]` | Tail Aspire dashboard / OTEL logs filtered by service / tenant / trace. |

### Knowledge + governance
| Command | Purpose |
|---|---|
| [/onboard](commands/onboard.md) | Fresh-clone walkthrough: prereq check, doc reading order, run commands. |
| [/adr](commands/adr.md) `<decision>` | New ADR with auto-numbering + project tone. |
| [/sync-docs](commands/sync-docs.md) `[ref]` | Bring `docs/services/*.md`, `docs/events.md`, `docs/entities.md` in line with shipped code. |
| [/diagram](commands/diagram.md) `<flow\|svc\|"events"\|"architecture">` | Mermaid sequence/flowchart/ER diagram saved to `docs/diagrams/`. |

### Operations + safety
| Command | Purpose |
|---|---|
| [/rollback](commands/rollback.md) `migration\|release\|flag <target>` | Generate rollback plan; never auto-executes. |
| [/secrets](commands/secrets.md) `init\|list\|set\|rotate` | Wraps `dotnet user-secrets`; never prints values; refuses prod-shaped keys. |
| [/realm-sync](commands/realm-sync.md) `export\|import\|diff` | Sync local Keycloak realm with `realms/*.json`. |
| [/ai-cost](commands/ai-cost.md) `[days]` | LLM token + cost report per tenant/model with cache-hit ratio. |
| [/cleanup](commands/cleanup.md) `[--apply]` | Sweep merged worktrees/branches/old artifacts; dry-run by default. |

### Three commands cover 95% of feature work

```bash
/plan <feature>        # plan only, stop. Review the artifacts.
/ship <feature>        # plan → wait for approval → orchestrate → review
/review                # re-run reviewer on existing branches
```

---

## Worked example — "publish a course → notify enrollees"

### 1. Plan

```
/plan add course publish flow that emails enrolled students
```

The master agent reads `docs/services/course.md`,
`docs/services/notification.md`, `docs/events.md`, then writes:

**`.claude/contracts.md`** (excerpt)
```markdown
## Events (LMS.Contracts/Courses)
- record CoursePublished(Guid EventId, Guid TenantId, Guid CourseId,
                         DateTime OccurredAt);

## HTTP endpoints
- POST /courses/{id}/publish
  → 204 NoContent | 404 NotFound | 409 PAYMENT_REQUIRED

## Entity changes
- Course.PublishedAt: DateTime?  (migration: AddCoursePublishedAt)

## Frontend
- usePublishCourse() → mutation
- <CoursePublishButton course={Course}/>  in features/courses/components

## Cross-service flow
- CourseService publishes CoursePublished
  → ProgressService consumes (seeds progress rows)
  → NotificationWorker consumes (sends email)
```

**`.claude/task-graph.json`** (excerpt)
```json
{
  "feature": "course publish + notify enrollees",
  "phase": 1, "complexity": "M",
  "tasks": [
    { "id": "T1", "agent": "events-architect",
      "title": "Lock CoursePublished event + topology",
      "files": ["src/LMS.Contracts/Courses/CoursePublished.cs"],
      "depends_on": [], "branch": "feat/publish-T1",
      "worktree": ".claude-worktrees/T1-event",
      "timeout_minutes": 20 },

    { "id": "T2", "agent": "db-migrator",
      "title": "Add Course.PublishedAt nullable column",
      "files": ["src/services/LMS.CourseService/Persistence/**"],
      "depends_on": [], "branch": "feat/publish-T2",
      "worktree": ".claude-worktrees/T2-migration",
      "timeout_minutes": 20 },

    { "id": "T3", "agent": "backend",
      "title": "POST /courses/{id}/publish + outbox publish",
      "files": ["src/services/LMS.CourseService/Features/Publish/**"],
      "depends_on": ["T1", "T2"],
      "branch": "feat/publish-T3", "worktree": ".claude-worktrees/T3-api",
      "timeout_minutes": 40,
      "acceptance": ["dotnet build", "dotnet test --filter Publish"] },

    { "id": "T4", "agent": "backend",
      "title": "ProgressService consumer: seed on CoursePublished",
      "files": ["src/services/LMS.ProgressService/Consumers/**"],
      "depends_on": ["T1"], "branch": "feat/publish-T4",
      "worktree": ".claude-worktrees/T4-progress" },

    { "id": "T5", "agent": "backend",
      "title": "NotificationWorker consumer: send publish email",
      "files": ["src/services/LMS.NotificationWorker/Consumers/**"],
      "depends_on": ["T1"], "branch": "feat/publish-T5",
      "worktree": ".claude-worktrees/T5-notif" },

    { "id": "T6", "agent": "gateway-ops",
      "title": "Add /api/courses/{id}/publish route at YARP",
      "files": ["src/gateway/LMS.Gateway/appsettings.json"],
      "depends_on": ["T3"], "branch": "feat/publish-T6",
      "worktree": ".claude-worktrees/T6-gateway" },

    { "id": "T7", "agent": "frontend",
      "title": "usePublishCourse() + CoursePublishButton",
      "files": ["frontend/src/features/courses/**"],
      "depends_on": ["T6"], "branch": "feat/publish-T7",
      "worktree": ".claude-worktrees/T7-ui" },

    { "id": "T8", "agent": "tester",
      "title": "Integration tests: publish flow end-to-end",
      "files": ["tests/LMS.IntegrationTests/CoursePublishTests.cs"],
      "depends_on": ["T3","T4","T5"],
      "branch": "feat/publish-T8", "worktree": ".claude-worktrees/T8-tests" },

    { "id": "T9", "agent": "security-auditor",
      "title": "Audit publish flow for tenant isolation + authz",
      "files": [],
      "depends_on": ["T3","T4","T5","T6","T7"],
      "branch": "feat/publish-T9", "worktree": ".claude-worktrees/T9-sec" },

    { "id": "T10", "agent": "reviewer",
      "title": "Final review",
      "files": [],
      "depends_on": ["T3","T4","T5","T6","T7","T8","T9"],
      "branch": "feat/publish-T10", "worktree": ".claude-worktrees/T10-rev" },

    { "id": "T11", "agent": "docs-writer",
      "title": "Update docs/services/course.md + events.md",
      "files": ["docs/**"],
      "depends_on": ["T10"],
      "branch": "feat/publish-T11", "worktree": ".claude-worktrees/T11-docs" }
  ]
}
```

Critical path: `T1 → T3 → T6 → T7 → T9 → T10 → T11` (7 hops).
Max parallel width: 5 at the impl layer.

### 2. Approve & ship

```
/ship add course publish flow that emails enrolled students
```

`/ship` re-runs `/plan`, prints "Reply 'approved' to continue", then
on approval runs `python .claude/orchestrate.py`:

```
Contract lock: 9c4a1f2e8b73
[+] T1 Lock CoursePublished (agent=events-architect)
[+] T2 Add PublishedAt column (agent=db-migrator)
[OK]   T1 (43s)
[OK]   T2 (61s)
[+] T3 POST /publish (agent=backend)
[+] T4 ProgressService consumer (agent=backend)
[+] T5 NotificationWorker consumer (agent=backend)
[OK]   T4 (88s)
[OK]   T5 (102s)
[OK]   T3 (147s)
[+] T6 Gateway route (agent=gateway-ops)
[OK]   T6 (38s)
[+] T7 React publish button (agent=frontend)
[+] T8 Integration tests (agent=tester)
[OK]   T8 (210s)
[OK]   T7 (185s)
[+] T9 Security audit (agent=security-auditor)
[OK]   T9 (94s)
[+] T10 Review (agent=reviewer)
[OK]   T10 (76s)
[+] T11 Doc sync (agent=docs-writer)
[OK]   T11 (52s)

=== Run complete in 798s ===
  ok:      11
  failed:  0
  skipped: 0
  summary: .claude/run-summary.json
```

### 3. Read the report, merge

```bash
cat .claude/review-report.md
cat .claude/security-report.md
git diff main..feat/publish-T3      # spot-check any branch
```

If reviewer says APPROVE, merge each branch via your normal PR flow.
The orchestrator never auto-merges.

---

## Recipes

A few common combinations that show how the commands compose.

### Start of day

```
/catchup          # what's the state?
/triage           # what should I do next?
```

### "I have a bug to fix"

```
/fix payment webhook returns 500 on retried delivery
/test                          # verify scope green
/commit                        # smart conventional commit
/pr                            # PR with structured body
```

### "New feature, end-to-end"

```
/spec course publish + email enrollees      # write the spec doc
/plan course publish + email enrollees      # master plans the DAG
# review .claude/contracts.md + task-graph.json
/ship course publish + email enrollees      # full pipeline
/sync-docs                                   # docs catch up
/cleanup                                     # remove merged worktrees
```

### "New microservice"

```
/scaffold-service Payment      # 4-project layout + AppHost wiring
/migrate payment Initial       # already done by scaffold; verify
/event-add PaymentCompleted payment enrollment,notification
/plan checkout flow            # then build it on top
```

### "Performance investigation"

```
/perf course list slow above 1k courses
/explain CourseService.ListAsync     # understand current shape
/bench CourseService.ListAsync       # measure before
# apply fix
/bench CourseService.ListAsync       # measure after
```

### "Pre-release safety pass"

```
/audit-tenant                  # cross-codebase tenant-leak audit
/coverage                      # gaps, not percent
/focus                         # leftover TODOs in branch
/ai-cost 7                     # any cost spikes?
```

### "Schema rollback"

```
/rollback migration course     # generates ROLLBACK.md
# read it, run it manually
```

### "Just a migration"

```
/migrate enrollment AddRefundedAtColumn
```

Spawns `db-migrator` only. Generates the EF migration, round-trips
it, applies to local Aspire Postgres, writes `MIGRATION.md`. No PR.

---

## How the pieces fit

### Agents
Markdown files in `agents/` with YAML frontmatter:

```yaml
name: backend
model: claude-sonnet-4-6
allowed-tools: Read, Write, Edit, Bash, Grep, Glob
skills: [backend-dev, masstransit-events, tenant-isolation]
max-turns: 60
```

Each agent reads root `CLAUDE.md`, then `.claude/contracts.md`,
then its task entry — in that order, every time.

### Skills
Reusable patterns under `skills/<name>/SKILL.md`. Auto-loaded by any
agent whose frontmatter lists the skill. Some ship with executable
checks (`backend-dev/check-patterns.sh`,
`tenant-isolation/audit.sh`).

### Commands
`commands/<name>.md` becomes the slash command `/<name>`.
`$ARGUMENTS` is replaced with whatever the user typed after the slash.

### Orchestrator
`orchestrate.py` is a plain DAG runner. Knobs per task:
`timeout_minutes`, `acceptance` commands, `files` (ownership lock),
`depends_on`. It enforces:
- per-task timeout (kills at deadline)
- one retry on transient exit codes (124/137/143)
- contract hash lock (any task that mutated `contracts.md` fails)
- cascade-skip for tasks whose deps failed

---

## Conventions

- **Branch names**: `feat/<slug>-T<id>` — keeps merges traceable to the task graph.
- **Worktrees**: `.claude-worktrees/T<id>-<short-name>` — kept after the run for diffing; safe to `rm -rf` once merged.
- **Per-worktree artifacts**: `DONE.md` (impl summary), `MIGRATION.md` (db-migrator), `EVENT-CONTRACT.md` (events-architect), `BUG.md` (haiku-helper aborts), `agent.attemptN.log`.
- **Phase gates**: master tags any Phase 3+ task with `[FeatureGate]` and ensures the flag defaults `false`.

## Anti-patterns the system prevents

- Two agents editing the same file → master rejects the plan
- Consumer built before event locked → must depend on `events-architect`
- Frontend built against an unlocked endpoint → must depend on `gateway-ops`
- A subagent rewriting `contracts.md` mid-run → orchestrator fails the task
- Cross-tenant data leak landing in `main` → `tenant-isolation/audit.sh`
  + reviewer block
- Direct HTTP between services → `backend-dev/check-patterns.sh` blocks

## Tuning

- **Cost cap**: lower `max-turns` on each agent, or downgrade non-critical agents to `claude-haiku-4-5` in `orchestrate.py`'s `MODEL_MAP`.
- **Parallelism cap**: master's `task-planner` skill targets ≤ 5 parallel tasks; raise/lower in `skills/task-planner/SKILL.md`.
- **Stricter sandbox**: tighten `permissions.allow` in `settings.json`; add hooks for required formatters.

## Troubleshooting

- **"Missing .claude/task-graph.json"** → run `/plan` first.
- **"contract-mutation"** in run summary → a subagent edited
  `contracts.md`. Inspect that worktree's `agent.attemptN.log`. Fix
  the agent's prompt or split the task.
- **Same task fails twice** → not retried. Read its log; usually the
  spec was ambiguous or the contract under-specified.
- **Worktree exists** error from git → orchestrator skips re-creating
  but the branch may already exist from a prior run. Delete the
  worktree (`git worktree remove <path>`) and the branch
  (`git branch -D <name>`) before re-running.
