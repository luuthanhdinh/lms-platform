using LMS.EnrollmentService.Domain.Entities;
using LMS.EnrollmentService.Domain.Enums;
using LMS.EnrollmentService.Domain.Repositories;
using LMS.EnrollmentService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.EnrollmentService.Infrastructure.Repositories;

public sealed class EnrollmentRepository : IEnrollmentRepository
{
    private readonly EnrollmentDbContext _db;

    public EnrollmentRepository(EnrollmentDbContext db) => _db = db;

    public Task<Enrollment?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.Enrollments
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, ct);

    public Task<Enrollment?> FindActiveAsync(Guid tenantId, Guid userId, Guid courseId, CancellationToken ct = default)
        => _db.Enrollments
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.UserId == userId && e.CourseId == courseId && e.Status == EnrollmentStatus.Active, ct);

    /// <summary>
    /// Find all active enrollments for a course — used by consumers where no HttpContext exists,
    /// so we bypass the global filter and apply tenantId explicitly.
    /// </summary>
    public async Task<IReadOnlyList<Enrollment>> FindActiveEnrollmentsForCourseAsync(Guid tenantId, Guid courseId, CancellationToken ct = default)
        => await _db.Enrollments
            .IgnoreQueryFilters() // consumer scope — no HttpContext; apply tenantId predicate manually
            .Where(e => e.TenantId == tenantId && e.CourseId == courseId && e.Status == EnrollmentStatus.Active)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Enrollment> Items, int Total)> ListByUserAsync(
        Guid tenantId, Guid userId, EnrollmentStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Enrollments.Where(e => e.UserId == userId);
        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.EnrolledAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(IReadOnlyList<Enrollment> Items, int Total)> ListAllAsync(
        Guid tenantId, EnrollmentStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Enrollments.AsQueryable();
        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.EnrolledAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(IReadOnlyList<Enrollment> Items, int Total)> ListByCourseAsync(
        Guid tenantId, Guid courseId, EnrollmentStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Enrollments.Where(e => e.CourseId == courseId);
        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.EnrolledAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(int ActiveCount, int TotalCount)> CountByCourseAsync(Guid tenantId, Guid courseId, CancellationToken ct = default)
    {
        var activeCount = await _db.Enrollments
            .CountAsync(e => e.CourseId == courseId && e.Status == EnrollmentStatus.Active, ct);
        var totalCount = await _db.Enrollments
            .CountAsync(e => e.CourseId == courseId, ct);
        return (activeCount, totalCount);
    }

    public Task AddAsync(Enrollment enrollment, CancellationToken ct = default)
    {
        _db.Enrollments.Add(enrollment);
        return Task.CompletedTask; // caller must invoke SaveChangesAsync (needed for outbox atomicity)
    }

    public Task UpdateAsync(Enrollment enrollment, CancellationToken ct = default)
    {
        _db.Enrollments.Update(enrollment);
        return Task.CompletedTask; // caller must invoke SaveChangesAsync (needed for outbox atomicity)
    }
}
