using System.Buffers.Text;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NotificationPlatform.Domain.DomainModels;
using NotificationPlatform.Services.Auth;

namespace NotificationPlatform.Services.Tests.Auth;

public class TokenServiceTests
{
    private const string Secret = "test-secret-that-is-at-least-32-characters-long";

    private static readonly Tenant Tenant = new()
    {
        Id = "6af8daacb866548119b05e33",
        Name = "Acme Inc",
        Email = "admin@acme.com",
    };

    private readonly JsonWebTokenHandler _handler = new();

    [Fact]
    public void CreateToken_ContainsExpectedClaims()
    {
        var token = CreateService().CreateToken(Tenant).Token;

        var jwt = _handler.ReadJsonWebToken(token);
        Assert.Equal(Tenant.Id, jwt.GetPayloadValue<string>("sub"));
        Assert.Equal(Tenant.Id, jwt.GetPayloadValue<string>("tenant_id"));
        Assert.Equal(Tenant.Email, jwt.GetPayloadValue<string>("email"));
        Assert.Equal(Tenant.Name, jwt.GetPayloadValue<string>("name"));
        Assert.Equal(JwtOptions.Issuer, jwt.Issuer);
        Assert.Equal(JwtOptions.Audience, Assert.Single(jwt.Audiences));
        Assert.Equal("HS256", jwt.Alg);
    }

    [Fact]
    public void CreateToken_ExpiresAfterConfiguredMinutes()
    {
        var now = new DateTimeOffset(2026, 11, 12, 8, 15, 30, 500, TimeSpan.Zero);
        var service = CreateService(new TestTimeProvider(now), expiryMinutes: 45);

        var issued = service.CreateToken(Tenant);

        var expected = new DateTime(2026, 11, 12, 9, 0, 30, DateTimeKind.Utc); // 08:15:30 + 45 min, fraction dropped
        Assert.Equal(expected, issued.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, issued.ExpiresAt.Kind);
        Assert.Equal(expected, _handler.ReadJsonWebToken(issued.Token).ValidTo);
    }

    [Fact]
    public async Task ValidToken_PassesValidation()
    {
        var service = CreateService();

        var result = await _handler.ValidateTokenAsync(service.CreateToken(Tenant).Token, service.CreateValidationParameters());

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task ExpiredToken_FailsValidation()
    {
        var twoHoursAgo = new TestTimeProvider(DateTimeOffset.UtcNow.AddHours(-2)); // 60 min token, expired 1 h ago
        var token = CreateService(twoHoursAgo).CreateToken(Tenant).Token;

        var result = await _handler.ValidateTokenAsync(token, CreateService().CreateValidationParameters());

        Assert.False(result.IsValid);
        Assert.IsAssignableFrom<SecurityTokenExpiredException>(result.Exception);
    }

    [Fact]
    public async Task TamperedToken_FailsValidation()
    {
        var service = CreateService();
        var parts = service.CreateToken(Tenant).Token.Split('.');

        // Swap the tenant id in the payload but keep the original signature.
        var payload = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[1]));
        var forged = payload.Replace(Tenant.Id, "000000000000000000000000");
        var tampered = $"{parts[0]}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(forged))}.{parts[2]}";

        var result = await _handler.ValidateTokenAsync(tampered, service.CreateValidationParameters());

        Assert.NotEqual(payload, forged);
        Assert.False(result.IsValid);
        Assert.IsAssignableFrom<SecurityTokenInvalidSignatureException>(result.Exception);
    }

    [Fact]
    public async Task TokenSignedWithAnotherSecret_FailsValidation()
    {
        var otherService = CreateService(secret: "a-completely-different-secret-of-32-plus-chars");
        var token = otherService.CreateToken(Tenant).Token;

        var result = await _handler.ValidateTokenAsync(token, CreateService().CreateValidationParameters());

        Assert.False(result.IsValid);
        Assert.IsAssignableFrom<SecurityTokenInvalidSignatureException>(result.Exception);
    }

    private static TokenService CreateService(TimeProvider? time = null, int expiryMinutes = 60, string secret = Secret) =>
        new(Options.Create(new JwtOptions { Secret = secret, ExpiryMinutes = expiryMinutes }), time ?? TimeProvider.System);
}
