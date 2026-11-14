using MongoDB.Bson;
using MongoDB.Driver;

namespace NotificationPlatform.DataAccess;

/// <summary>
/// Base for the Mongo data access classes. Each one inherits this with its model type,
/// e.g. <c>TenantDataAccess : MongoDataAccessBase&lt;Tenant&gt;, ITenantDataAccess</c>.
/// </summary>
internal abstract class MongoDataAccessBase<T>(MongoContext context) where T : class
{
    protected IMongoCollection<T> Collection { get; } = context.GetCollection<T>();

    /// <summary>New document id: an ObjectId as a hex string (BE-01 §6.4 rule 2).</summary>
    protected static string NewId() => ObjectId.GenerateNewId().ToString();
}
