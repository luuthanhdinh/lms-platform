namespace LMS.CourseService.Api.Extensions;

public static class ResultExtensions
{
    public static IResult ProblemNotFound(string code, string detail) =>
        Results.Problem(detail: detail, statusCode: 404,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    public static IResult ProblemForbidden() =>
        Results.Problem(detail: "Forbidden", statusCode: 403,
            extensions: new Dictionary<string, object?> { ["code"] = "FORBIDDEN" });

    public static IResult ProblemUnauthorized() =>
        Results.Problem(detail: "Unauthorized", statusCode: 401,
            extensions: new Dictionary<string, object?> { ["code"] = "UNAUTHORIZED" });

    public static IResult ProblemConflict(string code, string detail, Dictionary<string, object?>? extra = null)
    {
        var ext = new Dictionary<string, object?> { ["code"] = code };
        if (extra != null) foreach (var kv in extra) ext[kv.Key] = kv.Value;
        return Results.Problem(detail: detail, statusCode: 409, extensions: ext);
    }

    public static IResult ProblemTenantRequired() =>
        Results.Problem(detail: "X-Tenant-Id header is required", statusCode: 400,
            extensions: new Dictionary<string, object?> { ["code"] = "TENANT_REQUIRED" });
}
