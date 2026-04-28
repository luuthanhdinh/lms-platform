using System.Net;
using System.Net.Http.Headers;
using LMS.IntegrationTests.Gateway.Fixtures;
using Xunit;

namespace LMS.IntegrationTests.Gateway;

/// <summary>
/// Verifies that the gateway's JWT authentication layer correctly accepts and rejects
/// requests before forwarding them to downstream services.
///
/// A 502/503/504 response means YARP forwarded the request (auth passed) but the stub
/// backend at localhost:19999 is not listening — this is the expected "auth passed" signal.
/// A 401 means auth rejected the request before it reached YARP.
/// </summary>
[Trait("Category", "Gateway")]
public sealed class JwtTests : IClassFixture<GatewayFactory>
{
    private readonly HttpClient _client;

    public JwtTests(GatewayFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Request_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/courses/anything");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithoutToken_Returns401_WithUnauthenticatedCode()
    {
        var response = await _client.GetAsync("/api/courses/anything");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("UNAUTHENTICATED", body);
    }

    [Fact]
    public async Task Request_WithoutToken_Returns401_WithJsonContentType()
    {
        var response = await _client.GetAsync("/api/courses/anything");
        // Content-Type must be application/json per the contracts spec
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Contains("application/json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Request_WithExpiredToken_Returns401()
    {
        var token = GatewayFactory.GenerateToken(expired: true);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/courses/anything");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithMalformedToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not.a.real.jwt");

        var response = await _client.GetAsync("/api/courses/anything");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithValidToken_IsForwardedByGateway()
    {
        // Auth passed when gateway reaches YARP routing; stub backend is not running
        // so we get 502/503/504 — anything except 401/403 confirms auth succeeded.
        var token = GatewayFactory.GenerateToken();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/courses/anything");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden,    response.StatusCode);
    }

    [Theory]
    [InlineData("/api/courses/anything")]
    [InlineData("/api/enrollments/anything")]
    [InlineData("/api/assessments/anything")]
    [InlineData("/api/certificates/anything")]
    [InlineData("/api/progress/anything")]
    [InlineData("/api/content/anything")]
    [InlineData("/api/identity/anything")]
    public async Task AllProtectedRoutes_Reject_RequestsWithoutToken(string path)
    {
        // Every route in the contracts table requires auth — none may be anonymous
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousRoute_Verify_IsAccessible_WithoutToken()
    {
        // /verify/{**rest} is the only anonymous route per the contracts spec
        // Expect 502/503 (forwarded to stub) or similar — NOT 401
        var response = await _client.GetAsync("/verify/cert-abc");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
