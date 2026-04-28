using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using LMS.ContentService.Domain.Abstractions;
using LMS.ContentService.Domain.Storage;
using Microsoft.Extensions.Options;

namespace LMS.ContentService.Infrastructure.Storage;

public sealed class AzureBlobOptions
{
    public string Container { get; set; } = "lms-content";
    public int DownloadUrlTtlMinutes { get; set; } = 60;
}

public sealed class AzureBlobContentStorageService : IContentStorageService
{
    private readonly BlobContainerClient _container;
    private readonly AzureBlobOptions _opts;

    public AzureBlobContentStorageService(BlobServiceClient blobService, IOptions<AzureBlobOptions> opts)
    {
        _opts = opts.Value;
        _container = blobService.GetBlobContainerClient(_opts.Container);
    }

    public async Task<string> UploadAsync(
        Guid tenantId, Guid contentItemId,
        string fileName, string contentType,
        Stream data, CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        var key = StorageKeyBuilder.Original(tenantId, contentItemId, fileName);
        var blob = _container.GetBlobClient(key);
        await blob.UploadAsync(data, new BlobUploadOptions
        {
            // Content-Disposition: attachment prevents browsers rendering blobs inline (XSS mitigation)
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType,
                ContentDisposition = $"attachment; filename=\"{fileName}\""
            }
        }, ct);
        return key;
    }

    public Task<Uri> GetDownloadUriAsync(
        Guid tenantId, Guid contentItemId,
        string storageKey, TimeSpan expiry,
        CancellationToken ct = default)
    {
        StorageKeyBuilder.EnsureTenantScope(tenantId, contentItemId, storageKey);
        var blob = _container.GetBlobClient(storageKey);
        var expiresOn = DateTimeOffset.UtcNow.Add(expiry);
        return Task.FromResult(blob.GenerateSasUri(BlobSasPermissions.Read, expiresOn));
    }

    public async Task DeleteAsync(
        Guid tenantId, Guid contentItemId,
        string storageKey, CancellationToken ct = default)
    {
        StorageKeyBuilder.EnsureTenantScope(tenantId, contentItemId, storageKey);
        var blob = _container.GetBlobClient(storageKey);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }

    public async Task<Stream> OpenReadAsync(
        Guid tenantId, string storageKey,
        CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(storageKey);
        var response = await blob.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }
}
