using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Core.Interfaces;
using PHF.Infrastructure.Data;

namespace PHF.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public AuthService(ApplicationDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var identifier = (request.Email ?? string.Empty).Trim().ToLower();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == identifier || (u.Username != null && u.Username.ToLower() == identifier), cancellationToken);

        if (user == null)
            return null;

        if (!user.IsActive)
            throw new InvalidOperationException("This account is suspended or inactive. Please contact your system administrator.");

        if (!VerifyPassword(request.Password, user.PasswordHash))
            return null;

        // If station id provided on client login, update user station
        if (!string.IsNullOrWhiteSpace(request.StationId) && user.StationId != request.StationId)
        {
            user.StationId = request.StationId;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var expiresAt = DateTime.UtcNow.AddDays(7);
        var token = GenerateJwtToken(user, expiresAt);

        return new AuthResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role,
                WorkMode = user.WorkMode ?? "IN_OFFICE",
                StationId = user.StationId
            }
        };
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null) return null;

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            WorkMode = user.WorkMode ?? "IN_OFFICE",
            StationId = user.StationId
        };
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }

    private string GenerateJwtToken(User user, DateTime expiresAt)
    {
        var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") 
                     ?? _configuration["Jwt:Secret"] 
                     ?? "PHF_Work_Management_Enterprise_Super_Secret_Key_2026_Secure_HMAC_SHA256_Auth!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("station_id", user.StationId ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "PHF.Api",
            audience: _configuration["Jwt:Audience"] ?? "PHF.Clients",
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
