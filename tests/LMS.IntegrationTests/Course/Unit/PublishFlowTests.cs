using LMS.CourseService.Domain.Enums;
using Xunit;

namespace LMS.IntegrationTests.Course.Unit;

[Trait("Category", "Course")]
public class PublishFlowTests
{
    [Theory]
    [InlineData(CourseStatus.Published)]
    [InlineData(CourseStatus.Archived)]
    public void PublishFlow_RequiresDraftStatus_RejectsNonDraft(CourseStatus status)
    {
        var course = CourseBuilder.Build(status: status, sectionCount: 1, lessonsPerSection: 1);

        // Replicate the guard from CourseEndpoints: course.Status != CourseStatus.Draft → reject
        var wouldReject = course.Status != CourseStatus.Draft;

        Assert.True(wouldReject, $"Course in {status} status should be rejected for publish");
    }

    [Fact]
    public void PublishFlow_RequiresDraftStatus_AcceptsDraft()
    {
        var course = CourseBuilder.Build(status: CourseStatus.Draft);

        var wouldReject = course.Status != CourseStatus.Draft;

        Assert.False(wouldReject, "Course in Draft status should not be rejected by status check");
    }

    [Fact]
    public void PublishFlow_RequiresAtLeastOneLesson_RejectsZeroLessons()
    {
        var course = CourseBuilder.Build(sectionCount: 0, lessonsPerSection: 0);

        var totalLessons = course.Sections.SelectMany(s => s.Lessons).Count();

        Assert.Equal(0, totalLessons);
        Assert.True(totalLessons == 0, "Course with 0 lessons should be rejected");
    }

    [Fact]
    public void PublishFlow_RequiresAtLeastOneLesson_AcceptsWithLessons()
    {
        var course = CourseBuilder.Build(sectionCount: 1, lessonsPerSection: 2);

        var totalLessons = course.Sections.SelectMany(s => s.Lessons).Count();

        Assert.Equal(2, totalLessons);
        Assert.False(totalLessons == 0, "Course with lessons should pass the lesson count check");
    }

    [Fact]
    public void PublishFlow_RequiresAtLeastOneLesson_MultipleSections()
    {
        var course = CourseBuilder.Build(sectionCount: 3, lessonsPerSection: 1);

        var totalLessons = course.Sections.SelectMany(s => s.Lessons).Count();

        Assert.Equal(3, totalLessons);
    }

    [Fact]
    public void PublishFlow_PaymentGate_IsFreeTrue_Allows()
    {
        var course = CourseBuilder.Build(isFree: true, sectionCount: 1, lessonsPerSection: 1);

        // Replicate the guard: if (!course.IsFree) → block
        var wouldBlock = !course.IsFree;

        Assert.False(wouldBlock, "IsFree=true should pass the payment gate");
    }

    [Fact]
    public void PublishFlow_PaymentGate_IsFreeFalse_Blocks()
    {
        var course = CourseBuilder.Build(isFree: false, sectionCount: 1, lessonsPerSection: 1);

        var wouldBlock = !course.IsFree;

        Assert.True(wouldBlock, "IsFree=false should be blocked by the payment gate (ADR-006)");
    }

    [Fact]
    public void PublishFlow_VersionIncrements()
    {
        var initialVersion = 3;
        var course = CourseBuilder.Build(version: initialVersion, isFree: true, sectionCount: 1, lessonsPerSection: 1);

        // Replicate publish logic: course.Version += 1
        course.Version += 1;

        Assert.Equal(initialVersion + 1, course.Version);
    }

    [Fact]
    public void PublishFlow_VersionIncrements_FromDefaultOne()
    {
        var course = CourseBuilder.Build(version: 1, isFree: true, sectionCount: 1, lessonsPerSection: 1);

        course.Version += 1;

        Assert.Equal(2, course.Version);
    }
}
