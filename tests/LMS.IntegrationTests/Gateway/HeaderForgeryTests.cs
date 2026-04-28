using System.Net;
using System.Net.Http.Headers;
using LMS.IntegrationTests.Gateway.Fixtures;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LMS.IntegrationTests.Gateway;

/// <summary>
/// Verifies that inbound trusted headers sent by a client are stripped by the gateway
/// before the request reaches downstream services.
/// </summary>
[Trait("Category", "Gateway")]
public sealed class HeaderForgeryTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public HeaderForgeryTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    // Helper: create a factory clone with all YARP clusters pointing at stubPort
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> WithStub(int stubPort)
        => _factory.WithWebHostBuilder(wb =>
        {
            var addr = $"http://127.0.0.1:{stubPort}";
            wb.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReverseProxy:Clusters:identity:Destinations:d1:Address"]    = addr,
                    ["ReverseProxy:Clusters:courses:Destinations:d1:Address"]     = addr,
                    ["ReverseProxy:Clusters:content:Destinations:d1:Address"]     = addr,
                    ["ReverseProxy:Clusters:enrollment:Destinations:d1:Address"]  = addr,
                    ["ReverseProxy:Clusters:progress:Destinations:d1:Address"]    = addr,
                    ["ReverseProxy:Clusters:assessment:Destinations:d1:Address"]  = addr,
                    ["ReverseProxy:Clusters:certificate:Destinations:d1:Address"] = addr,
                }));
        });

    [Theory]
    [InlineData("X-User-Id",   "forged-user-id")]
    [InlineData("X-Tenant-Id", "forged-tenant-id")]
    [InlineData("X-Roles",     "admin")]
    public async Task ForgedTrustedHeader_OnUnauthenticatedRequest_IsBlockedBy401(
        string headerName, string forgedValue)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(headerName, forgedValue);

        var response = await client.GetAsync("/api/courses/test");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgedXUserId_IsOverwrittenByGateway_WithJwtSubClaim()
    {
        using var stub = new StubBackend();
        using var factory = WithStub(stub.Port);
        var client = factory.CreateClient();
        var token  = GatewayFactory.GenerateToken(userId: "real-user-id", tenantId: "real-tenant");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-User-Id", "forged-user-id");

        var requestTask = client.GetAsync("/api/courses/test");
        var captured    = await stub.WaitForRequestAsync(timeout: TimeSpan.FromSeconds(5));
        await Task.WhenAny(requestTask, Task.Delay(500));

        Assert.NotNull(captured);
        Assert.Equal("real-user-id", captured!["X-User-Id"]);
        Assert.NotEqual("forged-user-id", captured["X-User-Id"]);
    }

    [Fact]
    public async Task ForgedXTenantId_IsOverwrittenByGateway_WithJwtTenantIdClaim()
    {
        using var stub = new StubBackend();
        using var factory = WithStub(stub.Port);
        var client = factory.CreateClient();
        var token  = GatewayFactory.GenerateToken(userId: "user-1", tenantId: "real-tenant-id");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "forged-tenant-id");

        var requestTask = client.GetAsync("/api/courses/test");
        var captured    = await stub.WaitForRequestAsync(timeout: TimeSpan.FromSeconds(5));
        await Task.WhenAny(requestTask, Task.Delay(500));

        Assert.NotNull(captured);
        Assert.Equal("real-tenant-id", captured!["X-Tenant-Id"]);
        Assert.NotEqual("forged-tenant-id", captured["X-Tenant-Id"]);
    }

    [Fact]
    public async Task ForgedXRoles_IsOverwrittenByGateway_WithJwtRolesClaim()
    {
        using var stub = new StubBackend();
        using var factory = WithStub(stub.Port);
        var client = factory.CreateClient();
        var token  = GatewayFactory.GenerateToken(userId: "user-1", tenantId: "t1", roles: ["student"]);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Roles", "admin");

        var requestTask = client.GetAsync("/api/courses/test");
        var captured    = await stub.WaitForRequestAsync(timeout: TimeSpan.FromSeconds(5));
        await Task.WhenAny(requestTask, Task.Delay(500));

        Assert.NotNull(captured);
        Assert.Contains("student", captured!["X-Roles"] ?? string.Empty);
        Assert.DoesNotContain("admin", captured["X-Roles"] ?? string.Empty);
    }

    [Fact]
    public async Task AuthorizationHeader_IsStripped_BeforeForwarding()
    {
        using var stub = new StubBackend();
        using var factory = WithStub(stub.Port);
        var client = factory.CreateClient();
        var token  = GatewayFactory.GenerateToken();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var requestTask = client.GetAsync("/api/courses/test");
        var captured    = await stub.WaitForRequestAsync(timeout: TimeSpan.FromSeconds(5));
        await Task.WhenAny(requestTask, Task.Delay(500));

        Assert.NotNull(captured);
        Assert.False(captured!.ContainsKey("Authorization"),
            "Authorization header must not reach downstream services");
    }

    [Fact]
    public async Task XCorrelationId_IsForwarded_WhenProvidedByClient()
    {
        using var stub = new StubBackend();
        using var factory = WithStub(stub.Port);
        var client        = factory.CreateClient();
        var token         = GatewayFactory.GenerateToken();
        var correlationId = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

        var requestTask = client.GetAsync("/api/courses/test");
        var captured    = await stub.WaitForRequestAsync(timeout: TimeSpan.FromSeconds(5));
        await Task.WhenAny(requestTask, Task.Delay(500));

        Assert.NotNull(captured);
        Assert.Equal(correlationId, captured!["X-Correlation-Id"]);
    }

    [Fact]
    public async Task XCorrelationId_IsGenerated_WhenAbsent()
    {
        using var stub = new StubBackend();
        using var factory = WithStub(stub.Port);
        var client = factory.CreateClient();
        var token  = GatewayFactory.GenerateToken();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var requestTask = client.GetAsync("/api/courses/test");
        var captured    = await stub.WaitForRequestAsync(timeout: TimeSpan.FromSeconds(5));
        await Task.WhenAny(requestTask, Task.Delay(500));

        Assert.NotNull(captured);
        var generatedId = captured!["X-Correlation-Id"];
        Assert.False(string.IsNullOrEmpty(generatedId), "Gateway must generate X-Correlation-Id");
        Assert.True(Guid.TryParse(generatedId, out _), "Generated X-Correlation-Id must be a GUID");
    }
}
