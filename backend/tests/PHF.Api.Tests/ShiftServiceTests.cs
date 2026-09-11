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

public class ShiftServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ClockOutAsync_WhenActivityLogsEmpty_PreservesElapsedDurationAsActiveTime()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockNotifier = new Mock<IRealtimeNotifier>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "emp1@punnyaheart.org",
            FullName = "Test Employee",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-01",
            PasswordHash = "hash"
        };
        context.Users.Add(user);

        // Active shift started 2 hours ago
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            StationId = "ST-01",
            ClockIn = DateTime.UtcNow.AddHours(-2),
            Status = ShiftStatus.ACTIVE.ToString(),
            TotalActiveSeconds = 0,
            TotalIdleSeconds = 0
        };
        context.Shifts.Add(shift);
        await context.SaveChangesAsync();

        var shiftService = new ShiftService(context, mockNotifier.Object);

        // Act - clock out
        var result = await shiftService.ClockOutAsync(user.Id, "ST-01");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ShiftStatus.COMPLETED.ToString(), result.Status);
        // Elapsed time is approx 2 hours (7200 seconds)
        Assert.True(result.TotalActiveSeconds >= 7190 && result.TotalActiveSeconds <= 7210);
        Assert.True(result.TotalHoursToday >= 1.99 && result.TotalHoursToday <= 2.01);
        Assert.NotEqual("0h 0m", result.ActiveTimeFormatted);
        Assert.NotEqual("0h 0m", result.TotalTimeFormatted);

        // Verify database entity was updated
        var updatedShift = await context.Shifts.FindAsync(shift.Id);
        Assert.NotNull(updatedShift);
        Assert.True(updatedShift.TotalActiveSeconds >= 7190);
        Assert.Equal(ShiftStatus.COMPLETED.ToString(), updatedShift.Status);
    }

    [Fact]
    public async Task GetCurrentShiftAsync_AfterClockOut_ReturnsAccurateCompletedShiftHours()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockNotifier = new Mock<IRealtimeNotifier>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "emp2@punnyaheart.org",
            FullName = "Employee Two",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-02",
            PasswordHash = "hash"
        };
        context.Users.Add(user);

        // Completed shift today of 3 hours
        var clockIn = DateTime.UtcNow.Date.AddHours(9);
        var clockOut = clockIn.AddHours(3);
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            StationId = "ST-02",
            ClockIn = clockIn,
            ClockOut = clockOut,
            TotalActiveSeconds = 10800,
            TotalIdleSeconds = 0,
            Status = ShiftStatus.COMPLETED.ToString()
        };
        context.Shifts.Add(shift);
        await context.SaveChangesAsync();

        var shiftService = new ShiftService(context, mockNotifier.Object);

        // Act
        var result = await shiftService.GetCurrentShiftAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ShiftStatus.COMPLETED.ToString(), result.Status);
        Assert.Equal(3.0, result.TotalHoursToday);
        Assert.Equal("3h 0m", result.ActiveTimeFormatted);
        Assert.Equal("3h 0m", result.TotalTimeFormatted);
    }
}
