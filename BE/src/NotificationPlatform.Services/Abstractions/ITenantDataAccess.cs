using NotificationPlatform.Domain.DomainModels;

namespace NotificationPlatform.Services.Abstractions;

public interface ITenantDataAccess
{
    Task<Tenant?> GetByIdAsync(string id, CancellationToken ct = default);

    Task<Tenant?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task<Tenant?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken ct = default);

    /// <summary>Inserts the tenant and sets its <see cref="Tenant.Id"/>.</summary>
    Task InsertAsync(Tenant tenant, CancellationToken ct = default);

    Task UpdateApiKeyAsync(string tenantId, string apiKeyHash, string apiKeyPrefix, CancellationToken ct = default);
}
