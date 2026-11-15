using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using NotificationPlatform.Api.DTOs;

namespace NotificationPlatform.Api.Validation;

/// <summary>
/// Gives bodiless 4xx/5xx responses the Error shape: 401/403 from the authentication handlers,
/// 404 for unknown routes, 405, and controller results like <c>NotFound()</c>.
/// <c>UseStatusCodePages</c> only calls this when the response has no body yet.
/// </summary>
public static class ErrorStatusCodePages
{
    public static Task WriteAsync(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;

        var message = response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not found",
            StatusCodes.Status405MethodNotAllowed => "Method not allowed",
            _ => ReasonPhrases.GetReasonPhrase(response.StatusCode),
        };

        return response.WriteAsJsonAsync(new ErrorResponse(message));
    }
}
