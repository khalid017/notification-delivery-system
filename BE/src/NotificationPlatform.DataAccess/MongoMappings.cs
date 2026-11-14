using MongoDB.Bson.Serialization.Conventions;
using NotificationPlatform.Domain.DomainModels;

namespace NotificationPlatform.DataAccess;

/// <summary>
/// BSON conventions for the Domain's <c>DomainModels</c> classes, kept here so Domain stays free of
/// MongoDB attributes: camelCase element names, and unknown fields are ignored when reading.
/// Ids rely on driver defaults: a property named <c>Id</c> maps to <c>_id</c> and a <c>string</c> is
/// stored as a BSON string. Ids are always set with <see cref="MongoDataAccessBase{T}"/>.NewId() before insert.
/// Must run before the driver first uses a model, so <see cref="MongoContext"/> calls it in its constructor.
/// </summary>
internal static class MongoMappings
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        ConventionRegistry.Register(
            "NotificationPlatform",
            new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
            },
            type => type.Namespace == typeof(Tenant).Namespace);
    }
}
