using LMS.Contracts.Enrollment;
using LMS.ProgressService.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LMS.ProgressService.Infrastructure.Consumers;

public sealed class EnrollmentCancelledConsumer : IConsumer<EnrollmentCancelled>
{
    private readonly ProgressDbContext _db;

    public EnrollmentCancelledConsumer(ProgressDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<EnrollmentCancelled> context)
    {
        var ct = context.CancellationToken;
        var msg = context.Message;

        // Soft-freeze: preserve data for potential re-enrolment
        var progress = await _db.CourseProgress
            .IgnoreQueryFilters() // consumer scope: no HttpContext; tenantId applied explicitly
            .FirstOrDefaultAsync(p => p.TenantId == msg.TenantId && p.UserId == msg.UserId && p.CourseId == msg.CourseId, ct);

        if (progress is null)
            return; // Idempotent: nothing to do

        var now = DateTimeOffset.UtcNow;
        progress.LastAccessedAt = now;
        progress.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
    }
}
