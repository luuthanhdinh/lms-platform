Run tests with coverage and surface gaps: $ARGUMENTS

Default: backend coverage via coverlet. `$ARGUMENTS` can scope to
a service: `/coverage course`.

1. Backend:
   ```bash
   dotnet test tests/LMS.IntegrationTests \
     --collect:"XPlat Code Coverage" \
     --results-directory .coverage \
     -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
   ```
   If `$ARGUMENTS` given, append `--filter FullyQualifiedName~{Name}`.
2. Frontend (if `$ARGUMENTS` is `frontend` or empty):
   ```bash
   cd frontend && pnpm test --coverage
   ```

3. Parse the coverage output and surface **uncovered scenarios**,
   NOT a percent number. The number is noise; gaps are signal.

Output:
- Files with 0% coverage (add tests OR justify exclusion)
- Public methods uncovered in otherwise-covered files
- Branches not exercised (especially error paths in service handlers)
- Tenant-isolation test pairs MISSING for any endpoint touched in
  `git diff main...HEAD` (cross-check with `tenant-isolation` skill)

Do NOT fail on percent thresholds. Do NOT auto-add empty tests
just to lift the number. Print, let the human decide what's worth
covering.
