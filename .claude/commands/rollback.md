Produce a rollback plan for the last release / migration / deploy: $ARGUMENTS

Argument: `migration <ServiceShortName>` | `release [tag]` | `flag <FlagName>`.

## migration <Service>

1. Locate the latest migration in
   `src/services/LMS.{Name}Service/LMS.{Name}Service.Infrastructure/Migrations/`.
2. Inspect `Down()`:
   - If `Down()` is empty or destructive, write a **forward
     compensating migration** instead — never run a destructive
     `Down()` against shared data
   - Otherwise document `dotnet ef database update <PreviousMigrationName>`
     via the Migrator project
3. Output `ROLLBACK.md`:
   - Migration name + applied date
   - Tables/columns affected
   - Step-by-step rollback (commands + verification queries)
   - Data-loss risk (none/some/significant)
   - Compensating-migration draft if `Down()` is unsafe

## release [tag]

1. `git log <tag>..HEAD --oneline` — what shipped
2. Identify migrations, feature flags, event-contract changes in
   that range
3. Output `ROLLBACK.md` with:
   - Revert commit list (in reverse order)
   - Migrations to roll back (use the section above)
   - Flags to flip OFF
   - Event-contract regressions (any consumer expecting V2 fields
     after a V1 redeploy?)
   - Smoke checks post-rollback

## flag <FlagName>

1. `grep -rn 'FeatureGate("<FlagName>")' src/`
2. Generate the appsettings patch (per environment) flipping it off
3. Note any state created behind the flag that needs cleanup

NEVER auto-execute rollback. Print the plan; the human runs it.
