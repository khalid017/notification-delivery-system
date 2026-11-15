using NotificationPlatform.Api.DTOs;

namespace NotificationPlatform.Api.Middleware;

/// <summary>
/// Outermost middleware. Turns any unhandled exception into <c>500 { error: "Something went wrong" }</c>.
/// Details go to the logs only. Expected errors (409, 404, 503, ...) are returned by controllers, not thrown.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected, so nobody is waiting for a response.
            logger.LogInformation("Request {Method} {Path} was cancelled by the client",
                context.Request.Method, context.Request.Path);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                // Status and headers are already sent, so the response can't be replaced.
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("Something went wrong"));
        }
    }
}
