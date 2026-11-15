using System.ComponentModel.DataAnnotations;
using NotificationPlatform.Domain.DomainModels;

namespace NotificationPlatform.Api.DTOs;

/// <summary>Validation rules from BE-02 section 5.7.</summary>
public record RegisterRequest(
    [Required, MaxLength(100)] string Name,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(8), MaxLength(100)] string Password);

public record LoginRequest(
    [Required] string Email,
    [Required] string Password);

/// <summary>The public view of a tenant. Never includes the password or API key hashes.</summary>
public record TenantResponse(string Id, string Name, string Email, string ApiKeyPrefix, DateTime CreatedAt)
{
    public static TenantResponse From(Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.Email, tenant.ApiKeyPrefix, tenant.CreatedAt);
}

/// <summary>Login response.</summary>
public record AuthResponse(string Token, DateTime ExpiresAt, TenantResponse Tenant);

/// <summary>Register response. <see cref="ApiKey"/> is shown once and never again.</summary>
public record RegisterResponse(string Token, DateTime ExpiresAt, TenantResponse Tenant, string ApiKey);
