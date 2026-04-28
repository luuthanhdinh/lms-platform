# Architecture Reference

Read this before: scaffolding the solution, touching AppHost, modifying the gateway,
or implementing cross-cutting concerns (multi-tenancy, messaging, auth).

---

## AppHost — `LMS.AppHost/Program.cs`

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var postgres  = builder.AddPostgres("postgres").WithPgAdmin();
var mongo     = builder.AddMongoDB("mongo");
var redis     = builder.AddRedis("redis");
var rabbitmq  = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();
var keycloak  = builder.AddKeycloakContainer("keycloak")
                       .WithRealmImport("./keycloak/lms-realm.json");

// Databases
var identityDb    = postgres.AddDatabase("lms-identity");
var courseDb      = postgres.AddDatabase("lms-courses");
var contentDb     = mongo.AddDatabase("lms-content");
var enrollmentDb  = postgres.AddDatabase("lms-enrollments");
var progressDb    = postgres.AddDatabase("lms-progress");
var assessmentDb  = postgres.AddDatabase("lms-assessment");
var certificateDb = postgres.AddDatabase("lms-certificate");

// Storage
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var contentBlobs = storage.AddBlobs("content-blobs");
var certificatePdfs = storage.AddBlobs("certificate-pdfs");

// Gateway — JWT validation, rate limiting, header forwarding
var gateway = builder.AddProject<Projects.LMS_Gateway>("gateway")
    .WithReference(keycloak).WithReference(redis)
    .WaitFor(keycloak).WaitFor(redis);

// IdentityService
var identityMigrator = builder.AddProject<Projects.LMS_IdentityService_Migrator>("identity-migrator")
    .WithReference(identityDb).WaitFor(identityDb);
var identity = builder.AddProject<Projects.LMS_IdentityService_Api>("identity")
    .WithReference(identityDb).WithReference(rabbitmq).WithReference(keycloak)
    .WaitForCompletion(identityMigrator);
gateway.WithReference(identity);

// CourseService
var courseMigrator = builder.AddProject<Projects.LMS_CourseService_Migrator>("course-migrator")
    .WithReference(courseDb).WaitFor(courseDb);
var course = builder.AddProject<Projects.LMS_CourseService_Api>("courses")
    .WithReference(courseDb).WithReference(rabbitmq)
    .WaitForCompletion(courseMigrator);
gateway.WithReference(course);

// ContentService + ContentService.Worker
var content = builder.AddProject<Projects.LMS_ContentService_Api>("content")
    .WithReference(contentDb).WithReference(rabbitmq).WithReference(contentBlobs)
    .WaitFor(contentDb).WaitFor(contentBlobs).WaitFor(rabbitmq);
var contentWorker = builder.AddProject<Projects.LMS_ContentService_Worker>("content-worker")
    .WithReference(contentDb).WithReference(rabbitmq).WithReference(contentBlobs)
    .WaitFor(content);
gateway.WithReference(content);

// EnrollmentService
var enrollmentMigrator = builder.AddProject<Projects.LMS_EnrollmentService_Migrator>("enrollment-migrator")
    .WithReference(enrollmentDb).WaitFor(enrollmentDb);
var enrollment = builder.AddProject<Projects.LMS_EnrollmentService_Api>("enrollment")
    .WithReference(enrollmentDb).WithReference(rabbitmq)
    .WaitForCompletion(enrollmentMigrator);
gateway.WithReference(enrollment);

// ProgressService
var progressMigrator = builder.AddProject<Projects.LMS_ProgressService_Migrator>("progress-migrator")
    .WithReference(progressDb).WaitFor(progressDb);
var progress = builder.AddProject<Projects.LMS_ProgressService_Api>("progress")
    .WithReference(progressDb).WithReference(rabbitmq)
    .WaitForCompletion(progressMigrator);
gateway.WithReference(progress);

// AssessmentService
var assessmentMigrator = builder.AddProject<Projects.LMS_AssessmentService_Migrator>("assessment-migrator")
    .WithReference(assessmentDb).WaitFor(assessmentDb);
var assessment = builder.AddProject<Projects.LMS_AssessmentService_Api>("assessment")
    .WithReference(assessmentDb).WithReference(rabbitmq).WithReference(redis)
    .WaitForCompletion(assessmentMigrator);
gateway.WithReference(assessment);

// CertificateService
var certificateMigrator = builder.AddProject<Projects.LMS_CertificateService_Migrator>("certificate-migrator")
    .WithReference(certificateDb).WaitFor(certificateDb);
var certificate = builder.AddProject<Projects.LMS_CertificateService_Api>("certificate")
    .WithReference(certificateDb).WithReference(rabbitmq).WithReference(certificatePdfs)
    .WaitForCompletion(certificateMigrator).WaitFor(certificatePdfs);
gateway.WithReference(certificate);

// NotificationWorker — event consumer, sends emails via SMTP/MailHog
var mailhog = builder.AddContainer("mailhog", "mailhog/mailhog", "latest")
    .WithHttpEndpoint(1025, name: "smtp", port: 1025)
    .WithHttpEndpoint(8025, name: "http");
var notifications = builder.AddProject<Projects.LMS_NotificationWorker>("notifications")
    .WithReference(rabbitmq).WithReference(redis).WithReference(mailhog)
    .WaitFor(rabbitmq).WaitFor(redis).WaitFor(mailhog);

// Frontend — React app via Vite dev server
var frontend = builder.AddNpmApp("frontend", "../frontend")
    .WithReference(gateway)
    .WithEnvironment("VITE_API_BASE_URL", gateway.GetEndpoint("http"))
    .WithEnvironment("VITE_KEYCLOAK_URL", keycloak.GetEndpoint("http"))
    .WithEnvironment("VITE_KEYCLOAK_REALM", "lms")
    .WithEnvironment("VITE_KEYCLOAK_CLIENT_ID", "lms-spa")
    .WithHttpEndpoint(5173, name: "http")
    .WithExternalHttpEndpoints();

builder.Build().Run();
```

Phase 2+ resources (uncomment when starting Phase 2 sprint):
```csharp
// var clickhouse    = builder.AddContainer("clickhouse","clickhouse/clickhouse-server","24")
//                           .WithHttpEndpoint(8123, name: "http");
// var elasticsearch = builder.AddContainer("elasticsearch","elasticsearch","8.13.0")
//                            .WithEnvironment("discovery.type","single-node")
//                            .WithHttpEndpoint(9200, name: "http");
// var eventstoredb  = builder.AddContainer("eventstoredb","eventstore/eventstore","23.10")
//                            .WithHttpEndpoint(2113, name: "http");
```

---

## Gateway — JWT Trust Boundary

> Full details: [docs/services/gateway.md](services/gateway.md)

The gateway (`src/gateway/LMS.Gateway/`) is the **only** place JWTs are validated. All downstream services trust the forwarded headers injected by the gateway — they never hold or re-validate a JWT.

### Forwarded header contract (immutable — all services depend on this)

| Header | Source | When present |
|---|---|---|
| `X-User-Id` | JWT `sub` claim | Authenticated requests |
| `X-Tenant-Id` | JWT `tenant_id` custom claim | Authenticated requests |
| `X-Roles` | JWT `realm_access.roles[]`, comma-joined, no spaces | Authenticated requests |
| `X-Correlation-Id` | Inbound or generated GUID | Always |

**Inbound forgery prevention:** The gateway strips `X-User-Id`, `X-Tenant-Id`, `X-Roles`, and `Authorization` from every outbound proxied request before injecting trusted values from the validated JWT principal. A client cannot inject trusted headers.

### Downstream auth pattern

Every downstream service reads identity from forwarded headers — never from `HttpContext.User` or a JWT:

```csharp
// LMS.SharedKernel/Middleware/TenantMiddleware.cs — reads X-Tenant-Id
// Endpoints read X-User-Id, X-Roles directly from request headers
// No AddAuthentication / AddJwtBearer in any service — only in the gateway
```

### Anonymous route

Only `/verify/{**rest}` (certificate verification link) is anonymous. All other 7 routes require a valid JWT.

---

## ServiceDefaults — `LMS.ServiceDefaults/Extensions.cs`

```csharp
public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(
        this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });
        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready",
            new() { Predicate = r => r.Tags.Contains("ready") });
        return app;
    }
}
```

Every `Program.cs` must call:
```csharp
builder.AddServiceDefaults();   // at top
app.MapDefaultEndpoints();      // before app.Run()
```

---

## Multi-Tenancy Pattern

### Base entity — every DB entity must inherit this

```csharp
// LMS.SharedKernel/Entities/TenantEntity.cs
public abstract class TenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

### Global query filter — apply in every DbContext

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    foreach (var type in modelBuilder.Model.GetEntityTypes()
        .Where(e => typeof(TenantEntity).IsAssignableFrom(e.ClrType)))
    {
        var param = Expression.Parameter(type.ClrType, "e");
        var prop  = Expression.Property(param, nameof(TenantEntity.TenantId));
        var val   = Expression.Property(Expression.Constant(this),
                        nameof(CurrentTenantId));
        modelBuilder.Entity(type.ClrType)
            .HasQueryFilter(Expression.Lambda(Expression.Equal(prop, val), param));
    }
}

public Guid CurrentTenantId { get; set; }  // set by TenantMiddleware
```

### Tenant middleware — register in every service

```csharp
// LMS.SharedKernel/Middleware/TenantMiddleware.cs
public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, AppDbContext db)
    {
        if (ctx.Request.Headers.TryGetValue("X-Tenant-Id", out var v)
            && Guid.TryParse(v, out var g))
            db.CurrentTenantId = g;
        await next(ctx);
    }
}
```

```csharp
// Program.cs
app.UseMiddleware<TenantMiddleware>();
```

---

## Gateway — JWT & Header Enrichment

```csharp
// LMS.Gateway/Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Keycloak:Authority"];
        o.Audience  = builder.Configuration["Keycloak:Audience"];
        o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

// CORS — allow React dev server and production frontend origin
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins")
                             .Get<string[]>() ?? ["http://localhost:5173"];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // required for Keycloak cookie flows
    });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(ctx =>
    {
        ctx.AddRequestTransform(async t =>
        {
            if (t.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var user = t.HttpContext.User;
                t.ProxyRequest.Headers.TryAddWithoutValidation(
                    "X-User-Id", user.FindFirst("sub")?.Value ?? "");
                t.ProxyRequest.Headers.TryAddWithoutValidation(
                    "X-Tenant-Id", user.FindFirst("tenant_id")?.Value ?? "");
                t.ProxyRequest.Headers.TryAddWithoutValidation(
                    "X-Roles", string.Join(",",
                        user.FindAll(ClaimTypes.Role).Select(c => c.Value)));
            }
        });
    });
```

YARP routes (`appsettings.json`):
```json
{
  "ReverseProxy": {
    "Routes": {
      "identity":    { "ClusterId": "identity",    "Match": { "Path": "/api/identity/{**rest}" } },
      "courses":     { "ClusterId": "courses",     "Match": { "Path": "/api/courses/{**rest}" } },
      "content":     { "ClusterId": "content",     "Match": { "Path": "/api/content/{**rest}" } },
      "enrollment":  { "ClusterId": "enrollment",  "Match": { "Path": "/api/enrollments/{**rest}" } },
      "progress":    { "ClusterId": "progress",    "Match": { "Path": "/api/progress/{**rest}" } },
      "assessment":  { "ClusterId": "assessment",  "Match": { "Path": "/api/assessments/{**rest}" } },
      "certificate": { "ClusterId": "certificate", "Match": { "Path": "/api/certificates/{**rest}" } },
      "verify":      { "ClusterId": "certificate", "Match": { "Path": "/verify/{**rest}" },
                       "AuthorizationPolicy": "anonymous" }
    },
    "Clusters": {
      "identity":    { "Destinations": { "d1": { "Address": "http://identity" } } },
      "courses":     { "Destinations": { "d1": { "Address": "http://courses" } } },
      "content":     { "Destinations": { "d1": { "Address": "http://content" } } },
      "enrollment":  { "Destinations": { "d1": { "Address": "http://enrollment" } } },
      "progress":    { "Destinations": { "d1": { "Address": "http://progress" } } },
      "assessment":  { "Destinations": { "d1": { "Address": "http://assessment" } } },
      "certificate": { "Destinations": { "d1": { "Address": "http://certificate" } } }
    }
  }
}
```

### HttpContext helpers — use in all services instead of parsing headers manually

```csharp
// LMS.SharedKernel/Extensions/HttpContextExtensions.cs
public static class HttpContextExtensions
{
    public static Guid GetUserId(this HttpContext ctx) =>
        Guid.Parse(ctx.Request.Headers["X-User-Id"].ToString());
    public static Guid GetTenantId(this HttpContext ctx) =>
        Guid.Parse(ctx.Request.Headers["X-Tenant-Id"].ToString());
    public static string[] GetRoles(this HttpContext ctx) =>
        ctx.Request.Headers["X-Roles"].ToString().Split(',');
    public static bool IsInRole(this HttpContext ctx, string role) =>
        ctx.GetRoles().Contains(role);
}
```

---

## MassTransit Setup — every consumer service

```csharp
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumers(Assembly.GetExecutingAssembly()); // auto-register all consumers
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("rabbitmq"));
        cfg.UseMessageRetry(r =>
        {
            r.Immediate(3);
            r.Interval(3, TimeSpan.FromSeconds(30));
        });
        cfg.UseInMemoryOutbox(ctx);
        cfg.ConfigureEndpoints(ctx);
    });
});
```

---

## Shared Kernel Types

```csharp
// Result<T> — use instead of throwing exceptions for expected failures
public record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public static Result<T> Ok(T v) => new() { IsSuccess = true, Value = v };
    public static Result<T> Fail(string err, string code = "ERROR") =>
        new() { IsSuccess = false, Error = err, ErrorCode = code };
}

// Pagination
public record PagedResult<T>(IEnumerable<T> Data, PaginatedMeta Meta);
public record PaginatedMeta(int Page, int PageSize, int TotalCount, int TotalPages);

// Standard API error responses
public record ErrorResponse(string Code, string Message,
    IEnumerable<FieldError>? Details = null, string? TraceId = null);
public record FieldError(string Field, string Message);
```

---

## Program.cs Template — copy for every new service

```csharp
using LMS.ServiceDefaults;
using LMS.{Name}Service.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<AppDbContext>("lms_{dbname}");  // or AddMongoDBClient for ContentService

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumers(Assembly.GetExecutingAssembly());
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("rabbitmq"));
        cfg.UseMessageRetry(r => { r.Immediate(3); r.Interval(3, TimeSpan.FromSeconds(30)); });
        cfg.UseInMemoryOutbox(ctx);
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddFeatureManagement();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors("frontend");       // must be before UseAuthentication
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>()
               .Database.MigrateAsync();
}

app.Map{Name}Endpoints();   // defined in /Endpoints folder
app.MapDefaultEndpoints();
app.Run();
```

---

## .csproj Template — copy for every new service

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\LMS.ServiceDefaults\LMS.ServiceDefaults.csproj" />
    <ProjectReference Include="..\..\LMS.SharedKernel\LMS.SharedKernel.csproj" />
    <ProjectReference Include="..\..\LMS.Contracts\LMS.Contracts.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.*" />
    <PackageReference Include="MassTransit.RabbitMQ" Version="8.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.*" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="7.*" />
    <PackageReference Include="Microsoft.FeatureManagement.AspNetCore" Version="4.*" />
  </ItemGroup>
</Project>
```

---

## Keycloak — Realm `lms`

- Import file: `./keycloak/lms-realm.json`
- Custom claim `tenant_id` (string, UUID) mapped from user attribute
- Roles: `student` · `instructor` · `admin` · `org-admin` · `curator`
- MFA: Required (`CONFIGURE_TOTP`) for `instructor`, `admin`, `org-admin` on first login
- Token lifespan: access = 15 min · refresh = 30 days

### Dev admin credentials

Aspire generates a random admin password each time the Keycloak container is recreated.
Retrieve the current credentials with:

```bash
docker inspect $(docker ps -qf "name=keycloak") \
  | python3 -c "import sys,json; env=json.load(sys.stdin)[0]['Config']['Env']; [print(e) for e in env if 'ADMIN' in e]"
```

Output example:
```
KC_BOOTSTRAP_ADMIN_USERNAME=admin
KC_BOOTSTRAP_ADMIN_PASSWORD=5Muw2u6XhHyQs_r!geb)-a
```

Then log in at `http://localhost:<keycloak-port>/admin` (check the Aspire dashboard for the current port).

---

## Feature Flags — `appsettings.json` defaults

```json
{
  "FeatureManagement": {
    "Gamification": false,
    "LiveSessions": false,
    "Forums": false,
    "Payments": false,
    "AiTutor": false,
    "Marketplace": false,
    "PeerReview": false,
    "SkillsModule": false,
    "GdprModule": false,
    "WhiteLabel": false,
    "MandatoryTraining": false,
    "VirtualLabs": false,
    "BlockchainAnchoring": false,
    "OpenBadges3": false,
    "EngagementTracking": false,
    "AiTranslation": false
  }
}
```

---

## Environment Variables

```bash
# Shared by all services
ConnectionStrings__rabbitmq=amqp://guest:guest@localhost:5672
ConnectionStrings__redis=localhost:6379
Keycloak__Authority=http://localhost:8080/realms/lms
Keycloak__Audience=lms-api

# Per service (example)
ConnectionStrings__lms_identity=Host=localhost;Database=lms_identity;Username=postgres;Password=postgres

# ContentService
Aws__BucketName=lms-content-dev
Aws__Region=ap-southeast-1
Aws__CloudFrontDomain=https://cdn.lms.local

# Phase 2 — LLM
Anthropic__ApiKey=sk-ant-...
Anthropic__Model=claude-sonnet-4-5

# Phase 3 — Payments
Stripe__SecretKey=sk_test_...
Stripe__WebhookSecret=whsec_...
```

---

## Scaffold Sequence

```bash
dotnet new sln -n LMS
dotnet new classlib  -n LMS.Contracts       -o src/LMS.Contracts
dotnet new classlib  -n LMS.SharedKernel    -o src/LMS.SharedKernel
dotnet new aspire-servicedefaults -n LMS.ServiceDefaults -o src/LMS.ServiceDefaults
dotnet new aspire-apphost         -n LMS.AppHost         -o src/LMS.AppHost
dotnet new web    -n LMS.Gateway            -o src/gateway/LMS.Gateway
dotnet new web    -n LMS.IdentityService    -o src/services/LMS.IdentityService
dotnet new web    -n LMS.CourseService      -o src/services/LMS.CourseService
dotnet new web    -n LMS.ContentService     -o src/services/LMS.ContentService
dotnet new web    -n LMS.EnrollmentService  -o src/services/LMS.EnrollmentService
dotnet new web    -n LMS.ProgressService    -o src/services/LMS.ProgressService
dotnet new web    -n LMS.AssessmentService  -o src/services/LMS.AssessmentService
dotnet new web    -n LMS.CertificateService -o src/services/LMS.CertificateService
dotnet new worker -n LMS.NotificationWorker -o src/services/LMS.NotificationWorker
find . -name "*.csproj" | xargs dotnet sln add

# Frontend — scaffold React app
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install \
  @tanstack/react-query @tanstack/react-router \
  keycloak-js axios \
  zustand \
  react-hook-form @hookform/resolvers zod \
  tailwindcss @tailwindcss/vite \
  lucide-react
npx shadcn@latest init
```
