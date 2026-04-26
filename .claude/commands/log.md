Tail Aspire / service logs filtered by service: $ARGUMENTS

Argument: `<service-short-name> [filter]` (e.g. `course`,
`gateway error`, `notification` `tenant=abc`).

Sources (prefer in this order):

1. **Aspire dashboard** — if AppHost is running, the dashboard at
   `http://localhost:18888` exposes per-resource logs. Print the
   direct URL: `http://localhost:18888/structuredlogs?resource={name}-api`
2. **OTEL exporter** — if Aspire is configured with a console
   exporter, parse `.aspnet/logs/{service}.log` (best-effort path).
3. **dotnet run output** — if the service is running standalone via
   `dotnet run`, redirect to a known log file and `tail -f` it.

Filter rules:
- Default: last 200 lines, levels `Information+`
- `error` → `Warning+` only
- `tenant=<id>` → grep for the tenant id (logs include
  `TenantId` scope per the `observability` skill)
- `trace=<traceId>` → cross-service follow via OTEL trace id

NEVER print log lines containing what looks like a JWT
(`eyJ...`), API key (`sk-ant-`), or password. Redact and warn.

This command does NOT start the AppHost — it only reads. If
nothing is running, print the start command:
`dotnet run --project src/LMS.AppHost`.
