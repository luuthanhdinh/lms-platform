using LMS.CertificateService.Domain.DTOs;
using LMS.CertificateService.Domain.Enums;
using LMS.CertificateService.Domain.Interfaces;
using LMS.CertificateService.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace LMS.CertificateService.Api.Endpoints;

public static class CertificateEndpoints
{
    public static IEndpointRouteBuilder MapCertificateEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /certificates/me — list current user's certificates, optional courseId filter
        app.MapGet("/certificates/me", async (
            HttpContext ctx,
            ICertificateRepository repo,
            Guid? courseId,
            CancellationToken ct) =>
        {
            var (tenantId, userId, _, valid) = ReadHeaders(ctx);
            if (!valid) return Results.Problem("Missing X-Tenant-Id or X-User-Id", statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = "TENANT_REQUIRED" });

            var certs = await repo.ListByUserAsync(tenantId, userId, ct);
            var result = courseId.HasValue
                ? certs.Where(c => c.CourseId == courseId.Value).ToList()
                : certs;
            return Results.Ok(result.Select(MapToDto));
        });

        // GET /certificates/{id}
        app.MapGet("/certificates/{id:guid}", async (
            Guid id,
            HttpContext ctx,
            ICertificateRepository repo,
            CancellationToken ct) =>
        {
            var (tenantId, userId, roles, valid) = ReadHeaders(ctx);
            if (!valid) return Results.Problem("Missing X-Tenant-Id or X-User-Id", statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = "TENANT_REQUIRED" });

            var cert = await repo.FindAsync(tenantId, id, ct);
            if (cert is null) return Results.Problem("Certificate not found", statusCode: 404, extensions: new Dictionary<string, object?> { ["code"] = "CERT_NOT_FOUND" });

            // Only owner or Admin/Instructor can view
            if (cert.UserId != userId && !roles.Any(r => r is "admin" or "instructor"))
                return Results.Problem("Forbidden", statusCode: 403, extensions: new Dictionary<string, object?> { ["code"] = "FORBIDDEN" });

            return Results.Ok(MapToDto(cert));
        });

        // GET /certificates/{id}/pdf — stream PDF
        app.MapGet("/certificates/{id:guid}/pdf", async (
            Guid id,
            HttpContext ctx,
            ICertificateRepository repo,
            ICertificateStorageService storage,
            CancellationToken ct) =>
        {
            var (tenantId, userId, roles, valid) = ReadHeaders(ctx);
            if (!valid) return Results.Problem("Missing X-Tenant-Id or X-User-Id", statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = "TENANT_REQUIRED" });

            var cert = await repo.FindAsync(tenantId, id, ct);
            if (cert is null) return Results.Problem("Certificate not found", statusCode: 404, extensions: new Dictionary<string, object?> { ["code"] = "CERT_NOT_FOUND" });

            if (cert.UserId != userId && !roles.Any(r => r is "admin" or "instructor"))
                return Results.Problem("Forbidden", statusCode: 403, extensions: new Dictionary<string, object?> { ["code"] = "FORBIDDEN" });

            if (cert.Status == CertificateStatus.Revoked)
                return Results.Problem("Certificate has been revoked", statusCode: 410, extensions: new Dictionary<string, object?> { ["code"] = "REVOKED" });

            if (string.IsNullOrEmpty(cert.PdfStorageKey))
                return Results.Problem("PDF not available", statusCode: 404, extensions: new Dictionary<string, object?> { ["code"] = "CERT_NOT_FOUND" });

            var stream = await storage.GetStreamAsync(cert.PdfStorageKey, ct);
            return Results.File(stream, "application/pdf", $"certificate-{cert.CertificateNumber}.pdf");
        });

        // POST /certificates/{id}/revoke — Admin or Instructor only
        app.MapPost("/certificates/{id:guid}/revoke", async (
            Guid id,
            [FromBody] RevokeCertificateRequest req,
            HttpContext ctx,
            ICertificateRepository repo,
            IUnitOfWork uow,
            CancellationToken ct) =>
        {
            var (tenantId, _, roles, valid) = ReadHeaders(ctx);
            if (!valid) return Results.Problem("Missing X-Tenant-Id", statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = "TENANT_REQUIRED" });

            if (!roles.Any(r => r is "admin" or "instructor"))
                return Results.Problem("Forbidden — admin or instructor role required", statusCode: 403, extensions: new Dictionary<string, object?> { ["code"] = "FORBIDDEN" });

            if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length > 500)
                return Results.Problem("Reason must be 1–500 characters.", statusCode: 400,
                    extensions: new Dictionary<string, object?> { ["code"] = "VALIDATION_FAILED" });

            var cert = await repo.FindAsync(tenantId, id, ct);
            if (cert is null) return Results.Problem("Certificate not found", statusCode: 404, extensions: new Dictionary<string, object?> { ["code"] = "CERT_NOT_FOUND" });

            if (cert.Status == CertificateStatus.Revoked)
                return Results.Problem("Certificate already revoked", statusCode: 409, extensions: new Dictionary<string, object?> { ["code"] = "ALREADY_REVOKED" });

            cert.Status = CertificateStatus.Revoked;
            cert.RevokedAt = DateTimeOffset.UtcNow;
            cert.RevocationReason = req.Reason;
            cert.UpdatedAt = DateTimeOffset.UtcNow;

            await repo.UpdateAsync(cert, ct);
            await uow.SaveChangesAsync(ct);

            return Results.NoContent();
        });

        // GET /verify/{code} — anonymous
        app.MapGet("/verify/{code}", async (
            string code,
            ICertificateRepository repo,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(code, out var verificationCode))
                return Results.Problem("Invalid verification code", statusCode: 404, extensions: new Dictionary<string, object?> { ["code"] = "CERT_NOT_FOUND" });

            // IgnoreQueryFilters applied in FindByVerificationCodeAsync — anonymous endpoint
            var cert = await repo.FindByVerificationCodeAsync(verificationCode, ct);
            if (cert is null)
                return Results.Problem("Certificate not found", statusCode: 404, extensions: new Dictionary<string, object?> { ["code"] = "CERT_NOT_FOUND" });

            if (cert.Status == CertificateStatus.Revoked)
                return Results.Problem($"Certificate was revoked on {cert.RevokedAt:O}", statusCode: 410,
                    extensions: new Dictionary<string, object?> { ["code"] = "REVOKED", ["revokedAt"] = cert.RevokedAt });

            // Return safe fields only — no UserId, TenantId, PdfStorageKey
            var dto = new CertificateVerifyDto(
                cert.CertificateNumber,
                cert.CourseId,
                cert.IssuedAt,
                cert.RevokedAt,
                cert.Status == CertificateStatus.Active);

            return Results.Ok(dto);
        }).AllowAnonymous();

        return app;
    }

    private static (Guid TenantId, Guid UserId, string[] Roles, bool Valid) ReadHeaders(HttpContext ctx)
    {
        var tenantHeader = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var userHeader = ctx.Request.Headers["X-User-Id"].FirstOrDefault();

        if (!Guid.TryParse(tenantHeader, out var tenantId) || !Guid.TryParse(userHeader, out var userId))
            return (Guid.Empty, Guid.Empty, Array.Empty<string>(), false);

        var roles = ctx.Request.Headers["X-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim().ToLowerInvariant())
            .ToArray();

        return (tenantId, userId, roles, true);
    }

    private static CertificateDto MapToDto(Domain.Entities.Certificate cert) => new(
        cert.Id,
        cert.UserId,
        cert.CourseId,
        cert.CertificateNumber,
        cert.VerificationCode,
        cert.Status,
        cert.CourseName,
        cert.LearnerName,
        cert.IssuedAt,
        cert.RevokedAt,
        cert.RevocationReason);
}

public record RevokeCertificateRequest(string Reason);
