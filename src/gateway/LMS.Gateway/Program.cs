using LMS.Gateway.Cors;
using LMS.Gateway.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// YARP — load config and register the header-forwarding transform
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms<LMS.Gateway.Transforms.HeaderForwardingTransform>()
    .AddServiceDiscoveryDestinationResolver();

// JWT auth — Keycloak
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"] ?? "lms-api";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new()
        {
            ValidateAudience = true,
            RoleClaimType = ClaimTypes.Role,
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                // Flatten Keycloak realm_access.roles[] into ClaimTypes.Role claims
                var realmAccess = ctx.Principal?.FindFirstValue("realm_access");
                if (realmAccess is not null)
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(realmAccess);
                    if (doc.RootElement.TryGetProperty("roles", out var rolesEl))
                    {
                        var claims = rolesEl.EnumerateArray()
                            .Select(r => new Claim(ClaimTypes.Role, r.GetString() ?? string.Empty))
                            .Where(c => !string.IsNullOrEmpty(c.Value))
                            .ToList();
                        var identity = ctx.Principal!.Identity as ClaimsIdentity;
                        identity?.AddClaims(claims);
                    }
                }
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync(
                    """{"code":"UNAUTHENTICATED","message":"Authentication required"}""");
            },
            OnForbidden = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync(
                    """{"code":"FORBIDDEN","message":"Insufficient permissions"}""");
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // YARP reserves the name "anonymous" internally — do NOT register a custom policy with that name.
    // Routes with AuthorizationPolicy: "anonymous" in appsettings are handled by YARP natively
    // (they allow unauthenticated requests without consulting ASP.NET Core policy engine).
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.FallbackPolicy = options.DefaultPolicy;
});

builder.Services.AddGatewayCors(builder.Configuration);
builder.Services.AddGatewayRateLimiting(builder.Configuration);

// Health checks — tagged 'ready' for readiness probe
builder.Services.AddHttpClient("jwks-health")
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        if (builder.Environment.IsDevelopment())
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        return handler;
    });

builder.Services.AddHealthChecks()
    .AddCheck<LMS.Gateway.Health.JwksHealthCheck>(
        "jwks", tags: ["ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("redis") ?? "localhost:6379",
        name: "redis",
        tags: ["ready"]);

var app = builder.Build();

// Middleware order: CORS → auth → rate limiting → proxy
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

var rateLimitOpts = builder.Configuration.GetSection("RateLimit").Get<RateLimitOptions>() ?? new();
if (rateLimitOpts.Enabled)
    app.UseRateLimiter();

app.MapDefaultEndpoints();  // health probes — already .AllowAnonymous() via ServiceDefaults
app.MapReverseProxy();

app.Run();

// Expose Program to WebApplicationFactory<Program> in integration tests
public partial class Program { }
