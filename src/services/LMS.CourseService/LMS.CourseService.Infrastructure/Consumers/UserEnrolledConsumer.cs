using LMS.Contracts.Enrollment;
using LMS.CourseService.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LMS.CourseService.Infrastructure.Consumers;

public sealed class UserEnrolledConsumer : IConsumer<UserEnrolled>
{
    private readonly CourseDbContext _db;

    public UserEnrolledConsumer(CourseDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<UserEnrolled> context)
    {
        var ct = context.CancellationToken;
        var msg = context.Message;

        var course = await _db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == msg.TenantId && c.Id == msg.CourseId)
            .FirstOrDefaultAsync(ct);

        if (course is null) return;

        course.EnrollmentCount++;
        await _db.SaveChangesAsync(ct);
    }
}
