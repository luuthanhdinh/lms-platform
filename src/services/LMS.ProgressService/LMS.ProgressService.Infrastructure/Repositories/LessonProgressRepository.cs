using LMS.ProgressService.Domain.Entities;
using LMS.ProgressService.Domain.Repositories;
using LMS.ProgressService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.ProgressService.Infrastructure.Repositories;

public sealed class LessonProgressRepository : ILessonProgressRepository
{
    private readonly ProgressDbContext _db;

    public LessonProgressRepository(ProgressDbContext db) => _db = db;

    public async Task<LessonProgress?> FindAsync(Guid tenantId, Guid userId, Guid lessonId, CancellationToken ct = default)
        => await _db.LessonProgress
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == userId && p.LessonId == lessonId, ct);

    public async Task<IReadOnlyList<LessonProgress>> ListByCourseAsync(Guid tenantId, Guid userId, Guid courseId, CancellationToken ct = default)
        => await _db.LessonProgress
            .Where(p => p.TenantId == tenantId && p.UserId == userId && p.CourseId == courseId)
            .ToListAsync(ct);

    public async Task AddAsync(LessonProgress progress, CancellationToken ct = default)
        => await _db.LessonProgress.AddAsync(progress, ct);

    public Task UpdateAsync(LessonProgress progress, CancellationToken ct = default)
    {
        _db.LessonProgress.Update(progress);
        return Task.CompletedTask;
    }
}
