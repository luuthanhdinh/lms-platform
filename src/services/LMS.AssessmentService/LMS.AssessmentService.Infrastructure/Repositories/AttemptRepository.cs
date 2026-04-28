using LMS.AssessmentService.Domain.Entities;
using LMS.AssessmentService.Domain.Repositories;
using LMS.AssessmentService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.AssessmentService.Infrastructure.Repositories;

public sealed class AttemptRepository : IAttemptRepository
{
    private readonly AssessmentDbContext _db;

    public AttemptRepository(AssessmentDbContext db) => _db = db;

    public async Task<AssessmentAttempt?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.AssessmentAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);
    }

    public async Task<int> CountByUserAsync(Guid tenantId, Guid userId, Guid assessmentId, CancellationToken ct = default)
    {
        return await _db.AssessmentAttempts
            .CountAsync(a => a.TenantId == tenantId && a.UserId == userId && a.AssessmentId == assessmentId, ct);
    }

    public async Task<IReadOnlyList<AssessmentAttempt>> ListByUserAsync(
        Guid tenantId, Guid userId, Guid assessmentId, CancellationToken ct = default)
    {
        return await _db.AssessmentAttempts
            .Where(a => a.TenantId == tenantId && a.UserId == userId && a.AssessmentId == assessmentId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(AssessmentAttempt attempt, CancellationToken ct = default)
    {
        await _db.AssessmentAttempts.AddAsync(attempt, ct);
        // Caller is responsible for SaveChangesAsync
    }
}
