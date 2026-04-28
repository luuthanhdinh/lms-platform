using System.Text.Json;
using Xunit;

namespace LMS.IntegrationTests.Course.Unit;

[Trait("Category", "Course")]
public class SnapshotSerializationTests
{
    // Replicate the snapshot serialization logic from CourseEndpoints.cs publish flow
    private static string SerializeCourseStructure(LMS.CourseService.Domain.Entities.Course course)
    {
        return JsonSerializer.Serialize(new
        {
            sections = course.Sections.OrderBy(s => s.Order).Select(s => new
            {
                id = s.Id,
                title = s.Title,
                order = s.Order,
                lessons = s.Lessons.OrderBy(l => l.Order).Select(l => new
                {
                    id = l.Id,
                    title = l.Title,
                    order = l.Order,
                    contentItemId = l.ContentItemId,
                    durationSeconds = l.DurationSeconds,
                    isFreePreview = l.IsFreePreview,
                    isOptional = l.IsOptional,
                })
            })
        });
    }

    [Fact]
    public void SnapshotJson_ContainsSectionsAndLessons()
    {
        var course = CourseBuilder.Build(sectionCount: 2, lessonsPerSection: 3);

        var json = SerializeCourseStructure(course);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("sections", out var sections));
        Assert.Equal(JsonValueKind.Array, sections.ValueKind);
        Assert.Equal(2, sections.GetArrayLength());

        foreach (var section in sections.EnumerateArray())
        {
            Assert.True(section.TryGetProperty("lessons", out var lessons));
            Assert.Equal(JsonValueKind.Array, lessons.ValueKind);
            Assert.Equal(3, lessons.GetArrayLength());
        }
    }

    [Fact]
    public void SnapshotJson_ContainsExpectedSectionKeys()
    {
        var course = CourseBuilder.Build(sectionCount: 1, lessonsPerSection: 1);

        var json = SerializeCourseStructure(course);
        using var doc = JsonDocument.Parse(json);
        var section = doc.RootElement.GetProperty("sections").EnumerateArray().First();

        Assert.True(section.TryGetProperty("id", out _));
        Assert.True(section.TryGetProperty("title", out _));
        Assert.True(section.TryGetProperty("order", out _));
        Assert.True(section.TryGetProperty("lessons", out _));
    }

    [Fact]
    public void SnapshotJson_ContainsExpectedLessonKeys()
    {
        var course = CourseBuilder.Build(sectionCount: 1, lessonsPerSection: 1);

        var json = SerializeCourseStructure(course);
        using var doc = JsonDocument.Parse(json);
        var lesson = doc.RootElement
            .GetProperty("sections").EnumerateArray().First()
            .GetProperty("lessons").EnumerateArray().First();

        Assert.True(lesson.TryGetProperty("id", out _));
        Assert.True(lesson.TryGetProperty("title", out _));
        Assert.True(lesson.TryGetProperty("order", out _));
        Assert.True(lesson.TryGetProperty("contentItemId", out _));
        Assert.True(lesson.TryGetProperty("isFreePreview", out _));
        Assert.True(lesson.TryGetProperty("isOptional", out _));
    }

    [Fact]
    public void SnapshotJson_OrderedByOrder_Sections()
    {
        var course = CourseBuilder.Build(sectionCount: 3, lessonsPerSection: 1);

        var json = SerializeCourseStructure(course);
        using var doc = JsonDocument.Parse(json);
        var sections = doc.RootElement.GetProperty("sections").EnumerateArray().ToList();

        var orders = sections.Select(s => s.GetProperty("order").GetInt32()).ToList();
        var sortedOrders = orders.OrderBy(o => o).ToList();

        Assert.Equal(sortedOrders, orders);
    }

    [Fact]
    public void SnapshotJson_OrderedByOrder_Lessons()
    {
        var course = CourseBuilder.Build(sectionCount: 1, lessonsPerSection: 4);

        var json = SerializeCourseStructure(course);
        using var doc = JsonDocument.Parse(json);
        var lessons = doc.RootElement
            .GetProperty("sections").EnumerateArray().First()
            .GetProperty("lessons").EnumerateArray().ToList();

        var orders = lessons.Select(l => l.GetProperty("order").GetInt32()).ToList();
        var sortedOrders = orders.OrderBy(o => o).ToList();

        Assert.Equal(sortedOrders, orders);
    }

    [Fact]
    public void SnapshotJson_EmptySections_ProducesEmptyArray()
    {
        var course = CourseBuilder.Build(sectionCount: 0, lessonsPerSection: 0);

        var json = SerializeCourseStructure(course);
        using var doc = JsonDocument.Parse(json);
        var sections = doc.RootElement.GetProperty("sections");

        Assert.Equal(JsonValueKind.Array, sections.ValueKind);
        Assert.Equal(0, sections.GetArrayLength());
    }

    [Fact]
    public void SnapshotJson_IsValidJson()
    {
        var course = CourseBuilder.Build(sectionCount: 2, lessonsPerSection: 2);

        var json = SerializeCourseStructure(course);

        // JsonDocument.Parse throws if invalid JSON
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }
}
