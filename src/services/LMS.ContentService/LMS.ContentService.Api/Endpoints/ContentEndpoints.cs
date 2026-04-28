using LMS.ContentService.Api.Auth;
using LMS.ContentService.Api.Extensions;
using LMS.ContentService.Api.Mapping;
using LMS.ContentService.Domain.Abstractions;
using LMS.ContentService.Domain.Documents;
using LMS.ContentService.Domain.Enums;
using LMS.ContentService.Domain.Models;
using LMS.ContentService.Domain.Repositories;
using LMS.ContentService.Domain.Storage;
using LMS.Contracts.Content;
using MassTransit;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace LMS.ContentService.Api.Endpoints;

public static class ContentEndpoints
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/webm",
        "application/pdf",
        "image/jpeg", "image/png"
    };

    private const long MaxVideoBytes = 5L * 1024 * 1024 * 1024;   // 5 GiB
    private const long MaxDocumentBytes = 100L * 1024 * 1024;      // 100 MiB

    // Magic-byte signatures for server-side MIME verification
    private static readonly Dictionary<string, byte[][]> MagicBytes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["video/mp4"]        = [new byte[] { 0x00, 0x00, 0x00, 0x04, 0x66, 0x74, 0x79, 0x70 },  // ftyp at offset 4
                                    new byte[] { 0x00, 0x00, 0x00, 0x08, 0x66, 0x74, 0x79, 0x70 }],
            ["video/webm"]       = [new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }],
            ["application/pdf"]  = [new byte[] { 0x25, 0x50, 0x44, 0x46 }],  // %PDF
            ["image/jpeg"]       = [new byte[] { 0xFF, 0xD8, 0xFF }],
            ["image/png"]        = [new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }],
        };

    private static bool HasAllowedMagicBytes(ReadOnlySpan<byte> header, string mimeType)
    {
        if (!MagicBytes.TryGetValue(mimeType, out var signatures)) return false;
        foreach (var sig in signatures)
        {
            // For MP4 check ftyp at offset 4, others at offset 0
            int offset = mimeType.Equals("video/mp4", StringComparison.OrdinalIgnoreCase) ? 4 : 0;
            if (header.Length >= offset + sig.Length &&
                header.Slice(offset, sig.Length).SequenceEqual(sig))
                return true;
        }
        return false;
    }

    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/content");

        // POST /api/content/uploads — instructor or admin, multipart/form-data
        group.MapPost("/uploads", async (
            HttpContext http,
            ITenantContext ctx,
            IContentItemRepository repo,
            IContentStorageService storage,
            IPublishEndpoint bus,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsInstructorOrAdmin(ctx)) return ResultExtensions.ProblemForbidden();

            if (!http.Request.HasFormContentType)
                return Results.Problem(detail: "multipart/form-data required", statusCode: 400);

            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["file"] = ["File is required"] });

            var mimeType = file.ContentType;
            if (!AllowedMimeTypes.Contains(mimeType))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["file"] = [$"MimeType '{mimeType}' is not allowed"] });

            if (file.FileName.Length > 260)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["file"] = ["FileName must be <= 260 characters"] });

            var isVideo = mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
            var maxBytes = isVideo ? MaxVideoBytes : MaxDocumentBytes;
            if (file.Length > maxBytes)
                return ResultExtensions.ProblemPayloadTooLarge($"File exceeds maximum size of {maxBytes / (1024 * 1024)} MiB");

            // Server-side magic-byte verification to prevent MIME spoofing
            var magicBuf = new byte[16];
            await using var fileStream = file.OpenReadStream();
            var bytesRead = await fileStream.ReadAsync(magicBuf.AsMemory(0, 16), ct);
            if (!HasAllowedMagicBytes(magicBuf.AsSpan(0, bytesRead), mimeType))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["file"] = ["File content does not match declared content type"] });
            fileStream.Position = 0;

            if (!Enum.TryParse<Domain.Enums.ContentType>(form["type"].ToString(), true, out var contentType))
                contentType = isVideo ? Domain.Enums.ContentType.Video : Domain.Enums.ContentType.Other;

            // Sanitize filename to safe characters only
            var safeFileName = System.Text.RegularExpressions.Regex.Replace(
                Path.GetFileName(file.FileName), @"[^a-zA-Z0-9\-_\.]", "_");

            var contentItemId = Guid.NewGuid();
            var storageKey = await storage.UploadAsync(
                ctx.TenantId, contentItemId,
                safeFileName, mimeType,
                fileStream, ct);

            var item = new ContentItem
            {
                Id = contentItemId,
                TenantId = ctx.TenantId,
                UploadedBy = ctx.UserId,
                OriginalFileName = safeFileName,
                MimeType = mimeType,
                Type = contentType,
                Status = ContentStatus.Uploaded,
                SizeBytes = file.Length,
                StorageContainer = "lms-content",
                StorageKey = storageKey,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await repo.AddAsync(item, ct);

            await bus.Publish(new ContentUploaded(
                Guid.NewGuid(), contentItemId, ctx.TenantId,
                ctx.UserId, (LMS.Contracts.Content.ContentType)(int)contentType,
                file.Length, DateTimeOffset.UtcNow), ct);

            return Results.Created($"/api/content/{contentItemId}", item.ToDto());
        }).DisableAntiforgery()
          .WithMetadata(new RequestSizeLimitAttribute(MaxVideoBytes));

        // GET /api/content/{id} — tenant scope
        group.MapGet("/{id:guid}", async (
            Guid id,
            ITenantContext ctx,
            IContentItemRepository repo,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();

            var item = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            return item is null
                ? ResultExtensions.ProblemNotFound("CONTENT_NOT_FOUND", "Content item not found")
                : Results.Ok(item.ToDto());
        });

        // GET /api/content/{id}/playback — tenant scope, only when Ready
        group.MapGet("/{id:guid}/playback", async (
            Guid id,
            ITenantContext ctx,
            IContentItemRepository repo,
            IContentStorageService storage,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();

            var item = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (item is null)
                return ResultExtensions.ProblemNotFound("CONTENT_NOT_FOUND", "Content item not found");

            if (item.Status != ContentStatus.Ready)
                return ResultExtensions.ProblemConflict("NOT_READY", "Content is not ready for playback");

            var playbackKey = item.HlsManifestKey ?? item.StorageKey;
            var ttl = TimeSpan.FromMinutes(60);
            var url = await storage.GetDownloadUriAsync(ctx.TenantId, item.Id, playbackKey, ttl, ct);
            var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

            return Results.Ok(new PlaybackUrlDto(item.Id, url, expiresAt, item.DurationSeconds));
        });

        // GET /api/content — tenant scope, paginated
        group.MapGet("/", async (
            ITenantContext ctx,
            IContentItemRepository repo,
            string? type, string? status, Guid? uploadedBy,
            int page = 1, int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();

            var (items, total) = await repo.ListAsync(ctx.TenantId, type, status, uploadedBy, page, pageSize, ct);
            return Results.Ok(new Page<ContentItemDto>(
                items.Select(i => i.ToDto()).ToList(), page, pageSize, total));
        });

        // DELETE /api/content/{id} — uploader or admin (soft delete)
        group.MapDelete("/{id:guid}", async (
            Guid id,
            ITenantContext ctx,
            IContentItemRepository repo,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var item = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (item is null)
                return ResultExtensions.ProblemNotFound("CONTENT_NOT_FOUND", "Content item not found");

            if (item.UploadedBy != ctx.UserId && !AuthorizationHelpers.IsAdmin(ctx))
                return ResultExtensions.ProblemForbidden();

            item.IsDeleted = true;
            await repo.UpdateAsync(item, ct);
            return Results.NoContent();
        });

        return app;
    }
}
