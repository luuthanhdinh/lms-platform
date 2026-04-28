using LMS.SharedKernel;

namespace LMS.CourseService.Domain.Entities;

public class CourseSnapshot : TenantEntity
{
    public Guid CourseId { get; set; }
    public int Version { get; set; }
    public string StructureJson { get; set; } = default!;
    public DateTimeOffset SnapshotAt { get; set; } = DateTimeOffset.UtcNow;
}
