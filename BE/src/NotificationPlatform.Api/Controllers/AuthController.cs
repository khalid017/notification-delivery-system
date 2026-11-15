using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationPlatform.Api.Auth;
using NotificationPlatform.Api.DTOs;
using NotificationPlatform.Services.Auth;

namespace NotificationPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var registered = await authService.RegisterAsync(request.Name, request.Email, request.Password, cancellationToken);

        if (registered is null)
        {
            return Conflict(new ErrorResponse("An account with this email already exists."));
        }

        return Ok(new RegisterResponse(
            registered.Token.Token,
            registered.Token.ExpiresAt,
            TenantResponse.From(registered.Tenant),
            registered.ApiKey));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.LoginAsync(request.Email, request.Password, cancellationToken);

        if (session is null)
        {
            return Unauthorized(new ErrorResponse("Incorrect email or password."));
        }

        return Ok(new AuthResponse(session.Token.Token, session.Token.ExpiresAt, TenantResponse.From(session.Tenant)));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var tenant = await authService.GetTenantAsync(User.GetTenantId(), cancellationToken);

        // A valid token for a tenant that no longer exists: the identity is gone, so treat it as unauthenticated.
        return tenant is null ? Unauthorized() : Ok(TenantResponse.From(tenant));
    }
}
