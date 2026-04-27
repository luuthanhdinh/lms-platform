using LMS.ProgressService.Domain.Entities;
using LMS.ProgressService.Domain.Repositories;
using LMS.ProgressService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.ProgressService.Infrastructure.Repositories;

public sealed class CourseProgressRepository : ICourseProgressRepository
{
    private readonly ProgressDbContext _db;

    public CourseProgressRepository(ProgressDbContext db) => _db = db;

    public async Task<CourseProgress?> FindAsync(Guid tenantId, Guid userId, Guid courseId, CancellationToken ct = default)
        => await _db.CourseProgress
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == userId && p.CourseId == courseId, ct);

    public async Task<IReadOnlyList<CourseProgress>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.CourseProgress
            .Where(p => p.TenantId == tenantId && p.UserId == userId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CourseProgress>> ListByCourseAsync(Guid tenantId, Guid courseId, CancellationToken ct = default)
        // IgnoreQueryFilters for analytics — tenant filter is applied explicitly via tenantId predicate
        => await _db.CourseProgress
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.CourseId == courseId)
            .ToListAsync(ct);

    public async Task AddAsync(CourseProgress progress, CancellationToken ct = default)
        => await _db.CourseProgress.AddAsync(progress, ct);

    public Task UpdateAsync(CourseProgress progress, CancellationToken ct = default)
    {
        _db.CourseProgress.Update(progress);
        return Task.CompletedTask;
    }
}
