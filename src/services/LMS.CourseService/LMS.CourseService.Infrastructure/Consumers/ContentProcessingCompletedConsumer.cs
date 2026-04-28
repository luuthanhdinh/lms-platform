using LMS.Contracts.Content;
using LMS.CourseService.Domain.Repositories;
using LMS.CourseService.Infrastructure.Data;
using MassTransit;

namespace LMS.CourseService.Infrastructure.Consumers;

public sealed class ContentProcessingCompletedConsumer : IConsumer<ContentProcessingCompleted>
{
    private readonly ILessonRepository _lessons;
    private readonly CourseDbContext _db;

    public ContentProcessingCompletedConsumer(ILessonRepository lessons, CourseDbContext db)
    {
        _lessons = lessons;
        _db = db;
    }

    public async Task Consume(ConsumeContext<ContentProcessingCompleted> context)
    {
        var ct = context.CancellationToken;
        var msg = context.Message;

        var lessons = await _lessons.FindByContentItemAsync(msg.TenantId, msg.ContentItemId, ct);
        if (lessons.Count == 0) return;

        foreach (var lesson in lessons)
        {
            lesson.DurationSeconds = msg.DurationSeconds;
        }

        await _db.SaveChangesAsync(ct);
    }
}
