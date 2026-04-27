namespace LMS.CertificateService.Domain.Interfaces;

public interface ICertificateStorageService
{
    Task UploadAsync(string key, Stream pdf, CancellationToken ct = default);
    Task<string> GetDownloadUrlAsync(string key, CancellationToken ct = default);
    Task<Stream> GetStreamAsync(string key, CancellationToken ct = default);
}
