using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

internal static class AppHostExtensions
{
    /// <summary>
    /// Runs `npm install` before the npm app starts so node_modules are always present.
    /// </summary>
    internal static IResourceBuilder<NodeAppResource> WithNpmPackages(
        this IResourceBuilder<NodeAppResource> builder)
    {
        builder.ApplicationBuilder.Eventing.Subscribe<BeforeResourceStartedEvent>(
            builder.Resource,
            async (evt, ct) =>
            {
                var logger = evt.Services
                    .GetRequiredService<ResourceLoggerService>()
                    .GetLogger(builder.Resource);

                var workingDir = builder.Resource.WorkingDirectory;
                logger.LogInformation("Running npm install in {Dir}", workingDir);

                var result = await RunProcessAsync("npm", "install", workingDir, ct);
                if (result != 0)
                    throw new InvalidOperationException(
                        $"npm install failed with exit code {result} in {workingDir}");
            });

        return builder;
    }

    /// <summary>
    /// After Keycloak starts, applies the User Profile schema (including tenant_id) via REST.
    /// The realm JSON import does not support userProfile in Keycloak 26, so we do it post-start.
    /// </summary>
    internal static IResourceBuilder<KeycloakResource> WithUserProfileSchema(
        this IResourceBuilder<KeycloakResource> builder,
        string realm,
        IResourceBuilder<ParameterResource> adminPassword)
    {
        builder.ApplicationBuilder.Eventing.Subscribe<AfterResourcesCreatedEvent>(async (evt, ct) =>
        {
            var logger = evt.Services
                .GetRequiredService<ResourceLoggerService>()
                .GetLogger(builder.Resource);

            var notificationService = evt.Services
                .GetRequiredService<ResourceNotificationService>();

            await notificationService.WaitForResourceAsync(builder.Resource.Name,
                r => r.Snapshot.State?.Text == "Running", ct);

            var endpoint = builder.Resource.GetEndpoint("http");
            var baseUrl  = $"http://localhost:{endpoint.Port}";
            var password = await adminPassword.Resource.GetValueAsync(ct);

            logger.LogInformation("Applying User Profile schema to realm '{Realm}'", realm);

            using var http = new HttpClient();

            string? token = null;
            for (var i = 0; i < 20; i++)
            {
                try
                {
                    var resp = await http.PostAsync(
                        $"{baseUrl}/realms/master/protocol/openid-connect/token",
                        new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            ["client_id"]  = "admin-cli",
                            ["grant_type"] = "password",
                            ["username"]   = "admin",
                            ["password"]   = password!,
                        }), ct);

                    if (resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadFromJsonAsync<TokenResponse>(ct);
                        token = body!.AccessToken;
                        break;
                    }
                }
                catch { /* not ready yet */ }

                await Task.Delay(3000, ct);
            }

            if (token is null)
            {
                logger.LogWarning("Could not obtain Keycloak admin token — skipping User Profile schema");
                return;
            }

            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var schema = new
            {
                attributes = new object[]
                {
                    new { name = "username",  displayName = "${username}",  multivalued = false,
                          validations = new { length = new { min = 3, max = 255 } },
                          permissions = new { view = new[]{"admin","user"}, edit = new[]{"admin","user"} } },
                    new { name = "email",     displayName = "${email}",     multivalued = false,
                          validations = new { email = new{}, length = new { max = 255 } },
                          required    = new { roles = new[]{"user"} },
                          permissions = new { view = new[]{"admin","user"}, edit = new[]{"admin","user"} } },
                    new { name = "firstName", displayName = "${firstName}", multivalued = false,
                          validations = new { length = new { max = 255 } },
                          required    = new { roles = new[]{"user"} },
                          permissions = new { view = new[]{"admin","user"}, edit = new[]{"admin","user"} } },
                    new { name = "lastName",  displayName = "${lastName}",  multivalued = false,
                          validations = new { length = new { max = 255 } },
                          required    = new { roles = new[]{"user"} },
                          permissions = new { view = new[]{"admin","user"}, edit = new[]{"admin","user"} } },
                    new { name = "tenant_id", displayName = "Tenant ID",   multivalued = false,
                          validations = new { length = new { max = 36 } },
                          permissions = new { view = new[]{"admin"}, edit = new[]{"admin"} } },
                },
                groups = new object[]
                {
                    new { name = "user-metadata", displayHeader = "User metadata",
                          displayDescription = "Attributes, which refer to user metadata" }
                }
            };

            var put = await http.PutAsJsonAsync($"{baseUrl}/admin/realms/{realm}/users/profile", schema, ct);
            if (put.IsSuccessStatusCode)
                logger.LogInformation("User Profile schema applied successfully");
            else
                logger.LogWarning("Failed to apply User Profile schema: {Status}", put.StatusCode);
        });

        return builder;
    }

    private record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken);

    private static async Task<int> RunProcessAsync(
        string fileName, string arguments, string workingDir, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory       = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName}");

        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }
}
