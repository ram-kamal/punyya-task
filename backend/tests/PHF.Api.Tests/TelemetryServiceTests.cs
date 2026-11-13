using Microsoft.EntityFrameworkCore;
using Moq;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Core.Enums;
using PHF.Core.Interfaces;
using PHF.Infrastructure.Data;
using PHF.Infrastructure.Services;
using Xunit;

namespace PHF.Api.Tests;

public class TelemetryServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Theory]
    [InlineData("Antigravity", null, true)]
    [InlineData("Antigravity IDE", null, true)]
    [InlineData("antigravity-x64", null, true)]
    [InlineData("code", null, true)]
    [InlineData("Visual Studio Code", null, true)]
    [InlineData("devenv", null, true)]
    [InlineData("Visual Studio", null, true)]
    [InlineData("studio64", null, true)]
    [InlineData("android studio", null, true)]
    [InlineData("emulator", null, true)]
    [InlineData("adb", null, true)]
    [InlineData("idea64", null, true)]
    [InlineData("pycharm64", null, true)]
    [InlineData("rider64", null, true)]
    [InlineData("cursor", null, true)]
    [InlineData("windsurf", null, true)]
    [InlineData("powershell", null, true)]
    [InlineData("terminal", null, true)]
    [InlineData("dotnet", null, true)]
    [InlineData("gradle", null, true)]
    [InlineData("Unknown", "MainWindow.xaml.cs - Visual Studio", true)]
    [InlineData("Unknown", "app/build.gradle - Android Studio", true)]
    [InlineData("Unknown", "Antigravity - Project Workspace", true)]
    [InlineData("Google Chrome", "Google Search", false)]
    [InlineData("Calculator", "Calculator", false)]
    [InlineData("Windows Desktop", "Windows Desktop", false)]
    public void IsDevelopmentEnvironment_ClassifiesCorrectly(string processName, string? windowTitle, bool expected)
    {
        var result = TelemetryService.IsDevelopmentEnvironment(processName, windowTitle);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task IngestBatchAsync_WhenProcessIsIde_ForcesIsIdleToFalse()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockNotifier = new Mock<IRealtimeNotifier>();
        var service = new TelemetryService(context, mockNotifier.Object);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "dev@punnyaheart.org",
            FullName = "Dev User",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-DEV",
            PasswordHash = "hash"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var batch = new TelemetryBatchRequest
        {
            StationId = "ST-DEV",
            UserId = user.Id,
            ActivityLogs = new List<AppActivityLogDto>
            {
                new AppActivityLogDto
                {
                    ProcessName = "Antigravity",
                    WindowTitle = "PHF.Client.Desktop - Antigravity",
                    DurationSeconds = 300,
                    IsIdle = true, // Client sent isIdle = true due to reading/waiting
                    LoggedAt = DateTime.UtcNow
                },
                new AppActivityLogDto
                {
                    ProcessName = "Google Chrome",
                    WindowTitle = "Random Article",
                    DurationSeconds = 300,
                    IsIdle = true,
                    LoggedAt = DateTime.UtcNow
                }
            }
        };

        // Act
        var count = await service.IngestBatchAsync(batch);

        // Assert
        Assert.Equal(2, count);
        var logs = await context.AppActivityLogs.OrderBy(l => l.ProcessName).ToListAsync();
        var ideLog = logs.First(l => l.ProcessName == "Antigravity");
        var chromeLog = logs.First(l => l.ProcessName == "Google Chrome");

        Assert.False(ideLog.IsIdle, "IDE log must be converted to is_idle = false (active)");
        Assert.True(chromeLog.IsIdle, "Non-IDE log should preserve is_idle = true");
    }

    [Fact]
    public async Task ProcessHeartbeatAsync_WhenInIde_TreatsIdleStateAsActive()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockNotifier = new Mock<IRealtimeNotifier>();
        var service = new TelemetryService(context, mockNotifier.Object);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "dev@punnyaheart.org",
            FullName = "Dev User",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-DEV",
            PasswordHash = "hash"
        };
        context.Users.Add(user);

        var activeShift = new Shift
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            StationId = "ST-DEV",
            ClockIn = DateTime.UtcNow.AddHours(-1),
            Status = ShiftStatus.ACTIVE.ToString(),
            TotalActiveSeconds = 1000,
            TotalIdleSeconds = 0
        };
        context.Shifts.Add(activeShift);
        await context.SaveChangesAsync();

        // Initial heartbeat
        await service.ProcessHeartbeatAsync(new WorkstationHeartbeatDto
        {
            StationId = "ST-DEV",
            UserId = user.Id,
            UserFullName = user.FullName,
            UserEmail = user.Email,
            ActiveProcess = "Antigravity",
            WindowTitle = "Coding",
            State = "IDLE" // Client idle tracker flagged idle
        });

        // Advance 30 seconds
        await Task.Delay(50); // tiny delay

        // Subsequent heartbeat reporting IDLE while focused on Antigravity
        await service.ProcessHeartbeatAsync(new WorkstationHeartbeatDto
        {
            StationId = "ST-DEV",
            UserId = user.Id,
            UserFullName = user.FullName,
            UserEmail = user.Email,
            ActiveProcess = "Antigravity",
            WindowTitle = "Coding",
            State = "IDLE"
        });

        // Assert
        var stations = await service.GetWorkstationsStatusAsync();
        var st = stations.First(s => s.UserId == user.Id);

        Assert.Equal("ACTIVE", st.State);
    }
}
