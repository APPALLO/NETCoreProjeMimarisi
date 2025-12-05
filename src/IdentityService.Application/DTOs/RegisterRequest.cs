using System.ComponentModel.DataAnnotations;

namespace IdentityService.Application.DTOs;

public record RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [Required, MinLength(2)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MinLength(2)]
    public string LastName { get; init; } = string.Empty;
}

public record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public record AuthResponse
{
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public string Email { get; init; } = string.Empty;
}
