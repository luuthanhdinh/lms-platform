namespace LMS.IdentityService.Api.Extensions;

public static class ResultExtensions
{
    public static IResult ProblemNotFound(string code, string message) =>
        Results.Json(new { code, message }, statusCode: 404);

    public static IResult ProblemConflict(string code, string message) =>
        Results.Json(new { code, message }, statusCode: 409);

    public static IResult ProblemUnauthorized() =>
        Results.Json(new { code = "UNAUTHENTICATED", message = "Authentication required" }, statusCode: 401);

    public static IResult ProblemForbidden() =>
        Results.Json(new { code = "FORBIDDEN", message = "Insufficient permissions" }, statusCode: 403);
}
