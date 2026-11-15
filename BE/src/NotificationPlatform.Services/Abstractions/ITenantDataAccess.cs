using NotificationPlatform.Domain.DomainModels;

namespace NotificationPlatform.Services.Abstractions;

public interface ITenantDataAccess
{
    Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Exact match. Callers pass the normalised (trimmed, lowercase) email.</summary>
    Task<Tenant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<Tenant?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the tenant and sets its <see cref="Tenant.Id"/>. Returns false when the email is already
    /// registered. The unique index decides, so two simultaneous registrations can't both succeed.
    /// </summary>
    Task<bool> InsertAsync(Tenant tenant, CancellationToken cancellationToken = default);

    Task UpdateApiKeyAsync(string tenantId, string apiKeyHash, string apiKeyPrefix, CancellationToken cancellationToken = default);
}
