using System.Net;
using LMS.Gateway.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace LMS.IntegrationTests.Gateway.Unit;

[Trait("Category", "Gateway")]
public class JwksHealthCheckTests
{
    private static IConfiguration BuildConfig(string authority = "http://keycloak:8080/realms/lms")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = authority
            })
            .Build();
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenJwksEndpointResponds200()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var factory = CreateFactory(handler);
        var config = BuildConfig();

        var check = new JwksHealthCheck(factory, config);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenJwksEndpointRespondsNon200()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        var factory = CreateFactory(handler);
        var config = BuildConfig();

        var check = new JwksHealthCheck(factory, config);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenJwksEndpointThrows()
    {
        var handler = new ThrowingHttpMessageHandler();
        var factory = CreateFactory(handler);
        var config = BuildConfig();

        var check = new JwksHealthCheck(factory, config);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(f => f.CreateClient("jwks-health")).Returns(client);
        return mock.Object;
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _code;
        public MockHttpMessageHandler(HttpStatusCode code) => _code = code;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_code));
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Connection refused");
    }
}
