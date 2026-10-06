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

    [Fact]
    public async Task GetSummaryAsync_YesterdayPeriod_CalculatesCompletedShiftEvenWithZeroActiveSeconds()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockTelemetry = new Mock<ITelemetryService>();

        var testUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "dhvaj@punnyaheart.org",
            FullName = "Dhvaj Dhyani",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-103",
            PasswordHash = "hash"
        };
        context.Users.Add(testUser);

        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var clockIn = yesterday.AddHours(4).AddMinutes(1); // 04:01 UTC
        var clockOut = yesterday.AddHours(10).AddMinutes(23); // 10:23 UTC (6h 22m)

        // Legacy shift that had TotalActiveSeconds = 0 upon clock-out
        context.Shifts.Add(new Shift
        {
            Id = Guid.NewGuid(),
            UserId = testUser.Id,
            StationId = "ST-103",
            ClockIn = clockIn,
            ClockOut = clockOut,
            TotalActiveSeconds = 0,
            TotalIdleSeconds = 0,
            Status = ShiftStatus.COMPLETED.ToString()
        });
        await context.SaveChangesAsync();

        mockTelemetry.Setup(t => t.GetWorkstationsStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkstationStatusDto>());

        var timesheetService = new TimesheetService(context, mockTelemetry.Object);

        // Act
        var summary = await timesheetService.GetSummaryAsync(null, null, "YESTERDAY");

        // Assert
        Assert.NotNull(summary);
        Assert.Equal("YESTERDAY", summary.Period);
        Assert.NotEmpty(summary.Rows);

        var row = summary.Rows.First(r => r.Email == "dhvaj@punnyaheart.org");
        Assert.Equal(1, row.DaysWorked);
        Assert.True(row.TotalHours >= 6.3 && row.TotalHours <= 6.4);
        Assert.True(row.ActiveHours >= 6.3 && row.ActiveHours <= 6.4);
        Assert.Equal("6h 22m", row.TotalTimeFormatted);
        Assert.Equal(100.0, row.EfficiencyPercentage);
    }

    [Fact]
    public async Task GetSummaryAsync_IncludesManagersOrAdminsWithShiftsInTimesheets()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockTelemetry = new Mock<ITelemetryService>();

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@punnyaheart.org",
            FullName = "Admin User",
            Role = UserRole.ADMIN.ToString(),
            StationId = "ST-ADMIN",
            PasswordHash = "hash"
        };
        context.Users.Add(adminUser);

        var today = DateTime.UtcNow.Date;
        context.Shifts.Add(new Shift
        {
            Id = Guid.NewGuid(),
            UserId = adminUser.Id,
            StationId = "ST-ADMIN",
            ClockIn = today.AddHours(2),
            ClockOut = today.AddHours(5),
            TotalActiveSeconds = 10800,
            TotalIdleSeconds = 0,
            Status = ShiftStatus.COMPLETED.ToString()
        });
        await context.SaveChangesAsync();

        mockTelemetry.Setup(t => t.GetWorkstationsStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkstationStatusDto>());

        var timesheetService = new TimesheetService(context, mockTelemetry.Object);

        // Act
        var summary = await timesheetService.GetSummaryAsync(null, null, "TODAY");

        // Assert
        Assert.NotNull(summary);
        Assert.Contains(summary.Rows, r => r.Email == "admin@punnyaheart.org");
        var adminRow = summary.Rows.First(r => r.Email == "admin@punnyaheart.org");
    }

    [Fact]
    public async Task GetSummaryAsync_WithAppActivityLogs_AccuratelyAggregatesHoursWithoutOverfetching()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockTelemetry = new Mock<ITelemetryService>();

        var testUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "emp@punnyaheart.org",
            FullName = "Test Employee",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-103",
            PasswordHash = "hash"
        };
        context.Users.Add(testUser);

        var today = DateTime.UtcNow.Date;
        context.Shifts.Add(new Shift
        {
            Id = Guid.NewGuid(),
            UserId = testUser.Id,
            StationId = "ST-103",
            ClockIn = today.AddHours(9),
            ClockOut = today.AddHours(17),
            TotalActiveSeconds = 21600,
            TotalIdleSeconds = 7200,
            Status = ShiftStatus.COMPLETED.ToString()
        });

        // Add activity logs: 6h active (21600s), 2h idle (7200s)
        context.AppActivityLogs.AddRange(
            new AppActivityLog
            {
                Id = Guid.NewGuid(),
                UserId = testUser.Id,
                LoggedAt = today.AddHours(10),
                ProcessName = "Code.exe",
                WindowTitle = "VS Code",
                DurationSeconds = 21600,
                IsIdle = false
            },
            new AppActivityLog
            {
                Id = Guid.NewGuid(),
                UserId = testUser.Id,
                LoggedAt = today.AddHours(14),
                ProcessName = "IdleTracker",
                WindowTitle = "System Idle",
                DurationSeconds = 7200,
                IsIdle = true
            }
        );

        await context.SaveChangesAsync();

        mockTelemetry.Setup(t => t.GetWorkstationsStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkstationStatusDto>());

        var timesheetService = new TimesheetService(context, mockTelemetry.Object);

        // Act
        var summary = await timesheetService.GetSummaryAsync(today, null, "TODAY");

        // Assert
        Assert.NotNull(summary);
        Assert.NotEmpty(summary.Rows);
        var row = summary.Rows.First(r => r.Email == testUser.Email);
        Assert.Equal(8.0, row.TotalHours);
        Assert.Equal(6.0, row.ActiveHours);
        Assert.Equal(2.0, row.IdleHours);
        Assert.Equal(75.0, row.EfficiencyPercentage);
    }

    [Fact]
    public async Task GetSummaryAsync_EmployeeScoping_OnlyReturnsTargetEmployeeData()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockTelemetry = new Mock<ITelemetryService>();

        var emp1 = new User { Id = Guid.NewGuid(), Email = "emp1@punnya.org", FullName = "Emp One", Role = UserRole.EMPLOYEE.ToString() };
        var emp2 = new User { Id = Guid.NewGuid(), Email = "emp2@punnya.org", FullName = "Emp Two", Role = UserRole.EMPLOYEE.ToString() };
        context.Users.AddRange(emp1, emp2);

        var today = DateTime.UtcNow.Date;
        context.Shifts.AddRange(
            new Shift { Id = Guid.NewGuid(), UserId = emp1.Id, StationId = "ST-1", ClockIn = today.AddHours(9), ClockOut = today.AddHours(17), Status = ShiftStatus.COMPLETED.ToString() },
            new Shift { Id = Guid.NewGuid(), UserId = emp2.Id, StationId = "ST-2", ClockIn = today.AddHours(10), ClockOut = today.AddHours(18), Status = ShiftStatus.COMPLETED.ToString() }
        );
        await context.SaveChangesAsync();

        mockTelemetry.Setup(t => t.GetWorkstationsStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkstationStatusDto>());

        var timesheetService = new TimesheetService(context, mockTelemetry.Object);

        // Act - Request scoped specifically to emp1
        var summary = await timesheetService.GetSummaryAsync(today, null, "TODAY", emp1.Id);

        // Assert
        Assert.NotNull(summary);
        Assert.Single(summary.Rows);
        Assert.Equal("emp1@punnya.org", summary.Rows[0].Email);
        Assert.DoesNotContain(summary.Rows, r => r.Email == "emp2@punnya.org");
    }

    [Fact]
    public async Task GetSummaryAsync_ShiftCrossingMidnight_AccuratelyBindsHoursToRespectiveDays()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockTelemetry = new Mock<ITelemetryService>();

        var emp = new User { Id = Guid.NewGuid(), Email = "nightshift@punnya.org", FullName = "Night Owl", Role = UserRole.EMPLOYEE.ToString() };
        context.Users.Add(emp);

        // Shift starts at 22:00 on Day 1 and ends at 06:00 on Day 2 (8 hours total: 2 hours on Day 1, 6 hours on Day 2)
        var day1 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var day2 = day1.AddDays(1);

        context.Shifts.Add(new Shift
        {
            Id = Guid.NewGuid(),
            UserId = emp.Id,
            StationId = "ST-NIGHT",
            ClockIn = day1.AddHours(22),
            ClockOut = day2.AddHours(6),
            TotalActiveSeconds = 28800, // 8 hours active
            TotalIdleSeconds = 0,
            Status = ShiftStatus.COMPLETED.ToString()
        });
        await context.SaveChangesAsync();

        mockTelemetry.Setup(t => t.GetWorkstationsStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkstationStatusDto>());

        var timesheetService = new TimesheetService(context, mockTelemetry.Object);

        // Act: Request Day 1
        var summaryDay1 = await timesheetService.GetSummaryAsync(day1, null, "DAY");
        // Act: Request Day 2
        var summaryDay2 = await timesheetService.GetSummaryAsync(day2, null, "DAY");

        // Assert Day 1 has exactly 2 hours (22:00 to 00:00)
        Assert.NotEmpty(summaryDay1.DailyBreakdown);
        var day1Row = summaryDay1.DailyBreakdown.First(r => r.Email == emp.Email && r.Date == day1.ToString("yyyy-MM-dd"));
        Assert.Equal(2.0, day1Row.TotalHours);

        // Assert Day 2 has exactly 6 hours (00:00 to 06:00)
        Assert.NotEmpty(summaryDay2.DailyBreakdown);
        var day2Row = summaryDay2.DailyBreakdown.First(r => r.Email == emp.Email && r.Date == day2.ToString("yyyy-MM-dd"));
        Assert.Equal(6.0, day2Row.TotalHours);
    }
}
