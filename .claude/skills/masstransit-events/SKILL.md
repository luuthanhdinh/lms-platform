---
name: masstransit-events
description: >
  MassTransit publish/consume, idempotency, outbox, sagas, retry +
  poison handling. Apply to any code under LMS.Contracts or any
  IConsumer / IPublishEndpoint usage.
allowed-tools: Read, Write, Edit, Bash, Grep, Glob
user-invocable: false
---

## Event record convention

```csharp
namespace LMS.Contracts.Courses;

/// <summary>Course state transitioned to Published.</summary>
public sealed record CoursePublished(
    Guid EventId,
    Guid TenantId,
    Guid CourseId,
    DateTime OccurredAt);
```

- Past tense, `<Aggregate><Verb>ed`
- `EventId`, `TenantId`, `OccurredAt` are mandatory
- Sealed record, immutable
- Versioning: never repurpose — add `CoursePublishedV2`

## Publisher

```csharp
await _publish.Publish(new CoursePublished(
    EventId: Guid.NewGuid(),
    TenantId: _tenant.Id,
    CourseId: course.Id,
    OccurredAt: _clock.UtcNow));
```

## Outbox

When a single command both writes DB state and publishes events:

```csharp
services.AddMassTransit(x => {
    x.AddEntityFrameworkOutbox<CourseDbContext>(o => {
        o.UsePostgres();
        o.UseBusOutbox();
    });
});
```

## Consumer (idempotent)

```csharp
public sealed class SeedProgressOnPublish
    : IConsumer<CoursePublished>
{
    public async Task Consume(ConsumeContext<CoursePublished> ctx)
    {
        // MassTransit inbox handles duplicate EventId at the framework
        // level. App-level idempotency: check before write.
        var exists = await _db.Progress.AnyAsync(p =>
            p.CourseId == ctx.Message.CourseId &&
            p.TenantId == ctx.Message.TenantId);
        if (exists) return;
        // ...
    }
}
```

## Saga rules

- > 2 steps, OR has compensation → use `MassTransitStateMachine<T>`
- State entity is a `TenantEntity` like everything else
- Persist via EF Core repository, not in-memory

## Retry / redelivery / poison

```csharp
cfg.UseMessageRetry(r => r.Intervals(100, 500, 2000));
cfg.UseDelayedRedelivery(r => r.Intervals(
    TimeSpan.FromMinutes(1),
    TimeSpan.FromMinutes(15),
    TimeSpan.FromHours(1)));
// Faults end up in <queue>_error — alert on depth
```
