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

    [Fact]
    public async Task ClockInAndOutMultipleTimesWithinDay_CombinesOldAndNewTime()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockNotifier = new Mock<IRealtimeNotifier>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "multishift@punnyaheart.org",
            FullName = "Multi Shift Employee",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-MULTI",
            PasswordHash = "hash"
        };
        context.Users.Add(user);

        var today = DateTime.UtcNow.Date;

        // Shift 1: Completed earlier today (4 hours: 09:00 to 13:00)
        var shift1 = new Shift
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            StationId = "ST-MULTI",
            ClockIn = today.AddHours(9),
            ClockOut = today.AddHours(13),
            TotalActiveSeconds = 14400,
            TotalIdleSeconds = 0,
            Status = ShiftStatus.COMPLETED.ToString()
        };
        context.Shifts.Add(shift1);
        await context.SaveChangesAsync();

        var shiftService = new ShiftService(context, mockNotifier.Object);

        // Act 1 - Employee clocks in again for Shift 2
        var clockInResult = await shiftService.ClockInAsync(user.Id, "ST-MULTI");

        // Assert 1: Prior seconds must reflect Shift 1 (14400s / 4.0h)
        Assert.NotNull(clockInResult);
        Assert.Equal(ShiftStatus.ACTIVE.ToString(), clockInResult.Status);
        Assert.Equal(14400, clockInResult.PriorSecondsToday);
        Assert.True(clockInResult.TotalHoursToday >= 3.99);
        Assert.Equal("4h 0m", clockInResult.TotalTimeFormatted);

        // Simulate Shift 2 running for 2 hours (started 2 hours ago, clocking out now)
        var shift2Entity = await context.Shifts.FindAsync(clockInResult.Id);
        Assert.NotNull(shift2Entity);
        shift2Entity.ClockIn = DateTime.UtcNow.AddHours(-2);
        await context.SaveChangesAsync();

        // Act 2 - Employee clocks out of Shift 2
        var clockOutResult = await shiftService.ClockOutAsync(user.Id, "ST-MULTI");

        // Assert 2: Combined total work hours must equal old time (4h) + new time (2h) = 6h!
        Assert.NotNull(clockOutResult);
        Assert.Equal(ShiftStatus.COMPLETED.ToString(), clockOutResult.Status);
        Assert.Equal(14400, clockOutResult.PriorSecondsToday);
        // Total should be approx 6 hours (21600 seconds)
        Assert.True(clockOutResult.TotalHoursToday >= 5.98 && clockOutResult.TotalHoursToday <= 6.02, 
            $"Expected TotalHoursToday to be approx 6.0, but got {clockOutResult.TotalHoursToday}");
        Assert.True(clockOutResult.TotalSecondsToday >= 21590 && clockOutResult.TotalSecondsToday <= 21610);
        Assert.Equal("6h 0m", clockOutResult.TotalTimeFormatted);

        // Act 3 - Query GetCurrentShiftAsync while clocked out
        var currentShift = await shiftService.GetCurrentShiftAsync(user.Id);
        Assert.NotNull(currentShift);
        Assert.Equal(ShiftStatus.COMPLETED.ToString(), currentShift.Status);
        Assert.True(currentShift.TotalHoursToday >= 5.98 && currentShift.TotalHoursToday <= 6.02);
        Assert.Equal("6h 0m", currentShift.TotalTimeFormatted);
    }
}
