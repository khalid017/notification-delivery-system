using System.Text.Json.Serialization;

namespace NotificationPlatform.Api.DTOs;

/// <summary>
/// The single error shape for every error response (BE-02 section 5.1):
/// <c>{ "error": "message", "fields": { "fieldName": ["message", ...] } }</c>, with <c>fields</c> only on validation failures.
/// </summary>
public record ErrorResponse(
    string Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Fields = null);
