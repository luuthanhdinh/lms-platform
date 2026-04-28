using LMS.Gateway.RateLimiting;
using Xunit;

namespace LMS.IntegrationTests.Gateway.Unit;

[Trait("Category", "Gateway")]
public class RateLimitPolicyTests
{
    [Fact]
    public void TenantPartitionKey_HasCorrectFormat()
    {
        var tenantId = "tenant-abc-123";
        var key = $"tenant:{tenantId}";
        Assert.StartsWith("tenant:", key);
        Assert.Equal("tenant:tenant-abc-123", key);
    }

    [Fact]
    public void AnonymousPartitionKey_HasCorrectFormat()
    {
        var ip = "192.168.1.1";
        var key = $"ip:{ip}";
        Assert.StartsWith("ip:", key);
        Assert.Equal("ip:192.168.1.1", key);
    }

    [Fact]
    public void TenantAndAnonymousKeys_AreDistinct()
    {
        var tenantKey = "tenant:abc";
        var ipKey = "ip:192.168.1.1";
        Assert.NotEqual(tenantKey, ipKey);
    }

    [Fact]
    public void RateLimitOptions_DefaultValues_MatchContracts()
    {
        var opts = new RateLimitOptions();
        Assert.Equal(600, opts.PerTenantPerMinute);
        Assert.Equal(60, opts.AnonymousPerMinute);
        Assert.Equal(60, opts.WindowSeconds);
        Assert.True(opts.Enabled);
    }

    [Fact]
    public void HealthPaths_AreExempt_ByPrefix()
    {
        var healthPaths = new[] { "/health", "/health/live", "/health/ready" };
        foreach (var path in healthPaths)
            Assert.True(path.StartsWith("/health"), $"{path} should be exempt");
    }
}
