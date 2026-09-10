using System.Text;
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

public class TimesheetServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ExportTimesheetCsvAsync_ProducesValidCsvWithBOMAndHeaders()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockTelemetry = new Mock<ITelemetryService>();

        var testUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "st101@punnyaheart.org",
            FullName = "Aarav Sharma",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-101",
            PasswordHash = "hash"
        };
        context.Users.Add(testUser);

        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            UserId = testUser.Id,
            StationId = "ST-101",
            ClockIn = DateTime.UtcNow.Date.AddHours(9),
            ClockOut = DateTime.UtcNow.Date.AddHours(17),
            TotalActiveSeconds = 25200,
            TotalIdleSeconds = 3600,
            Status = ShiftStatus.COMPLETED.ToString()
        };
        context.Shifts.Add(shift);
        await context.SaveChangesAsync();

        mockTelemetry.Setup(t => t.GetWorkstationsStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkstationStatusDto>
            {
                new() { StationId = "ST-101", State = StationState.ACTIVE.ToString() }
            });

        var timesheetService = new TimesheetService(context, mockTelemetry.Object);

        // Act
        var csvBytes = await timesheetService.ExportTimesheetCsvAsync(DateTime.UtcNow.Date);
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert
        Assert.NotNull(csvBytes);
        Assert.True(csvBytes.Length > 0);
        Assert.Contains("Date,Station,Employee Name,Email,Clock In,Clock Out,Total Hours,Total Time (HH:MM),Active Hours,Active Time (HH:MM),Idle Hours,Idle Time (HH:MM),Efficiency %,Tasks Completed", csvContent);
        Assert.Contains("ST-101", csvContent);
        Assert.Contains("Aarav Sharma", csvContent);
        Assert.Contains("st101@punnyaheart.org", csvContent);
    }

    [Fact]
    public async Task GetSummaryAsync_MonthlyPeriod_ComputesAggregatesAndDailyBreakdown()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockTelemetry = new Mock<ITelemetryService>();

        var testUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "priya@punnyaheart.org",
            FullName = "Priya Patel",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-102",
            PasswordHash = "hash"
        };
        context.Users.Add(testUser);

        var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 9, 0, 0, DateTimeKind.Utc);
        var secondOfMonth = firstOfMonth.AddDays(1);

        context.Shifts.AddRange(
            new Shift
            {
                Id = Guid.NewGuid(),
                UserId = testUser.Id,
                StationId = "ST-102",
                ClockIn = firstOfMonth,
                ClockOut = firstOfMonth.AddHours(8),
                TotalActiveSeconds = 25200,
                TotalIdleSeconds = 3600,
                Status = ShiftStatus.COMPLETED.ToString()
            },
            new Shift
            {
                Id = Guid.NewGuid(),
                UserId = testUser.Id,
                StationId = "ST-102",
                ClockIn = secondOfMonth,
                ClockOut = secondOfMonth.AddHours(8),
                TotalActiveSeconds = 25200,
                TotalIdleSeconds = 3600,
                Status = ShiftStatus.COMPLETED.ToString()
            }
        );
        await context.SaveChangesAsync();

        mockTelemetry.Setup(t => t.GetWorkstationsStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkstationStatusDto>());

        var timesheetService = new TimesheetService(context, mockTelemetry.Object);

        // Act
        var summary = await timesheetService.GetSummaryAsync(firstOfMonth, null, "MONTH");

        // Assert
        Assert.NotNull(summary);
        Assert.Equal("MONTH", summary.Period);
        Assert.NotEmpty(summary.Rows);
        Assert.NotEmpty(summary.DailyBreakdown);

        var empRow = summary.Rows.First(r => r.Email == "priya@punnyaheart.org");
        Assert.Equal(2, empRow.DaysWorked);
        Assert.Equal(16.0, empRow.TotalHours);
        Assert.Equal(14.0, empRow.ActiveHours);
        Assert.Equal(2.0, empRow.IdleHours);
    }
}
