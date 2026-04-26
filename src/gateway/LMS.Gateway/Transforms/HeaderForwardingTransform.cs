using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace LMS.Gateway.Transforms;

public sealed class HeaderForwardingTransform : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context) { }
    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            var httpContext = transformContext.HttpContext;

            // X-Correlation-Id — always forward (generate if missing)
            var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                ?? Guid.NewGuid().ToString();
            transformContext.ProxyRequest.Headers.Remove("X-Correlation-Id");
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

            // Auth headers — only for authenticated users
            if (httpContext.User.Identity?.IsAuthenticated != true)
                return ValueTask.CompletedTask;

            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");
            var tenantId = httpContext.User.FindFirstValue("tenant_id");
            var roles = httpContext.User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Where(r => !string.IsNullOrEmpty(r));
            var rolesHeader = string.Join(",", roles);

            if (!string.IsNullOrEmpty(userId))
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);
            if (!string.IsNullOrEmpty(tenantId))
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
            if (!string.IsNullOrEmpty(rolesHeader))
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Roles", rolesHeader);

            return ValueTask.CompletedTask;
        });
    }
}
