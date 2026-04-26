namespace LMS.SharedKernel;

public abstract class TenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
