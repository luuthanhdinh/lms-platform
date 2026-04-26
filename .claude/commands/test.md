Run tests focused on current changes: $ARGUMENTS

Default behavior (no args):

1. `git diff main...HEAD --name-only` to find changed files.
2. Map each changed file to its test target:
   - `src/services/LMS.{Name}Service/...` → run
     `dotnet test tests/LMS.IntegrationTests --filter FullyQualifiedName~{Name}`
   - `src/LMS.Contracts/...` → run `dotnet test tests/LMS.ContractTests`
   - Any entity / DbContext change → also run `LMS.ArchitectureTests`
   - `frontend/src/features/{area}/...` → `cd frontend && pnpm test {area}`
3. Run them in parallel where possible.
4. If any test fails, print the failure with file:line and the
   smallest repro command. Do NOT auto-fix.

Args:
- `all` → full `dotnet test` + `pnpm test`
- `tenant` → only tenant-isolation tests
- `watch` → run frontend tests in watch mode (foreground)
- `<filter>` → forward as `--filter` to dotnet / `pnpm test <filter>`
