using System.Security.Claims;
using NotificationPlatform.Services.Auth;

namespace NotificationPlatform.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The authenticated tenant's id, from the <c>tenant_id</c> claim set by both the JWT and the API key scheme.
    /// Controllers never take the tenant id from the request body or query string.
    /// </summary>
    public static string GetTenantId(this ClaimsPrincipal user) =>
        user.FindFirstValue(TokenService.TenantIdClaim)
        ?? throw new InvalidOperationException("The authenticated user has no tenant_id claim.");
}
