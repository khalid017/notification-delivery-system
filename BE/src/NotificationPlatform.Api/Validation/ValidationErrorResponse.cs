using Microsoft.AspNetCore.Mvc;
using NotificationPlatform.Api.DTOs;

namespace NotificationPlatform.Api.Validation;

/// <summary>
/// Replaces the default <c>ValidationProblemDetails</c> produced by <c>[ApiController]</c> with
/// <c>400 { error: "Validation failed", fields: { camelCaseField: [messages] } }</c>.
/// </summary>
public static class ValidationErrorResponse
{
    private const string BodyKey = "body";

    public static IActionResult Create(ActionContext context)
    {
        // When the JSON body is missing or can't be read (keys "" or "$..."), the action parameter itself
        // is also reported as "The request field is required.". That's noise next to the real error, so drop it.
        var hasJsonErrors = context.ModelState
            .Any(entry => entry.Value is { Errors.Count: > 0 } && (entry.Key.Length == 0 || entry.Key.StartsWith('$')));
        var parameterNames = context.ActionDescriptor.Parameters
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.Ordinal);

        var fields = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .Where(entry => !(hasJsonErrors && parameterNames.Contains(entry.Key)))
            .GroupBy(entry => ToFieldName(entry.Key))
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(entry => entry.Value!.Errors)
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The value is invalid." : error.ErrorMessage)
                    .Distinct()
                    .ToArray());

        return new BadRequestObjectResult(new ErrorResponse("Validation failed", fields));
    }

    /// <summary>
    /// ModelState keys are property names (<c>Password</c>) for DataAnnotations errors and JSON paths
    /// (<c>$.password</c>, <c>$</c>) for JSON errors. Converts both to camelCase field names.
    /// </summary>
    private static string ToFieldName(string key)
    {
        if (key.StartsWith("$.", StringComparison.Ordinal))
        {
            key = key[2..];
        }

        if (key is "" or "$")
        {
            return BodyKey;
        }

        var segments = key.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length > 0)
            {
                segments[i] = char.ToLowerInvariant(segment[0]) + segment[1..];
            }
        }

        return string.Join('.', segments);
    }
}
