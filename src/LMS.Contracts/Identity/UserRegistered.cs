namespace LMS.Contracts.Identity;

/// <summary>
/// Published by IdentityService when a new user account is created within a tenant.
/// Consumers: NotificationWorker (welcome email).
/// </summary>
public sealed record UserRegistered(
    /// <summary>Unique event identifier used for idempotency (MassTransit inbox key).</summary>
    Guid EventId,

    /// <summary>Tenant the new user belongs to. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Surrogate key of the newly created user in IdentityService.</summary>
    Guid UserId,

    /// <summary>Verified email address of the new user.</summary>
    string Email,

    /// <summary>Human-readable display name chosen during registration.</summary>
    string DisplayName,

    /// <summary>UTC instant the registration was committed to the database.</summary>
    DateTimeOffset OccurredAt);
