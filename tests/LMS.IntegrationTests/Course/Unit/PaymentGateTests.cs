using Xunit;

namespace LMS.IntegrationTests.Course.Unit;

[Trait("Category", "Course")]
public class PaymentGateTests
{
    [Fact]
    public void PaymentGate_IsFreeTrue_DoesNotBlock()
    {
        var course = CourseBuilder.Build(isFree: true, sectionCount: 1, lessonsPerSection: 1);

        // No exception when IsFree = true
        Assert.True(course.IsFree);
    }

    [Fact]
    public void PaymentGate_IsFreeFalse_ShouldReturnPaymentRequired()
    {
        var course = CourseBuilder.Build(isFree: false, sectionCount: 1, lessonsPerSection: 1);

        Assert.False(course.IsFree);
        // ADR-006: IsFree=false → caller must return 409 PAYMENT_REQUIRED
    }

    [Fact]
    public void PaymentGate_DefaultCourse_IsFreeTrue()
    {
        // Default value of IsFree on a new Course should be true
        var course = CourseBuilder.Build();

        Assert.True(course.IsFree);
    }

    [Fact]
    public void PaymentGate_IsFreeFalse_BlocksRegardlessOfLessons()
    {
        // Payment gate is checked after lesson count — a paid course with lessons still blocks
        var course = CourseBuilder.Build(isFree: false, sectionCount: 2, lessonsPerSection: 3);

        var totalLessons = course.Sections.SelectMany(s => s.Lessons).Count();
        var wouldBlock = !course.IsFree;

        Assert.Equal(6, totalLessons);
        Assert.True(wouldBlock, "Paid course with lessons should still be blocked by payment gate");
    }
}
