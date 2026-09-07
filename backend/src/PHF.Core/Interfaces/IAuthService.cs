using PHF.Core.DTOs;
using PHF.Core.Entities;

namespace PHF.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
