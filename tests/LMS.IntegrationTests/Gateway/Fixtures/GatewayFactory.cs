using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LMS.IntegrationTests.Gateway.Fixtures;

/// <summary>
/// WebApplicationFactory for the gateway under test.
/// Overrides JWT validation to use a local symmetric key (no JWKS fetch),
/// disables rate limiting, and points every YARP cluster at a port that
/// returns 502/503 so auth-layer tests are never confused with routing failures.
/// </summary>
public sealed class GatewayFactory : WebApplicationFactory<Program>
{
    public static readonly string TestIssuer   = "http://test-keycloak/realms/lms";
    public static readonly string TestAudience = "lms-api";

    // HS256 symmetric key — adequate for test token generation; avoids RSA key-gen cost.
    private static readonly SymmetricSecurityKey TestSigningKey =
        new(Encoding.UTF8.GetBytes("test-secret-key-that-is-long-enough-for-hs256-algo"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var stubAddress = _stubPort is int port
                ? $"http://127.0.0.1:{port}"
                : "http://localhost:19999";

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = TestIssuer,
                ["Keycloak:Audience"]  = TestAudience,
                ["RateLimit:Enabled"]  = "false",
                ["ConnectionStrings:redis"] = "localhost:6399,abortConnect=false",
                ["ReverseProxy:Clusters:identity:Destinations:d1:Address"]    = stubAddress,
                ["ReverseProxy:Clusters:courses:Destinations:d1:Address"]     = stubAddress,
                ["ReverseProxy:Clusters:content:Destinations:d1:Address"]     = stubAddress,
                ["ReverseProxy:Clusters:enrollment:Destinations:d1:Address"]  = stubAddress,
                ["ReverseProxy:Clusters:progress:Destinations:d1:Address"]    = stubAddress,
                ["ReverseProxy:Clusters:assessment:Destinations:d1:Address"]  = stubAddress,
                ["ReverseProxy:Clusters:certificate:Destinations:d1:Address"] = stubAddress,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.PostConfigureAll<JwtBearerOptions>(options =>
            {
                options.RequireHttpsMetadata = false;
                options.Authority            = null;
                options.MetadataAddress      = null;
                options.ConfigurationManager = null;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer   = true,
                    ValidIssuer      = TestIssuer,
                    ValidateAudience = true,
                    ValidAudience    = TestAudience,
                    ValidateLifetime = true,
                    IssuerSigningKey = TestSigningKey,
                    RoleClaimType    = ClaimTypes.Role,
                    ClockSkew        = TimeSpan.Zero,
                };
            });
        });
    }

    /// <summary>
    /// Generates a signed JWT accepted by <see cref="GatewayFactory"/>.
    /// Roles are embedded as a <c>realm_access</c> JSON claim so that the gateway's
    /// <c>OnTokenValidated</c> handler flattens them into <c>ClaimTypes.Role</c> — exactly
    /// the same path as a real Keycloak token.
    /// </summary>
    public static string GenerateToken(
        string userId   = "user-123",
        string tenantId = "tenant-abc",
        string[]? roles = null,
        bool expired    = false)
    {
        roles ??= ["student"];

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("tenant_id", tenantId),
        };

        // Embed roles in Keycloak's realm_access structure so the gateway transform fires.
        var rolesJson = System.Text.Json.JsonSerializer.Serialize(new { roles });
        claims.Add(new Claim("realm_access", rolesJson, "JSON"));

        var now = DateTime.UtcNow;
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateJwtSecurityToken(
            issuer:             TestIssuer,
            audience:           TestAudience,
            subject:            new ClaimsIdentity(claims),
            notBefore:          expired ? now.AddHours(-2) : now,
            expires:            expired ? now.AddHours(-1) : now.AddHours(1),
            issuedAt:           now,
            signingCredentials: new SigningCredentials(TestSigningKey, SecurityAlgorithms.HmacSha256));

        return handler.WriteToken(token);
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> pre-configured with a valid Bearer token.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(
        string userId   = "user-123",
        string tenantId = "tenant-abc",
        string[]? roles = null)
    {
        var client = CreateClient();
        var token  = GenerateToken(userId, tenantId, roles);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private readonly int? _stubPort;

}
