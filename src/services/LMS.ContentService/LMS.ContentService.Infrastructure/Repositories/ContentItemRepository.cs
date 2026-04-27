using LMS.ContentService.Domain.Documents;
using LMS.ContentService.Domain.Enums;
using LMS.ContentService.Domain.Repositories;
using LMS.ContentService.Infrastructure.Data;
using MongoDB.Driver;

namespace LMS.ContentService.Infrastructure.Repositories;

public sealed class ContentItemRepository : IContentItemRepository
{
    private readonly IMongoCollection<ContentItem> _col;

    public ContentItemRepository(ContentMongoContext ctx)
        => _col = ctx.Collection<ContentItem>();

    public async Task<ContentItem?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var filter = Builders<ContentItem>.Filter.And(
            Builders<ContentItem>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<ContentItem>.Filter.Eq(x => x.Id, id),
            Builders<ContentItem>.Filter.Eq(x => x.IsDeleted, false));
        return await _col.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<(IReadOnlyList<ContentItem> Items, long Total)> ListAsync(
        Guid tenantId, string? type, string? status, Guid? uploadedBy,
        int page, int pageSize, CancellationToken ct = default)
    {
        var builder = Builders<ContentItem>.Filter;
        var filter = builder.And(
            builder.Eq(x => x.TenantId, tenantId),
            builder.Eq(x => x.IsDeleted, false));

        if (type is not null && Enum.TryParse<ContentType>(type, true, out var t))
            filter &= builder.Eq(x => x.Type, t);

        if (status is not null && Enum.TryParse<ContentStatus>(status, true, out var s))
            filter &= builder.Eq(x => x.Status, s);

        if (uploadedBy.HasValue)
            filter &= builder.Eq(x => x.UploadedBy, uploadedBy.Value);

        var total = await _col.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await _col.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(ContentItem item, CancellationToken ct = default)
        => await _col.InsertOneAsync(item, cancellationToken: ct);

    public async Task UpdateAsync(ContentItem item, CancellationToken ct = default)
    {
        item.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<ContentItem>.Filter.And(
            Builders<ContentItem>.Filter.Eq(x => x.TenantId, item.TenantId),
            Builders<ContentItem>.Filter.Eq(x => x.Id, item.Id));
        await _col.ReplaceOneAsync(filter, item, cancellationToken: ct);
    }
}
