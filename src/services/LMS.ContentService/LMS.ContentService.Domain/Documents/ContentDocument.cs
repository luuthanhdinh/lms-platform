using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.ContentService.Domain.Documents;

public abstract class ContentDocument : ITenantDocument
{
    [BsonId]
    public ObjectId MongoId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
