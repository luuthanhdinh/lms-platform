using LMS.ContentService.Domain.Abstractions;
using LMS.ContentService.Domain.Repositories;
using LMS.ContentService.Infrastructure.Data;
using LMS.ContentService.Infrastructure.Processors;
using LMS.ContentService.Infrastructure.Repositories;
using LMS.ContentService.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace LMS.ContentService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    /// <summary>
    /// Registers MongoDB, repositories, blob storage, and video processor.
    /// MassTransit is intentionally excluded — Api and Worker configure their
    /// own bus with different consumer sets.
    /// </summary>
    public static IServiceCollection AddContentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // MongoDB
        var mongoConnectionString = configuration.GetConnectionString("lms-content")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:lms-content");
        var mongoUrl = MongoUrl.Create(mongoConnectionString);
        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
        services.AddSingleton(sp =>
            new ContentMongoContext(sp.GetRequiredService<IMongoClient>(), mongoUrl.DatabaseName ?? "lms_content"));

        // Repositories
        services.AddScoped<IContentItemRepository, ContentItemRepository>();

        // Azure Blob Storage
        services.Configure<AzureBlobOptions>(configuration.GetSection("AzureBlob"));
        services.AddSingleton<IContentStorageService, AzureBlobContentStorageService>();

        // Video processor (Phase 1 stub)
        services.AddSingleton<IVideoProcessor, StubVideoProcessor>();

        return services;
    }

    /// <summary>Call on Api startup to ensure MongoDB indexes exist.</summary>
    public static async Task EnsureContentIndexesAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var ctx = services.GetRequiredService<ContentMongoContext>();
        await IndexInitializer.EnsureIndexesAsync(ctx, ct);
    }
}
