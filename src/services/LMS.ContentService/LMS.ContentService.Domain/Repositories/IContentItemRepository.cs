using LMS.ContentService.Domain.Documents;

namespace LMS.ContentService.Domain.Repositories;

public interface IContentItemRepository
{
    Task<ContentItem?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<ContentItem> Items, long Total)> ListAsync(
        Guid tenantId,
        string? type, string? status, Guid? uploadedBy,
        int page, int pageSize,
        CancellationToken ct = default);
    Task AddAsync(ContentItem item, CancellationToken ct = default);
    Task UpdateAsync(ContentItem item, CancellationToken ct = default);
}
