using System.Text.Json.Serialization;

namespace LMS.EnrollmentService.Domain.DTOs;

// CS0542: property cannot share the type name — serialized as "page" via JsonPropertyName
public record Page<T>(
    IReadOnlyList<T> Items,
    [property: JsonPropertyName("page")] int PageNumber,
    int PageSize,
    int Total);
