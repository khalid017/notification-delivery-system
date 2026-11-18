using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NotificationPlatform.Services.Abstractions;
using NotificationPlatform.Services.Auth;

namespace NotificationPlatform.Api.Auth;

/// <summary>
/// The <c>ApiKey</c> authentication scheme for third-party apps (BE-02 section 2.3). Reads <c>X-API-Key</c>,
/// hashes it, finds the tenant by hash and puts the tenant id in a <c>tenant_id</c> claim.
/// Endpoints opt in with <c>[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]</c>.
/// </summary>
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder urlEncoder,
    ApiKeyService apiKeyService,
    ITenantDataAccess tenants)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, urlEncoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-API-Key";

    // Never include the key itself in a failure message: these messages are logged.
    private const string InvalidKeyMessage = "Invalid API key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? apiKey = Request.Headers[HeaderName];
        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.NoResult(); // no credentials offered
        }

        if (!apiKeyService.HasValidFormat(apiKey))
        {
            return AuthenticateResult.Fail(InvalidKeyMessage); // rejected without a database call
        }

        // Read-only lookup, so cancelling it when the client disconnects is safe.
        var tenant = await tenants.GetByApiKeyHashAsync(apiKeyService.Hash(apiKey), Context.RequestAborted);

        // The lookup already matched on the hash; the fixed-time comparison is defence in depth in case
        // the lookup ever changes (for example, finding by prefix and then comparing).
        if (tenant is null || !apiKeyService.Matches(apiKey, tenant.ApiKeyHash))
        {
            return AuthenticateResult.Fail(InvalidKeyMessage);
        }

        var identity = new ClaimsIdentity([new Claim(TokenService.TenantIdClaim, tenant.Id)], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
