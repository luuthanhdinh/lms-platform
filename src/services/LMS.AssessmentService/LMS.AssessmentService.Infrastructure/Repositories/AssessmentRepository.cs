using LMS.AssessmentService.Domain.Entities;
using LMS.AssessmentService.Domain.Repositories;
using LMS.AssessmentService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.AssessmentService.Infrastructure.Repositories;

public sealed class AssessmentRepository : IAssessmentRepository
{
    private readonly AssessmentDbContext _db;

    public AssessmentRepository(AssessmentDbContext db) => _db = db;

    public async Task<Assessment?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Assessments
            .Include(a => a.Questions)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);
    }

    public async Task<(IReadOnlyList<Assessment> Items, int Total)> ListByCourseAsync(
        Guid tenantId, Guid courseId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Assessments
            .Where(a => a.TenantId == tenantId && a.CourseId == courseId)
            .OrderByDescending(a => a.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(a => a.Questions)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<Assessment>> FindActiveByCourseAsync(
        Guid tenantId, Guid courseId, CancellationToken ct = default)
    {
        // IgnoreQueryFilters + explicit tenantId predicate (defence in depth)
        return await _db.Assessments
            .IgnoreQueryFilters() // consumer/background scope — tenantId applied explicitly below
            .Where(a => a.TenantId == tenantId && a.CourseId == courseId && a.IsActive)
            .Include(a => a.Questions)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Assessment assessment, CancellationToken ct = default)
    {
        await _db.Assessments.AddAsync(assessment, ct);
        // Caller is responsible for SaveChangesAsync
    }

    public Task UpdateAsync(Assessment assessment, CancellationToken ct = default)
    {
        _db.Assessments.Update(assessment);
        // Caller is responsible for SaveChangesAsync
        return Task.CompletedTask;
    }
}
