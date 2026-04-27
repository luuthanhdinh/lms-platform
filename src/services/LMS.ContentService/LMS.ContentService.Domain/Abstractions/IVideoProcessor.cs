using LMS.ContentService.Domain.Documents;

namespace LMS.ContentService.Domain.Abstractions;

public record ProcessingResult(string HlsManifestKey, string ThumbnailKey, int DurationSeconds);

public interface IVideoProcessor
{
    Task<ProcessingResult> ProcessAsync(ContentItem item, CancellationToken ct = default);
}
