namespace LMS.Contracts.Identity;

/// <summary>
/// Published by IdentityService when an admin or the platform deactivates a user account.
/// Consumers: EnrollmentService (suspend active enrollments), NotificationWorker (account notice).
/// </summary>
public sealed record UserDeactivated(
    /// <summary>Unique event identifier used for idempotency (MassTransit inbox key).</summary>
    Guid EventId,

    /// <summary>Tenant the deactivated user belongs to. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Surrogate key of the user whose account has been deactivated.</summary>
    Guid UserId,

    /// <summary>UserId of the actor who initiated the deactivation (admin or system actor).</summary>
    Guid DeactivatedBy,

    /// <summary>UTC instant the deactivation was committed to the database.</summary>
    DateTimeOffset OccurredAt);
