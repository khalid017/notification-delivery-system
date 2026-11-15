using Microsoft.Extensions.Options;
using NotificationPlatform.Domain.DomainModels;
using NotificationPlatform.Services.Abstractions;
using NotificationPlatform.Services.Auth;
using NSubstitute;

namespace NotificationPlatform.Services.Tests.Auth;

public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly ITenantDataAccess _tenants = Substitute.For<ITenantDataAccess>();
    private readonly ApiKeyService _apiKeys = new();
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        var time = new TestTimeProvider(Now);
        var tokens = new TokenService(
            Options.Create(new JwtOptions { Secret = "test-secret-that-is-at-least-32-characters-long" }), time);

        _authService = new AuthService(_tenants, tokens, _apiKeys, time);

        // By default, inserts succeed and set an id, like the real data access.
        _tenants.InsertAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<Tenant>().Id = "6af8daacb866548119b05e33";
                return true;
            });
    }

    [Fact]
    public async Task Register_StoresNormalisedEmailHashedPasswordAndApiKeyHash()
    {
        var registered = await _authService.RegisterAsync("  Acme Inc ", "  Admin@ACME.com ", "correct-horse");

        Assert.NotNull(registered);
        var tenant = registered.Tenant;
        Assert.Equal("Acme Inc", tenant.Name);
        Assert.Equal("admin@acme.com", tenant.Email);
        Assert.NotEqual("correct-horse", tenant.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify("correct-horse", tenant.PasswordHash));
        Assert.Equal(_apiKeys.Hash(registered.ApiKey), tenant.ApiKeyHash);
        Assert.Equal(registered.ApiKey[..11], tenant.ApiKeyPrefix);
        Assert.Equal(Now.UtcDateTime, tenant.CreatedAt);
        Assert.False(string.IsNullOrEmpty(registered.Token.Token));
        await _tenants.Received(1).InsertAsync(tenant, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_ReturnsNullWhenEmailAlreadyRegistered()
    {
        _tenants.InsertAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>()).Returns(false);

        var registered = await _authService.RegisterAsync("Acme", "admin@acme.com", "correct-horse");

        Assert.Null(registered);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsSessionForThatTenant()
    {
        var tenant = ExistingTenant("correct-horse");
        _tenants.GetByEmailAsync("admin@acme.com", Arg.Any<CancellationToken>()).Returns(tenant);

        var session = await _authService.LoginAsync(" Admin@Acme.com ", "correct-horse");

        Assert.NotNull(session);
        Assert.Same(tenant, session.Tenant);
        Assert.False(string.IsNullOrEmpty(session.Token.Token));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsNull()
    {
        _tenants.GetByEmailAsync("admin@acme.com", Arg.Any<CancellationToken>()).Returns(ExistingTenant("correct-horse"));

        var session = await _authService.LoginAsync("admin@acme.com", "wrong-password");

        Assert.Null(session);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsNull()
    {
        _tenants.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        var session = await _authService.LoginAsync("nobody@acme.com", "correct-horse");

        Assert.Null(session);
    }

    [Fact]
    public async Task GetTenant_ReturnsTenantFromDataAccess()
    {
        var tenant = ExistingTenant("correct-horse");
        _tenants.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        Assert.Same(tenant, await _authService.GetTenantAsync(tenant.Id));
    }

    private static Tenant ExistingTenant(string password) => new()
    {
        Id = "6af8daacb866548119b05e33",
        Name = "Acme Inc",
        Email = "admin@acme.com",
        PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password),
        ApiKeyHash = new string('a', 64),
        ApiKeyPrefix = "np_abcdefgh",
        CreatedAt = Now.UtcDateTime,
    };
}
