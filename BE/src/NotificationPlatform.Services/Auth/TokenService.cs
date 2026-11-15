using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NotificationPlatform.Domain.DomainModels;

namespace NotificationPlatform.Services.Auth;

/// <summary>A signed dashboard token and when it expires (UTC, whole seconds).</summary>
public record IssuedToken(string Token, DateTime ExpiresAt);

/// <summary>
/// Creates the dashboard JWT (HS256) and describes how it is validated. <see cref="CreateValidationParameters"/>
/// is the single source of truth, used by the Api's JwtBearer scheme and by the unit tests.
/// </summary>
public class TokenService(IOptions<JwtOptions> options, TimeProvider time)
{
    public const string TenantIdClaim = "tenant_id";

    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken CreateToken(Tenant tenant)
    {
        var settings = options.Value;

        // JWT times are whole seconds, so drop the fraction to keep ExpiresAt equal to the exp claim.
        var nowUtc = time.GetUtcNow().UtcDateTime;
        var now = nowUtc.AddTicks(-(nowUtc.Ticks % TimeSpan.TicksPerSecond));
        var expiresAt = now.AddMinutes(settings.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtOptions.Issuer,
            Audience = JwtOptions.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = tenant.Id,
                [TenantIdClaim] = tenant.Id,
                [JwtRegisteredClaimNames.Email] = tenant.Email,
                [JwtRegisteredClaimNames.Name] = tenant.Name,
            },
            SigningCredentials = new SigningCredentials(SigningKey(settings.Secret), SecurityAlgorithms.HmacSha256),
        };

        return new IssuedToken(_handler.CreateToken(descriptor), expiresAt);
    }

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = JwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = JwtOptions.Audience,
        ValidateLifetime = true,
        ClockSkew = ClockSkew, // default is 5 minutes, which would keep expired tokens working
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey(options.Value.Secret),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], // only HS256: blocks "alg: none" and algorithm confusion
    };

    private static SymmetricSecurityKey SigningKey(string secret) => new(Encoding.UTF8.GetBytes(secret));
}
