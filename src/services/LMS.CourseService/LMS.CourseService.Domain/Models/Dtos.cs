using LMS.CourseService.Domain.Enums;

namespace LMS.CourseService.Domain.Models;

public record CourseSummary(Guid Id, string Title, string Category, string[] Tags,
    DifficultyLevel Difficulty, string Language, CourseStatus Status, bool IsFree,
    int Version, int EnrollmentCount, Guid InstructorId, DateTimeOffset? PublishedAt);

public record CourseDetail(Guid Id, string Title, string Category, string[] Tags,
    DifficultyLevel Difficulty, string Language, CourseStatus Status, bool IsFree,
    int Version, int EnrollmentCount, Guid InstructorId, DateTimeOffset? PublishedAt,
    string Description, Guid? ThumbnailContentId,
    IReadOnlyList<SectionDto> Sections, IReadOnlyList<Guid> Prerequisites);

public record SectionDto(Guid Id, string Title, int Order, IReadOnlyList<LessonDto> Lessons);

public record LessonDto(Guid Id, string Title, Guid ContentItemId, int? DurationSeconds,
    int Order, bool IsFreePreview, bool IsOptional);

public record CourseSyllabus(Guid CourseId, int Version, IReadOnlyList<SyllabusSection> Sections);
public record SyllabusSection(Guid Id, string Title, int Order, IReadOnlyList<SyllabusLesson> Lessons);
public record SyllabusLesson(Guid Id, string Title, int Order, int? DurationSeconds, bool IsFreePreview);

public record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int Total);

public record CreateCourseRequest(string Title, string Description, string Category,
    string[] Tags, DifficultyLevel Difficulty, string Language, bool IsFree, Guid? ThumbnailContentId);
public record UpdateCourseRequest(string Title, string Description, string Category,
    string[] Tags, DifficultyLevel Difficulty, string Language, bool IsFree, Guid? ThumbnailContentId);
public record SectionRequest(string Title, int Order);
public record CreateLessonRequest(string Title, Guid ContentItemId, int Order, bool IsFreePreview, bool IsOptional);
public record UpdateLessonRequest(string Title, Guid ContentItemId, int Order, bool IsFreePreview, bool IsOptional);
