using LMS.Contracts.Course;
using LMS.AssessmentService.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LMS.AssessmentService.Infrastructure.Consumers;

public sealed class CourseArchivedConsumer : IConsumer<CourseArchived>
{
    private readonly AssessmentDbContext _db;

    public CourseArchivedConsumer(AssessmentDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<CourseArchived> context)
    {
        var ct = context.CancellationToken;
        var msg = context.Message;

        // Load all active assessments for this course — bypass global query filter
        // because this consumer runs outside an HTTP request scope (no ITenantContext from headers).
        var assessments = await _db.Assessments
            .IgnoreQueryFilters() // consumer scope: no HttpContext; tenantId applied explicitly below
            .Where(a => a.TenantId == msg.TenantId
                     && a.CourseId == msg.CourseId
                     && a.IsActive)
            .ToListAsync(ct);

        if (assessments.Count == 0)
            return; // Idempotent: nothing to do

        foreach (var assessment in assessments)
        {
            assessment.IsActive = false;
        }

        // Single SaveChanges to keep state transition atomic
        await _db.SaveChangesAsync(ct);
    }
}
