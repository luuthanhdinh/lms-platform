using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using LMS.CertificateService.Domain.Interfaces;

namespace LMS.CertificateService.Infrastructure.Storage;

public sealed class AzureBlobCertificateStorageService : ICertificateStorageService
{
    private const string ContainerName = "certificate-pdfs";
    private readonly BlobServiceClient _blobServiceClient;

    public AzureBlobCertificateStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task UploadAsync(string key, Stream pdf, CancellationToken ct = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blob = container.GetBlobClient(key);
        await blob.UploadAsync(pdf, overwrite: true, cancellationToken: ct);

        await blob.SetHttpHeadersAsync(new Azure.Storage.Blobs.Models.BlobHttpHeaders
        {
            ContentType = "application/pdf"
        }, cancellationToken: ct);
    }

    public async Task<string> GetDownloadUrlAsync(string key, CancellationToken ct = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blob = container.GetBlobClient(key);

        if (blob.CanGenerateSasUri)
        {
            var sasUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));
            return sasUri.ToString();
        }

        // Fallback for emulator / connection strings without account key
        return blob.Uri.ToString();
    }

    public async Task<Stream> GetStreamAsync(string key, CancellationToken ct = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blob = container.GetBlobClient(key);

        var download = await blob.DownloadAsync(ct);
        var ms = new MemoryStream();
        await download.Value.Content.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }
}
