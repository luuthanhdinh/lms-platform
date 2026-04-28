using LMS.CourseService.Domain.Entities;
using LMS.CourseService.Domain.Repositories;
using LMS.CourseService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.CourseService.Infrastructure.Repositories;

public sealed class CourseSnapshotRepository : ICourseSnapshotRepository
{
    private readonly CourseDbContext _db;

    public CourseSnapshotRepository(CourseDbContext db) => _db = db;

    public async Task AddAsync(CourseSnapshot snapshot, CancellationToken ct = default) =>
        await _db.CourseSnapshots.AddAsync(snapshot, ct);
}
