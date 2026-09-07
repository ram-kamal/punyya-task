using System.ComponentModel.DataAnnotations;

namespace PHF.Core.DTOs;

public record LoginRequest
{
    [Required]
    public string Email { get; init; } = string.Empty; // Accepts username or email

    [Required]
    public string Password { get; init; } = string.Empty;

    public string? StationId { get; init; }
}

public record AuthResponse
{
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public UserProfileDto User { get; init; } = null!;
}

public record UserProfileDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Username { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string WorkMode { get; init; } = "IN_OFFICE";
    public string? StationId { get; init; }
}
