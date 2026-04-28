namespace LMS.EnrollmentService.Api.Extensions;

internal static class ResultExtensions
{
    internal static IResult ProblemUnauthorized() =>
        Results.Problem(detail: "Authentication required", statusCode: 401);

    internal static IResult ProblemForbidden() =>
        Results.Problem(detail: "Insufficient permissions", statusCode: 403);

    internal static IResult ProblemTenantRequired() =>
        Results.Problem(title: "Tenant required", detail: "X-Tenant-Id header is missing or invalid",
            statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = "TENANT_REQUIRED" });

    internal static IResult ProblemNotFound(string code, string detail) =>
        Results.Problem(title: "Not found", detail: detail, statusCode: 404,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    internal static IResult ProblemConflict(string code, string detail) =>
        Results.Problem(title: "Conflict", detail: detail, statusCode: 409,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
