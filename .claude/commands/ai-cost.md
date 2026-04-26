Report LLM token usage and estimated cost: $ARGUMENTS

Argument: `[days]` (default 7) — lookback window.

Sources (try in order):

1. **OTEL metrics** (preferred) — query the local Prometheus /
   metrics endpoint Aspire exposes for `lms.llm.*` counters:
   - `lms.llm.calls{tenant_id, model}` — call count
   - `lms.llm.input_tokens{tenant_id, model, cache_read|cache_creation}`
   - `lms.llm.output_tokens{tenant_id, model}`
2. **Redis quota counters** — keys `t:{tenantId}:llm:tokens:{yyyyMM}`
   give per-tenant monthly totals (set by the `llm-client` skill's
   quota guard).
3. **Anthropic console** — if neither is available, print the URL
   to the Anthropic admin console and stop.

Estimate cost from current Anthropic price sheet (read from
`docs/llm-pricing.md` if present; otherwise prompt user for rates
to avoid stale numbers). Distinguish:
- input tokens (uncached)
- input tokens (cache READ — much cheaper)
- input tokens (cache WRITE — slightly more expensive)
- output tokens

Output table:

```
| Tenant      | Model            | Calls | In(uncached) | In(cache R) | Out  | Cost   |
| ----------- | ---------------- | ----- | ------------ | ----------- | ---- | ------ |
| acme-corp   | sonnet-4-6       |  1.2k |       450k   |      2.3M   | 90k  | $X.XX  |
```

Plus:
- Top 5 tenants by cost
- Cache-hit ratio per model — flag if < 50% (prompt-caching opportunity)
- Tenants approaching their monthly quota (warn at 80%)

Read-only. Don't change quotas — that's a separate config change.
