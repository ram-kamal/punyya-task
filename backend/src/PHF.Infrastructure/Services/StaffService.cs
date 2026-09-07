using Microsoft.EntityFrameworkCore;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Core.Enums;
using PHF.Core.Interfaces;
using PHF.Infrastructure.Data;

namespace PHF.Infrastructure.Services;

public class StaffService : IStaffService
{
    private readonly ApplicationDbContext _dbContext;

    public StaffService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<StaffMemberDto>> GetAllStaffAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var users = await _dbContext.Users
            .OrderBy(u => u.Role == "ADMIN" ? 0 : u.Role == "MANAGER" ? 1 : 2)
            .ThenBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        var activeTasks = await _dbContext.Tasks
            .Where(t => t.Status != TaskItemStatus.COMPLETED.ToString() && t.AssignedTo != null)
            .GroupBy(t => t.AssignedTo)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId!.Value, x => x.Count, cancellationToken);

        var todayShifts = await _dbContext.Shifts
            .Where(s => s.ClockIn >= today)
            .OrderByDescending(s => s.ClockIn)
            .ToListAsync(cancellationToken);

        var latestShiftMap = todayShifts
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.First().Status);

        return users.Select(u => new StaffMemberDto
        {
            Id = u.Id,
            Email = u.Email,
            Username = u.Username,
            FullName = u.FullName,
            Role = u.Role,
            WorkMode = u.WorkMode ?? "IN_OFFICE",
            StationId = u.StationId,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            ActiveTasksCount = activeTasks.TryGetValue(u.Id, out var count) ? count : 0,
            ShiftStatus = latestShiftMap.TryGetValue(u.Id, out var shiftStatus) ? shiftStatus : null
        });
    }

    public async Task<StaffMemberDto?> GetStaffByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user == null) return null;

        var activeCount = await _dbContext.Tasks
            .CountAsync(t => t.AssignedTo == id && t.Status != TaskItemStatus.COMPLETED.ToString(), cancellationToken);

        var shift = await _dbContext.Shifts
            .Where(s => s.UserId == id && s.ClockIn >= DateTime.UtcNow.Date)
            .OrderByDescending(s => s.ClockIn)
            .FirstOrDefaultAsync(cancellationToken);

        return new StaffMemberDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            WorkMode = user.WorkMode ?? "IN_OFFICE",
            StationId = user.StationId,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            ActiveTasksCount = activeCount,
            ShiftStatus = shift?.Status
        };
    }

    public async Task<StaffMemberDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken = default)
    {
        var cleanEmail = request.Email.Trim().ToLower();
        var cleanUsername = string.IsNullOrWhiteSpace(request.Username) 
            ? cleanEmail.Split('@')[0] 
            : request.Username.Trim().ToLower();

        var exists = await _dbContext.Users.AnyAsync(u => 
            u.Email.ToLower() == cleanEmail || 
            (u.Username != null && u.Username.ToLower() == cleanUsername), 
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"A user with email '{cleanEmail}' or username '{cleanUsername}' already exists.");
        }

        var defaultStation = request.WorkMode.Equals("REMOTE_WFH", StringComparison.OrdinalIgnoreCase)
            ? $"WFH-{cleanUsername.ToUpper()}"
            : (string.IsNullOrWhiteSpace(request.StationId) ? "ST-NEW" : request.StationId.Trim().ToUpper());

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = cleanEmail,
            Username = cleanUsername,
            FullName = request.FullName.Trim(),
            Role = string.IsNullOrWhiteSpace(request.Role) ? "EMPLOYEE" : request.Role.Trim().ToUpper(),
            WorkMode = string.IsNullOrWhiteSpace(request.WorkMode) ? "IN_OFFICE" : request.WorkMode.Trim().ToUpper(),
            StationId = defaultStation,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(string.IsNullOrWhiteSpace(request.Password) ? "PHF-Welcome@2026" : request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StaffMemberDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            WorkMode = user.WorkMode,
            StationId = user.StationId,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            ActiveTasksCount = 0,
            ShiftStatus = null
        };
    }

    public async Task<bool> ToggleActiveStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null) return false;

        user.IsActive = !user.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateWorkModeAsync(Guid userId, string workMode, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null) return false;

        user.WorkMode = workMode.Trim().ToUpper();
        if (user.WorkMode == "REMOTE_WFH" && (string.IsNullOrEmpty(user.StationId) || user.StationId.StartsWith("ST-")))
        {
            user.StationId = $"WFH-{(user.Username ?? user.FullName.Replace(" ", "")).ToUpper()}";
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<StaffMemberDto?> UpdateStaffAsync(Guid userId, UpdateStaffRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null) return null;

        var cleanEmail = request.Email.Trim().ToLower();
        var cleanUsername = string.IsNullOrWhiteSpace(request.Username)
            ? cleanEmail.Split('@')[0]
            : request.Username.Trim().ToLower();

        // Ensure email and username uniqueness against other existing accounts
        var conflict = await _dbContext.Users.AnyAsync(u =>
            u.Id != userId && (u.Email.ToLower() == cleanEmail || (u.Username != null && u.Username.ToLower() == cleanUsername)),
            cancellationToken);

        if (conflict)
        {
            throw new InvalidOperationException($"Another user with email '{cleanEmail}' or username '{cleanUsername}' already exists.");
        }

        user.FullName = request.FullName.Trim();
        user.Email = cleanEmail;
        user.Username = cleanUsername;
        user.Role = string.IsNullOrWhiteSpace(request.Role) ? "EMPLOYEE" : request.Role.Trim().ToUpper();
        user.WorkMode = string.IsNullOrWhiteSpace(request.WorkMode) ? "IN_OFFICE" : request.WorkMode.Trim().ToUpper();

        if (!string.IsNullOrWhiteSpace(request.StationId))
        {
            user.StationId = request.StationId.Trim().ToUpper();
        }
        else if (user.WorkMode == "REMOTE_WFH")
        {
            user.StationId = $"WFH-{cleanUsername.ToUpper()}";
        }

        user.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword.Trim());
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetStaffByIdAsync(userId, cancellationToken);
    }
}

