Generate an EF Core migration via the db-migrator agent: $ARGUMENTS

Arguments format: `<service-short-name> <MigrationName>`
Example: `/migrate course AddCoursePublishedAt`

The agent will:

1. Locate `src/services/LMS.{ServiceName}Service`
2. `dotnet ef migrations add <MigrationName>` against that project
3. Inspect Up/Down for destructive ops; convert to expand/contract
4. Add backfill SQL if a NOT NULL column needs data
5. Round-trip: remove → re-add → diff must be empty
6. Apply locally against the Aspire Postgres
7. Write `MIGRATION.md` in the worktree

STOP after the migration is generated. Do not commit.
