namespace LMS.Contracts.Content;

/// <summary>Discriminates the kind of binary asset stored in blob storage.</summary>
public enum ContentType
{
    /// <summary>MP4 / MOV / WebM video that will be transcoded to HLS by ContentService.Worker.</summary>
    Video,

    /// <summary>PDF document served as-is to learners.</summary>
    Pdf,

    /// <summary>JPEG, PNG, GIF, or WebP image.</summary>
    Image,

    /// <summary>Any asset type not covered by the explicit values above.</summary>
    Other,
}
