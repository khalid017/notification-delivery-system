using System.ComponentModel.DataAnnotations;

namespace NotificationPlatform.Services.Auth;

/// <summary>
/// Dashboard JWT settings, bound from the <c>Jwt</c> section (<c>Jwt__Secret</c>, <c>Jwt__ExpiryMinutes</c>).
/// Bound and validated in the Api host only: the Worker doesn't issue or check tokens and has no secret.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Fixed in code (BE-02 section 2.1). Checked on every request.</summary>
    public const string Issuer = "notification-platform";

    /// <summary>Fixed in code (BE-02 section 2.1). Checked on every request.</summary>
    public const string Audience = "notification-platform-dashboard";

    /// <summary>HMAC-SHA256 signing secret. At least 32 characters (256 bits).</summary>
    [Required]
    [MinLength(32)]
    public string Secret { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 60;
}
