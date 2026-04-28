using LMS.CourseService.Domain.Entities;
using LMS.CourseService.Domain.Repositories;
using LMS.CourseService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.CourseService.Infrastructure.Repositories;

public sealed class SectionRepository : ISectionRepository
{
    private readonly CourseDbContext _db;

    public SectionRepository(CourseDbContext db) => _db = db;

    public async Task<CourseSection?> FindByIdAsync(Guid tenantId, Guid sectionId, CancellationToken ct = default) =>
        await _db.CourseSections
            .Where(s => s.TenantId == tenantId && s.Id == sectionId)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(CourseSection section, CancellationToken ct = default) =>
        await _db.CourseSections.AddAsync(section, ct);

    public Task DeleteAsync(CourseSection section, CancellationToken ct = default)
    {
        _db.CourseSections.Remove(section);
        return Task.CompletedTask;
    }
}
