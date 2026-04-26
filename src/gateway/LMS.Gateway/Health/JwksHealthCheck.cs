using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LMS.Gateway.Health;

public sealed class JwksHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _jwksUri;

    public JwksHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        var authority = configuration["Keycloak:Authority"] ?? string.Empty;
        _jwksUri = $"{authority.TrimEnd('/')}/protocol/openid-connect/certs";
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("jwks-health");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var response = await client.GetAsync(_jwksUri, cts.Token);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("JWKS endpoint reachable")
                : HealthCheckResult.Degraded($"JWKS returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("JWKS endpoint unreachable", ex);
        }
    }
}
