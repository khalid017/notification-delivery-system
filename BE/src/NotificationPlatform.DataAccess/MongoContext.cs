using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace NotificationPlatform.DataAccess;

/// <summary>
/// Holds the single <see cref="MongoClient"/> and database. Registered as a singleton, so the
/// client (and its connection pool) is reused across requests and Lambda invocations.
/// Collections are resolved by type: <c>Tenant</c> → <c>tenants</c>, <c>DeliveryLog</c> → <c>deliveryLogs</c>.
/// </summary>
public class MongoContext
{
    private readonly IMongoDatabase _database;

    public MongoContext(IOptions<MongoOptions> options)
    {
        MongoMappings.Register();

        var settings = options.Value;
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.Database);
    }

    public IMongoCollection<T> GetCollection<T>() => _database.GetCollection<T>(CollectionName<T>());

    /// <summary>camelCase type name plus "s". Renaming a domain class renames its collection.</summary>
    public static string CollectionName<T>()
    {
        var name = typeof(T).Name;
        return char.ToLowerInvariant(name[0]) + name[1..] + "s";
    }
}
