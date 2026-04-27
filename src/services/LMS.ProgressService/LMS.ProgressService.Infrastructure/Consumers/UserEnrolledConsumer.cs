using LMS.Contracts.Enrollment;
using LMS.ProgressService.Domain.Entities;
using LMS.ProgressService.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LMS.ProgressService.Infrastructure.Consumers;

public sealed class UserEnrolledConsumer : IConsumer<UserEnrolled>
{
    private readonly ProgressDbContext _db;

    public UserEnrolledConsumer(ProgressDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<UserEnrolled> context)
    {
        var ct = context.CancellationToken;
        var msg = context.Message;

        // Idempotency: check if CourseProgress already exists for (TenantId, UserId, CourseId)
        var exists = await _db.CourseProgress
            .IgnoreQueryFilters() // consumer scope: no HttpContext; tenantId applied explicitly
            .AnyAsync(p => p.TenantId == msg.TenantId && p.UserId == msg.UserId && p.CourseId == msg.CourseId, ct);

        if (exists)
            return;

        var progress = new CourseProgress
        {
            UserId = msg.UserId,
            CourseId = msg.CourseId,
            TenantId = msg.TenantId,
            LessonsCompleted = 0,
            TotalRequiredLessons = 0,
            CompletionPercent = 0,
            LastAccessedAt = DateTimeOffset.UtcNow,
            CourseCompletedEventPublished = false
        };

        await _db.CourseProgress.AddAsync(progress, ct);
        await _db.SaveChangesAsync(ct);
    }
}
