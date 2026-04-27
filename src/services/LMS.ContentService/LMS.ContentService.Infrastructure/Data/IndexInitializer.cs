using LMS.ContentService.Domain.Documents;
using MongoDB.Driver;

namespace LMS.ContentService.Infrastructure.Data;

public static class IndexInitializer
{
    public static async Task EnsureIndexesAsync(ContentMongoContext ctx, CancellationToken ct = default)
    {
        var col = ctx.Collection<ContentItem>();
        var indexes = new List<CreateIndexModel<ContentItem>>
        {
            new(Builders<ContentItem>.IndexKeys
                .Ascending(x => x.TenantId).Descending(x => x.CreatedAt)),
            new(Builders<ContentItem>.IndexKeys
                .Ascending(x => x.TenantId).Ascending(x => x.Status)),
            new(Builders<ContentItem>.IndexKeys
                .Ascending(x => x.TenantId).Ascending(x => x.UploadedBy)),
            new(Builders<ContentItem>.IndexKeys
                .Ascending(x => x.TenantId).Ascending(x => x.Id),
                new CreateIndexOptions { Unique = true }),
        };
        await col.Indexes.CreateManyAsync(indexes, ct);
    }
}
