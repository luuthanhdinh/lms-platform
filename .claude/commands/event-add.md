Add a new MassTransit event via the events-architect agent: $ARGUMENTS

Argument format: `<EventName> [publisher-service] [consumer-service,...]`
Example: `/event-add CoursePublished course progress,notification`

The events-architect agent will:

1. Create the record in
   `src/LMS.Contracts/<Domain>/<EventName>.cs` with the mandatory
   fields (`EventId`, `TenantId`, `OccurredAt`) plus payload from
   `.claude/contracts.md` if locked, otherwise prompt for fields.
2. Register `AddConsumer<T>` in each consuming service (kebab-case
   endpoint name).
3. Set retry + redelivery policies per ADR.
4. Enable the EF Core outbox on the publisher's DbContext if not
   already on.
5. Add the row to `docs/events.md`.
6. Write `EVENT-CONTRACT.md` in the worktree summarizing the
   contract, ordering guarantees, and failure mode.

STOP after the event + topology is in place. Implementing the
publisher/consumer logic is a separate task — schedule via `/plan`
or hand-write following the `masstransit-events` skill.
