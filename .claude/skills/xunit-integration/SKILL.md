---
name: xunit-integration
description: >
  xUnit + WebApplicationFactory + Testcontainers integration test
  patterns for LMS services. Includes Keycloak stub and MassTransit
  test harness.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## Fixture (per-collection, shared containers)

```csharp
public sealed class LmsFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } =
        new PostgreSqlBuilder().Build();
    public RabbitMqContainer Rabbit { get; } =
        new RabbitMqBuilder().Build();
    public RedisContainer Redis { get; } = new RedisBuilder().Build();

    public async Task InitializeAsync() {
        await Task.WhenAll(Postgres.StartAsync(),
                          Rabbit.StartAsync(),
                          Redis.StartAsync());
    }
    public async Task DisposeAsync() {
        await Postgres.DisposeAsync();
        await Rabbit.DisposeAsync();
        await Redis.DisposeAsync();
    }
}
```

## Factory

`WebApplicationFactory<Program>` overrides connection strings to
the Testcontainers + replaces `IClock` with a fake.

## Auth stub

Add a fake JWT bearer scheme in test DI that accepts any token
and produces a `ClaimsPrincipal` with `tenant_id`, `sub`, roles —
mirrors what YARP would forward.

```csharp
client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantA.ToString());
client.DefaultRequestHeaders.Add("X-User-Id",   userA.ToString());
client.DefaultRequestHeaders.Add("X-Roles",     "Instructor");
```

## MassTransit harness for cross-service flows

```csharp
services.AddMassTransitTestHarness(x => {
    x.AddConsumer<SeedProgressOnPublish>();
});
// in the test:
await harness.Bus.Publish(new CoursePublished(...));
Assert.True(await harness.Consumed.Any<CoursePublished>());
```

## Tenant pair test (mandatory for every endpoint)

```csharp
await PostAs(tenantA, ...);
var get = await GetAs(tenantB, idFromA);
get.StatusCode.Should().Be(HttpStatusCode.NotFound); // not 403
```
