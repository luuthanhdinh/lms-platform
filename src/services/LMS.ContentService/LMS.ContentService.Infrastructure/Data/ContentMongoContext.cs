using LMS.ContentService.Domain.Documents;
using MongoDB.Driver;

namespace LMS.ContentService.Infrastructure.Data;

public sealed class ContentMongoContext
{
    private readonly IMongoDatabase _db;

    public ContentMongoContext(IMongoClient client, string databaseName)
    {
        _db = client.GetDatabase(databaseName);
    }

    public IMongoCollection<T> Collection<T>() where T : ContentDocument
    {
        var attr = typeof(T).GetCustomAttributes(typeof(BsonCollectionAttribute), false)
            .Cast<BsonCollectionAttribute>()
            .FirstOrDefault();
        var name = attr?.CollectionName ?? typeof(T).Name.ToLowerInvariant();
        return _db.GetCollection<T>(name);
    }
}
