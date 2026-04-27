using LMS.CourseService.Domain.Entities;
using LMS.CourseService.Domain.Repositories;
using LMS.CourseService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.CourseService.Infrastructure.Repositories;

public sealed class LessonRepository : ILessonRepository
{
    private readonly CourseDbContext _db;

    public LessonRepository(CourseDbContext db) => _db = db;

    public async Task<CourseLesson?> FindByIdAsync(Guid tenantId, Guid lessonId, CancellationToken ct = default) =>
        await _db.CourseLessons
            .Where(l => l.TenantId == tenantId && l.Id == lessonId)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<CourseLesson>> FindByContentItemAsync(Guid tenantId, Guid contentItemId, CancellationToken ct = default) =>
        await _db.CourseLessons
            .Where(l => l.TenantId == tenantId && l.ContentItemId == contentItemId)
            .ToListAsync(ct);

    public async Task AddAsync(CourseLesson lesson, CancellationToken ct = default) =>
        await _db.CourseLessons.AddAsync(lesson, ct);

    public Task DeleteAsync(CourseLesson lesson, CancellationToken ct = default)
    {
        _db.CourseLessons.Remove(lesson);
        return Task.CompletedTask;
    }
}
