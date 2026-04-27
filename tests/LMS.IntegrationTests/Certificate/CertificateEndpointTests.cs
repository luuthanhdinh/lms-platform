using System.Net;
using System.Net.Http.Json;
using LMS.CertificateService.Domain.DTOs;
using CertEntity = LMS.CertificateService.Domain.Entities.Certificate;
using LMS.CertificateService.Domain.Enums;
using LMS.CertificateService.Infrastructure.Data;
using LMS.IntegrationTests.Certificate.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LMS.IntegrationTests.Certificate;

public sealed class CertificateEndpointTests : IClassFixture<CertificateFactory>
{
    private readonly CertificateFactory _factory;

    public CertificateEndpointTests(CertificateFactory factory)
    {
        _factory = factory;
    }

    private async Task<CertEntity> SeedCertificateAsync(Guid tenantId, Guid userId, Guid courseId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CertificateDbContext>();

        var cert = new CertEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            CourseId = courseId,
            CertificateNumber = $"CERT-TEST-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            VerificationCode = Guid.NewGuid(),
            Status = CertificateStatus.Active,
            IssuedAt = DateTimeOffset.UtcNow,
            CourseName = "Test Course",
            LearnerName = "Test Learner",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await db.Certificates.AddAsync(cert);
        await db.SaveChangesAsync();
        return cert;
    }

    [Fact]
    public async Task GetCertificates_Returns_List_For_User()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cert = await SeedCertificateAsync(tenantId, userId, Guid.NewGuid());

        var client = _factory.CreateClientWithHeaders(tenantId, userId);
        var response = await client.GetAsync("/certificates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await response.Content.ReadFromJsonAsync<List<CertificateDto>>();
        Assert.NotNull(dtos);
        Assert.Contains(dtos, d => d.Id == cert.Id);
    }

    [Fact]
    public async Task VerifyEndpoint_Returns_CertificateVerifyDto_Anonymous()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cert = await SeedCertificateAsync(tenantId, userId, Guid.NewGuid());

        // Anonymous client — no headers
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/verify/{cert.VerificationCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<CertificateVerifyDto>();
        Assert.NotNull(dto);
        Assert.Equal(cert.CertificateNumber, dto!.CertificateNumber);
        Assert.True(dto.IsValid);
    }

    [Fact]
    public async Task RevokeCertificate_Admin_Returns_204()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cert = await SeedCertificateAsync(tenantId, userId, Guid.NewGuid());

        var client = _factory.CreateClientWithHeaders(tenantId, Guid.NewGuid(), "admin");
        var response = await client.PostAsJsonAsync($"/certificates/{cert.Id}/revoke",
            new { Reason = "Test revocation" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CertificateDbContext>();
        var updated = await db.Certificates.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == cert.Id);
        Assert.NotNull(updated);
        Assert.Equal(CertificateStatus.Revoked, updated!.Status);
        Assert.NotNull(updated.RevokedAt);
    }

    [Fact]
    public async Task RevokeCertificate_Instructor_Returns_403()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cert = await SeedCertificateAsync(tenantId, userId, Guid.NewGuid());

        var client = _factory.CreateClientWithHeaders(tenantId, Guid.NewGuid(), "instructor");
        var response = await client.PostAsJsonAsync($"/certificates/{cert.Id}/revoke",
            new { Reason = "Unauthorized" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCertificates_TenantB_Cannot_See_TenantA_Certs()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedCertificateAsync(tenantA, userId, Guid.NewGuid());

        // Tenant B client with same userId
        var client = _factory.CreateClientWithHeaders(tenantB, userId);
        var response = await client.GetAsync("/certificates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await response.Content.ReadFromJsonAsync<List<CertificateDto>>();
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }
}
