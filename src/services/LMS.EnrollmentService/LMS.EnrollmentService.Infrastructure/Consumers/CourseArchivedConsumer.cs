using LMS.Contracts.Course;
using LMS.EnrollmentService.Domain.Enums;
using LMS.EnrollmentService.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LMS.EnrollmentService.Infrastructure.Consumers;

public sealed class CourseArchivedConsumer : IConsumer<CourseArchived>
{
    private readonly EnrollmentDbContext _db;

    public CourseArchivedConsumer(EnrollmentDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<CourseArchived> context)
    {
        var ct = context.CancellationToken;
        var msg = context.Message;

        // Load all active enrollments for this course — bypass global query filter
        // because this consumer runs outside an HTTP request scope (no ITenantContext from headers).
        var enrollments = await _db.Enrollments
            .IgnoreQueryFilters() // consumer scope: no HttpContext; tenantId applied explicitly below
            .Where(e => e.TenantId == msg.TenantId
                     && e.CourseId == msg.CourseId
                     && e.Status == EnrollmentStatus.Active)
            .ToListAsync(ct);

        if (enrollments.Count == 0)
            return; // Idempotent: nothing to do

        var now = DateTimeOffset.UtcNow;
        foreach (var enrollment in enrollments)
        {
            enrollment.Status = EnrollmentStatus.Suspended;
            enrollment.SuspendedAt = now;
            enrollment.SuspensionReason = "course-archived";
            enrollment.UpdatedAt = now;
        }

        // Single SaveChanges to keep state transition atomic
        await _db.SaveChangesAsync(ct);
    }
}
