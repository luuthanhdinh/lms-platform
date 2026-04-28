namespace LMS.ContentService.Domain.Abstractions;

public interface IContentStorageService
{
    Task<string> UploadAsync(
        Guid tenantId, Guid contentItemId,
        string fileName, string contentType,
        Stream data, CancellationToken ct = default);

    Task<Uri> GetDownloadUriAsync(
        Guid tenantId, Guid contentItemId,
        string storageKey, TimeSpan expiry,
        CancellationToken ct = default);

    Task DeleteAsync(
        Guid tenantId, Guid contentItemId,
        string storageKey, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(
        Guid tenantId, string storageKey,
        CancellationToken ct = default);
}
