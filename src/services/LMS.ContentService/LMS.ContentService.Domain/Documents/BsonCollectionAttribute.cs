namespace LMS.ContentService.Domain.Documents;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BsonCollectionAttribute : Attribute
{
    public string CollectionName { get; }
    public BsonCollectionAttribute(string collectionName) => CollectionName = collectionName;
}
