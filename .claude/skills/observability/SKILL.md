---
name: observability
description: >
  OpenTelemetry traces/metrics/logs with tenant + user tags;
  structured logging conventions; health endpoints.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## Tracing

- All traces auto-tagged with `tenant.id`, `user.id`, `service.name`
  via `LMS.ServiceDefaults` enricher (read header in middleware)
- HTTP, EF Core, MassTransit, HttpClient instrumentations enabled in
  ServiceDefaults — services don't re-add them
- Custom spans for: LLM call, PDF render, cross-service saga step

## Logging

- `ILogger<T>` only — no `Console.WriteLine`
- Structured: `_log.LogInformation("Course {CourseId} published", id);`
- Add scopes for cross-cutting context:
  ```csharp
  using (_log.BeginScope(new Dictionary<string,object>{
      ["TenantId"] = _tenant.Id, ["UserId"] = _user.Id }))
  ```
- Never log: JWTs, full request bodies with PII, raw LLM prompts at info+

## Metrics

- Counters: `lms.events.published`, `lms.events.consumed`,
  `lms.llm.calls`, `lms.pdf.rendered`, `lms.payment.required`
- Histograms: request duration (auto), LLM latency, PDF render time
- All metrics tagged with `tenant.id` (cardinality OK for tenant
  count < 10k; revisit if larger)

## Health

Three endpoints via ServiceDefaults: `/health/live`, `/health/ready`,
`/health/startup`. Ready checks ping Postgres + Rabbit + Redis.
