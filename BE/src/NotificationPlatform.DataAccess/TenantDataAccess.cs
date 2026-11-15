using MongoDB.Driver;
using NotificationPlatform.Domain.DomainModels;
using NotificationPlatform.Services.Abstractions;

namespace NotificationPlatform.DataAccess;

internal sealed class TenantDataAccess(MongoContext context) : MongoDataAccessBase<Tenant>(context), ITenantDataAccess
{
    // Name of the unique index on email, as created by MongoIndexInitializer.
    private const string EmailIndexName = "email_1";

    public async Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await Collection.Find(tenant => tenant.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<Tenant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        await Collection.Find(tenant => tenant.Email == email).FirstOrDefaultAsync(cancellationToken);

    public async Task<Tenant?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken = default) =>
        await Collection.Find(tenant => tenant.ApiKeyHash == apiKeyHash).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> InsertAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        tenant.Id = NewId();

        try
        {
            await Collection.InsertOneAsync(tenant, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (
            exception.WriteError.Category == ServerErrorCategory.DuplicateKey &&
            exception.Message.Contains(EmailIndexName, StringComparison.Ordinal))
        {
            return false;
        }
    }

    public async Task UpdateApiKeyAsync(
        string tenantId, string apiKeyHash, string apiKeyPrefix, CancellationToken cancellationToken = default)
    {
        var update = Builders<Tenant>.Update
            .Set(tenant => tenant.ApiKeyHash, apiKeyHash)
            .Set(tenant => tenant.ApiKeyPrefix, apiKeyPrefix);

        await Collection.UpdateOneAsync(tenant => tenant.Id == tenantId, update, cancellationToken: cancellationToken);
    }
}
