using LMS.CertificateService.Infrastructure.Data;
using LMS.Contracts.Progress;
using LMS.IntegrationTests.Certificate.Fixtures;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LMS.IntegrationTests.Certificate;

public sealed class CourseCompletedConsumerTests : IClassFixture<CertificateFactory>
{
    private readonly CertificateFactory _factory;

    public CourseCompletedConsumerTests(CertificateFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CourseCompleted_Issues_Certificate_And_Publishes_Event()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new CourseCompleted(
                userId, courseId, tenantId,
                "Test Course", "Test Learner",
                DateTimeOffset.UtcNow));

            // Wait for consumer to process
            Assert.True(await harness.Consumed.Any<CourseCompleted>(), "Consumer should have consumed the message");
            Assert.True(await harness.Published.Any<LMS.Contracts.Certificate.CertificateIssued>(), "CertificateIssued should be published");

            var db = scope.ServiceProvider.GetRequiredService<CertificateDbContext>();
            var certs = await db.Certificates
                .IgnoreQueryFilters()
                .Where(c => c.UserId == userId && c.CourseId == courseId)
                .ToListAsync();

            Assert.Single(certs);
            Assert.Equal("Test Course", certs[0].CourseName);
            Assert.Equal("Test Learner", certs[0].LearnerName);
            Assert.True(_factory.StorageStub.HasKey($"certificates/{tenantId}/{certs[0].Id}.pdf"));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task CourseCompleted_Duplicate_Is_Idempotent()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var msg = new CourseCompleted(userId, courseId, tenantId, null, null, DateTimeOffset.UtcNow);

            await harness.Bus.Publish(msg);
            Assert.True(await harness.Consumed.Any<CourseCompleted>());

            // Send again — duplicate
            await harness.Bus.Publish(msg);
            await Task.Delay(500); // allow second message to process

            var db = scope.ServiceProvider.GetRequiredService<CertificateDbContext>();
            var count = await db.Certificates
                .IgnoreQueryFilters()
                .CountAsync(c => c.UserId == userId && c.CourseId == courseId);

            Assert.Equal(1, count);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
