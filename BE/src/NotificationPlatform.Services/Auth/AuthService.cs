using NotificationPlatform.Domain.DomainModels;
using NotificationPlatform.Services.Abstractions;

namespace NotificationPlatform.Services.Auth;

/// <summary>A logged-in tenant and its dashboard token.</summary>
public record AuthSession(Tenant Tenant, IssuedToken Token);

/// <summary>A newly registered tenant, its token, and the API key (shown once, never stored).</summary>
public record RegisteredTenant(Tenant Tenant, IssuedToken Token, string ApiKey);

/// <summary>
/// Dashboard registration and login. Expected outcomes (email taken, wrong credentials) are returned
/// as null, not thrown, so the controller maps them to 409 and 401.
/// </summary>
public class AuthService(ITenantDataAccess tenants, TokenService tokens, ApiKeyService apiKeys, TimeProvider time)
{
    // A valid hash of a random password. Verifying against it when the email is unknown makes a failed
    // login take as long as a wrong password, so response time doesn't reveal which emails exist.
    private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(Guid.NewGuid().ToString());

    /// <summary>Returns null when the email is already registered.</summary>
    public async Task<RegisteredTenant?> RegisterAsync(
        string name, string email, string password, CancellationToken cancellationToken = default)
    {
        var apiKey = apiKeys.Generate();

        var tenant = new Tenant
        {
            Name = name.Trim(),
            Email = NormaliseEmail(email),
            // Enhanced = SHA-384 pre-hash, because plain BCrypt ignores everything after 72 bytes.
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password),
            ApiKeyHash = apiKey.Hash,
            ApiKeyPrefix = apiKey.Prefix,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };

        if (!await tenants.InsertAsync(tenant, cancellationToken))
        {
            return null;
        }

        return new RegisteredTenant(tenant, tokens.CreateToken(tenant), apiKey.Key);
    }

    /// <summary>Returns null for an unknown email or a wrong password (deliberately indistinguishable).</summary>
    public async Task<AuthSession?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var tenant = await tenants.GetByEmailAsync(NormaliseEmail(email), cancellationToken);

        if (tenant is null)
        {
            BCrypt.Net.BCrypt.EnhancedVerify(password, DummyPasswordHash);
            return null;
        }

        if (!BCrypt.Net.BCrypt.EnhancedVerify(password, tenant.PasswordHash))
        {
            return null;
        }

        return new AuthSession(tenant, tokens.CreateToken(tenant));
    }

    public Task<Tenant?> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default) =>
        tenants.GetByIdAsync(tenantId, cancellationToken);

    private static string NormaliseEmail(string email) => email.Trim().ToLowerInvariant();
}
