using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace LMS.Gateway.RateLimiting;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddGatewayRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("RateLimit").Get<RateLimitOptions>()
            ?? new RateLimitOptions();

        if (!options.Enabled)
            return services;

        services.AddRateLimiter(limiter =>
        {
            // TODO: Replace GlobalLimiter with a Redis-backed distributed limiter in Phase 2
            // when the `RedisRateLimiting` NuGet package is stable.
            // Partition keys are locked: tenant:{X-Tenant-Id} and ip:{RemoteIpAddress}.
            // Only the backing store changes — swap FixedWindowRateLimiterOptions for
            // RedisFixedWindowRateLimiter with the same PermitLimit/Window values.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                // Health endpoints are exempt from rate limiting
                var path = httpContext.Request.Path.Value ?? string.Empty;
                if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
                    return RateLimitPartition.GetNoLimiter("health");

                // Authenticated requests — partition by tenant_id claim
                var tenantId = httpContext.User.FindFirstValue("tenant_id");
                if (!string.IsNullOrEmpty(tenantId))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"tenant:{tenantId}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = options.PerTenantPerMinute,
                            Window = TimeSpan.FromSeconds(options.WindowSeconds),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        });
                }

                // Anonymous requests (/verify/** and unauthenticated) — partition by IP
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"ip:{ip}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.AnonymousPerMinute,
                        Window = TimeSpan.FromSeconds(options.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                // RetryAfter metadata may not be present on all limiter implementations;
                // fall back to the configured window length.
                var retryAfter = context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter, out var retryAfterTs)
                    ? (int)retryAfterTs.TotalSeconds
                    : options.WindowSeconds;

                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();

                await context.HttpContext.Response.WriteAsync(
                    $$"""{"code":"RATE_LIMITED","message":"Too many requests","retryAfterSeconds":{{retryAfter}}}""",
                    cancellationToken);
            };
        });

        return services;
    }
}
