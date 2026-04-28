using LMS.CourseService.Domain.Entities;
using LMS.CourseService.Domain.Enums;
using LMS.CourseService.Domain.Repositories;
using LMS.CourseService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.CourseService.Infrastructure.Repositories;

public sealed class CourseRepository : ICourseRepository
{
    private readonly CourseDbContext _db;

    public CourseRepository(CourseDbContext db) => _db = db;

    public async Task<Course?> FindByIdAsync(Guid tenantId, Guid courseId, CancellationToken ct = default) =>
        await _db.Courses
            .Where(c => c.TenantId == tenantId && c.Id == courseId)
            .FirstOrDefaultAsync(ct);

    public async Task<Course?> FindByIdWithSectionsAsync(Guid tenantId, Guid courseId, CancellationToken ct = default) =>
        await _db.Courses
            .Where(c => c.TenantId == tenantId && c.Id == courseId)
            .Include(c => c.Sections).ThenInclude(s => s.Lessons)
            .Include(c => c.Prerequisites)
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<Course> Items, int Total)> ListPublishedAsync(
        Guid tenantId, string? category, string? tag, DifficultyLevel? difficulty,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Courses
            .Where(c => c.TenantId == tenantId && c.Status == CourseStatus.Published);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(c => c.Category == category);

        if (!string.IsNullOrEmpty(tag))
            query = query.Where(c => c.Tags.Contains(tag));

        if (difficulty.HasValue)
            query = query.Where(c => c.Difficulty == difficulty.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(Course course, CancellationToken ct = default) =>
        await _db.Courses.AddAsync(course, ct);

    public async Task<bool> ExistsAsync(Guid tenantId, Guid courseId, CancellationToken ct = default) =>
        await _db.Courses.AnyAsync(c => c.TenantId == tenantId && c.Id == courseId, ct);
}
