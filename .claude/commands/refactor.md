Targeted refactor with safety net: $ARGUMENTS

`$ARGUMENTS` should name the smell + scope
(e.g. "extract publish flow from CourseEndpoints into a service").

Workflow:

1. **Confirm the smell** — read the target; if the code is fine,
   say so and stop. Don't refactor for taste.
2. **Pin behavior with tests FIRST** — if no tests exist for the
   target, write characterization tests covering current behavior,
   commit them before touching production code.
3. **Refactor in small reversible steps** — each step compiles +
   passes tests. No mixed reformat + behavior changes.
4. **Apply LMS rules** — entity moves keep TenantEntity; service
   moves keep `ITenantContext` injection; events stay in
   `LMS.Contracts`; reference graph stays one-way
   (Api → Infra → Domain).
5. **No rename for rename's sake** — only rename if it clarifies
   meaning to a future reader; update all call sites in the same
   commit.
6. **Verify** — `dotnet build`, run focused tests (use `/test`),
   run `bash .claude/skills/backend-dev/check-patterns.sh src` if
   backend changed.
7. Print before/after summary: lines moved, new files, public API
   delta (none expected for pure refactor).

DO NOT add features. DO NOT change behavior. If you discover a bug,
note it (file:line) and stop — fix in a separate `/fix` pass.
