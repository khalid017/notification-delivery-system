using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace NotificationPlatform.Services.Auth;

/// <summary>
/// A new API key. <see cref="Key"/> is shown to the tenant once and never stored.
/// Only <see cref="Hash"/> and <see cref="Prefix"/> are saved.
/// </summary>
public record GeneratedApiKey(string Key, string Hash, string Prefix);

/// <summary>
/// Tenant API keys (BE-02 section 2.2): <c>np_</c> + 32 random bytes as base64url, stored as a SHA-256 hex hash.
/// A fast hash is safe here because the key is 256 bits of randomness, and it lets the tenant be found
/// with one indexed lookup on <c>apiKeyHash</c>.
/// </summary>
public class ApiKeyService
{
    public const string KeyPrefix = "np_";
    private const int RandomBytes = 32;
    private const int DisplayChars = 8;

    public GeneratedApiKey Generate()
    {
        var random = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RandomBytes)); // 43 chars, no padding
        var key = KeyPrefix + random;

        return new GeneratedApiKey(key, Hash(key), KeyPrefix + random[..DisplayChars]);
    }

    /// <summary>SHA-256 of the full key (including <c>np_</c>), as 64 lowercase hex characters.</summary>
    public string Hash(string apiKey) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
}
