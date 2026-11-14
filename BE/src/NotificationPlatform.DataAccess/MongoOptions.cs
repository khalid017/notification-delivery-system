using System.ComponentModel.DataAnnotations;

namespace NotificationPlatform.DataAccess;

/// <summary>Bound from the <c>Mongo</c> section (env vars <c>Mongo__ConnectionString</c>, <c>Mongo__Database</c>).</summary>
public class MongoOptions
{
    public const string SectionName = "Mongo";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string Database { get; set; } = string.Empty;
}
