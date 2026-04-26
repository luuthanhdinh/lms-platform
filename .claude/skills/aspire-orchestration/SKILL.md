---
name: aspire-orchestration
description: >
  .NET Aspire AppHost wiring for LMS: resources, references,
  WaitFor / WaitForCompletion, the Migrator-before-Api pattern,
  service discovery, and ServiceDefaults conventions.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## AppHost responsibilities

`LMS.AppHost` orchestrates only — never holds business logic.
It declares **resources** (Postgres/Redis/Rabbit/Mongo/Keycloak)
and **projects** (services, workers, frontend), then wires
references and ordering.

## Resource graph (current shape)

```csharp
var pg     = builder.AddPostgres("pg")
                    .WithDataVolume()           // local persistence
                    .WithPgAdmin();             // dev-only UI
var redis  = builder.AddRedis("redis").WithRedisInsight();
var rabbit = builder.AddRabbitMQ("rabbit").WithManagementPlugin();
var mongo  = builder.AddMongoDB("mongo");       // ContentService only
var kc     = builder.AddKeycloak("keycloak", 8080)
                    .WithRealmImport("./realms");

var coursesDb  = pg.AddDatabase("courses");
var enrollDb   = pg.AddDatabase("enrollment");
// ...one DB per relational service
```

## Migrator-before-Api pattern (mandatory for every relational service)

```csharp
var courseMigrator = builder
    .AddProject<Projects.LMS_CourseService_Migrator>("course-migrator")
    .WithReference(coursesDb)
    .WaitFor(coursesDb);

var courseApi = builder
    .AddProject<Projects.LMS_CourseService_Api>("course-api")
    .WithReference(coursesDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitForCompletion(courseMigrator);   // ← API blocks until exit-0
```

Why `WaitForCompletion` (not `WaitFor`):
- `WaitFor` = depends on resource being **ready** (still running)
- `WaitForCompletion` = depends on resource having **finished**
  (exit code 0). The Migrator is a one-shot Worker, so this is
  the correct primitive.

## Frontend resource

```csharp
var frontend = builder.AddNpmApp("frontend", "../../frontend", "dev")
                      .WithReference(gateway)
                      .WithEnvironment("VITE_API_URL", gateway.GetEndpoint("https"))
                      .WithHttpEndpoint(env: "PORT")
                      .PublishAsDockerFile();
```

## Service defaults (always added inside services, not AppHost)

Every service's `Program.cs`:

```csharp
builder.AddServiceDefaults();   // OTEL, health, resilience, service discovery
// ...
app.MapDefaultEndpoints();      // /health/live, /health/ready
```

`ServiceDefaults` is shared — never duplicate OTEL setup per service.

## Connection strings & service discovery

- Connection strings come from Aspire references — never hardcode
- HTTP between services (rare; only via gateway) uses
  `services.AddHttpClient("course", c => c.BaseAddress = new("https+http://course-api"))`
  — Aspire resolves the logical name
- Backend services NEVER call each other directly — MassTransit only.
  Service-discovery for HTTP applies to **gateway → service** only

## Local-dev vs production

- AppHost is local-dev orchestrator + production publish manifest
- For K8s production: same Migrator project ships as a `Job` or
  init-container; same Api ships as a `Deployment`. The
  `WaitForCompletion` becomes a Kubernetes `initContainer` ordering
- Aspire dashboard URL surfaces in console at startup;
  bookmark `http://localhost:18888`

## Rules

- Don't put secrets in AppHost — use `builder.AddParameter("name", secret: true)`
- Don't add a service to AppHost without also adding its Migrator
  (if it's relational)
- Health checks for upstream deps go through ServiceDefaults — services
  shouldn't add bespoke `IHealthCheck` for Postgres/Rabbit/Redis
