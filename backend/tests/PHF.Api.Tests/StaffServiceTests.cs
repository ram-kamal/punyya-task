using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Infrastructure.Data;
using PHF.Infrastructure.Services;
using Xunit;

namespace PHF.Api.Tests;

public class StaffServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private IConfiguration CreateMockConfiguration()
    {
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Jwt:Secret"]).Returns("PHF_Super_Long_Secret_Key_For_Jwt_Authentication_Must_Be_256_Bits_Long!");
        mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("PHF.Api");
        mockConfig.Setup(c => c["Jwt:Audience"]).Returns("PHF.Clients");
        return mockConfig.Object;
    }

    [Fact]
    public async Task CreateStaffAsync_WithPersonalGmail_CreatesRemoteWfhStaff()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var staffService = new StaffService(context);

        var request = new CreateStaffRequest
        {
            FullName = "Dhyan Remote",
            Username = "dhyan_wfh",
            Email = "dhyan.personal@gmail.com",
            Role = "EMPLOYEE",
            WorkMode = "REMOTE_WFH",
            Password = "SecurePassword@123"
        };

        // Act
        var result = await staffService.CreateStaffAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dhyan.personal@gmail.com", result.Email);
        Assert.Equal("dhyan_wfh", result.Username);
        Assert.Equal("REMOTE_WFH", result.WorkMode);
        Assert.StartsWith("WFH-", result.StationId);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task LoginAsync_WithUsername_AuthenticatesSuccessfully()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var config = CreateMockConfiguration();
        var authService = new AuthService(context, config);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Aarav Sharma",
            Email = "aarav.personal@gmail.com",
            Username = "aarav_remote",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass@123"),
            Role = "EMPLOYEE",
            WorkMode = "REMOTE_WFH",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act - Log in using username instead of email
        var loginRequest = new LoginRequest
        {
            Email = "aarav_remote",
            Password = "TestPass@123",
            StationId = "WFH-AARAV-PC"
        };
        var result = await authService.LoginAsync(loginRequest);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Token);
        Assert.Equal("aarav_remote", result.User.Username);
        Assert.Equal("REMOTE_WFH", result.User.WorkMode);
        Assert.Equal("WFH-AARAV-PC", result.User.StationId);
    }

    [Fact]
    public async Task ToggleActiveStatusAsync_SuspendsAndReactivatesStaff()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var staffService = new StaffService(context);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Test Staff",
            Email = "test@gmail.com",
            Username = "test_user",
            PasswordHash = "hash",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act 1: Deactivate
        var deactivated = await staffService.ToggleActiveStatusAsync(user.Id);
        var userAfterDeactivate = await context.Users.FindAsync(user.Id);

        // Assert 1
        Assert.True(deactivated);
        Assert.False(userAfterDeactivate!.IsActive);

        // Act 2: Reactivate
        var reactivated = await staffService.ToggleActiveStatusAsync(user.Id);
        var userAfterReactivate = await context.Users.FindAsync(user.Id);

        // Assert 2
        Assert.True(reactivated);
        Assert.True(userAfterReactivate!.IsActive);
    }

    [Fact]
    public async Task UpdateWorkModeAsync_SwitchesModeAndUpdatesStation()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var staffService = new StaffService(context);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Priya Patel",
            Email = "priya@gmail.com",
            Username = "priya",
            PasswordHash = "hash",
            WorkMode = "IN_OFFICE",
            StationId = "ST-102",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act: Switch to WFH
        var updated = await staffService.UpdateWorkModeAsync(user.Id, "REMOTE_WFH");
        var userAfterUpdate = await context.Users.FindAsync(user.Id);

        // Assert
        Assert.True(updated);
        Assert.Equal("REMOTE_WFH", userAfterUpdate!.WorkMode);
        Assert.StartsWith("WFH-", userAfterUpdate.StationId);
    }

    [Fact]
    public async Task UpdateStaffAsync_ValidRequest_UpdatesAllFields()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var staffService = new StaffService(context);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Original Name",
            Email = "original@gmail.com",
            Username = "original_user",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass@123"),
            Role = "EMPLOYEE",
            WorkMode = "IN_OFFICE",
            StationId = "ST-OLD",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var updateRequest = new UpdateStaffRequest
        {
            FullName = "Updated Name",
            Username = "updated_user",
            Email = "updated@gmail.com",
            Role = "MANAGER",
            WorkMode = "REMOTE_WFH",
            StationId = "WFH-UPDATED-DESK",
            IsActive = true
        };

        // Act
        var result = await staffService.UpdateStaffAsync(user.Id, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.FullName);
        Assert.Equal("updated@gmail.com", result.Email);
        Assert.Equal("updated_user", result.Username);
        Assert.Equal("MANAGER", result.Role);
        Assert.Equal("REMOTE_WFH", result.WorkMode);
        Assert.Equal("WFH-UPDATED-DESK", result.StationId);
    }

    [Fact]
    public async Task UpdateStaffAsync_WithNewPassword_UpdatesPasswordHash()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var staffService = new StaffService(context);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Alice Smith",
            Email = "alice@gmail.com",
            Username = "alice",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldAlice@123"),
            Role = "EMPLOYEE",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var updateRequest = new UpdateStaffRequest
        {
            FullName = "Alice Smith",
            Username = "alice",
            Email = "alice@gmail.com",
            Role = "EMPLOYEE",
            WorkMode = "IN_OFFICE",
            IsActive = true,
            NewPassword = "NewSecretAlice@2026"
        };

        // Act
        await staffService.UpdateStaffAsync(user.Id, updateRequest);
        var updatedUser = await context.Users.FindAsync(user.Id);

        // Assert
        Assert.NotNull(updatedUser);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewSecretAlice@2026", updatedUser.PasswordHash));
    }

    [Fact]
    public async Task UpdateStaffAsync_DuplicateEmailOrUsername_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var staffService = new StaffService(context);

        var user1 = new User
        {
            Id = Guid.NewGuid(),
            FullName = "User One",
            Email = "user1@gmail.com",
            Username = "user1",
            PasswordHash = "hash1",
            IsActive = true
        };
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            FullName = "User Two",
            Email = "user2@gmail.com",
            Username = "user2",
            PasswordHash = "hash2",
            IsActive = true
        };
        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();

        // Act & Assert: try to change user2 email to user1's email
        var request = new UpdateStaffRequest
        {
            FullName = "User Two Edited",
            Email = "user1@gmail.com",
            Username = "user2",
            Role = "EMPLOYEE",
            WorkMode = "IN_OFFICE",
            IsActive = true
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            staffService.UpdateStaffAsync(user2.Id, request));
    }
}

