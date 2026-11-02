using System.Text;
using Microsoft.EntityFrameworkCore;
using Moq;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Core.Enums;
using PHF.Core.Interfaces;
using PHF.Core.Utils;
using PHF.Infrastructure.Data;
using PHF.Infrastructure.Services;
using Xunit;

namespace PHF.Api.Tests;

public class CsvExportFilterTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Theory]
    [InlineData("Ram Kamal", "rk@saaol.com", "rk", "ADMIN", true)]
    [InlineData("Dr. Ram Kamal", "ramkamal@saaol.com", "ramkamal", "ADMIN", true)]
    [InlineData("Ramkamal", "ram.kamal@punnya.org", "ram-kamal", "MANAGER", true)]
    [InlineData("Supervisor", "ram_kamal@saaol.com", "supervisor", "EMPLOYEE", true)]
    [InlineData("Auditor One", "auditor@saaol.com", "auditor", "VIEWER", true)]
    [InlineData("Monitor User", "monitor@saaol.com", "monitor", "MONITOR", true)]
    [InlineData("Observer", "observer@saaol.com", "obs", "viewer", true)]
    [InlineData("Observer", "observer@saaol.com", "obs", "monitor", true)]
    [InlineData("Aarav Sharma", "aarav@punnya.org", "aarav", "EMPLOYEE", false)]
    [InlineData("Dr. Jane Smith", "jane@punnya.org", "jsmith", "MANAGER", false)]
    [InlineData("Admin System", "admin@punnya.org", "sysadmin", "ADMIN", false)]
    public void ShouldExcludeFromCsv_CorrectlyFiltersRamkamalAndViewers(
        string fullName, string email, string username, string role, bool expectedResult)
    {
        var result = CsvExportFilter.ShouldExcludeFromCsv(fullName, email, username, role);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExportTimesheetCsvAsync_ExcludesRamkamalAndViewers()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockTelemetry = new Mock<ITelemetryService>();
        mockTelemetry.Setup(t => t.GetWorkstationsStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkstationStatusDto>());

        var mockShiftService = new Mock<IShiftService>();

        var emp = new User
        {
            Id = Guid.NewGuid(),
            Email = "emp@saaol.com",
            FullName = "Regular Employee",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-01"
        };
        var ramkamal = new User
        {
            Id = Guid.NewGuid(),
            Email = "ramkamal@saaol.com",
            FullName = "Dr. Ram Kamal",
            Role = UserRole.ADMIN.ToString(),
            StationId = "ST-DIR"
        };
        var viewer = new User
        {
            Id = Guid.NewGuid(),
            Email = "viewer@saaol.com",
            FullName = "Viewer Auditor",
            Role = "MONITOR",
            StationId = "ST-VIEW"
        };

        context.Users.AddRange(emp, ramkamal, viewer);

        var now = DateTime.UtcNow.Date.AddHours(9);
        context.Shifts.AddRange(
            new Shift { Id = Guid.NewGuid(), UserId = emp.Id, StationId = "ST-01", ClockIn = now, ClockOut = now.AddHours(8), Status = "COMPLETED" },
            new Shift { Id = Guid.NewGuid(), UserId = ramkamal.Id, StationId = "ST-DIR", ClockIn = now, ClockOut = now.AddHours(8), Status = "COMPLETED" },
            new Shift { Id = Guid.NewGuid(), UserId = viewer.Id, StationId = "ST-VIEW", ClockIn = now, ClockOut = now.AddHours(8), Status = "COMPLETED" }
        );
        await context.SaveChangesAsync();

        var service = new TimesheetService(context, mockTelemetry.Object, mockShiftService.Object);

        // Act
        var csvBytes = await service.ExportTimesheetCsvAsync(period: "TODAY");
        var csvText = Encoding.UTF8.GetString(csvBytes);

        // Assert
        Assert.Contains("Regular Employee", csvText);
        Assert.Contains("emp@saaol.com", csvText);

        Assert.DoesNotContain("Ram Kamal", csvText);
        Assert.DoesNotContain("ramkamal@saaol.com", csvText);
        Assert.DoesNotContain("Viewer Auditor", csvText);
        Assert.DoesNotContain("viewer@saaol.com", csvText);
    }

    [Fact]
    public async Task ExportReportCsv_ExcludesRamkamalAndViewersAcrossAllReports()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockShiftService = new Mock<IShiftService>();

        var emp = new User
        {
            Id = Guid.NewGuid(),
            Email = "emp@saaol.com",
            FullName = "Regular Staff",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-EMP"
        };
        var ramkamal = new User
        {
            Id = Guid.NewGuid(),
            Email = "ramkamal@saaol.com",
            FullName = "Dr. Ram Kamal",
            Role = UserRole.ADMIN.ToString(),
            StationId = "ST-ADMIN"
        };
        var viewer = new User
        {
            Id = Guid.NewGuid(),
            Email = "auditor@saaol.com",
            FullName = "Site Auditor",
            Role = "VIEWER",
            StationId = "ST-VIEW"
        };

        context.Users.AddRange(emp, ramkamal, viewer);

        var oct1 = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        context.Shifts.AddRange(
            new Shift { Id = Guid.NewGuid(), UserId = emp.Id, StationId = "ST-EMP", ClockIn = oct1, ClockOut = oct1.AddHours(8), Status = "COMPLETED" },
            new Shift { Id = Guid.NewGuid(), UserId = ramkamal.Id, StationId = "ST-ADMIN", ClockIn = oct1, ClockOut = oct1.AddHours(8), Status = "COMPLETED" },
            new Shift { Id = Guid.NewGuid(), UserId = viewer.Id, StationId = "ST-VIEW", ClockIn = oct1, ClockOut = oct1.AddHours(8), Status = "COMPLETED" }
        );

        context.LeaveRequests.AddRange(
            new LeaveRequest { Id = Guid.NewGuid(), UserId = emp.Id, LeaveType = "CASUAL", StartDate = oct1, EndDate = oct1.AddDays(1), TotalDays = 1, Status = "APPROVED", Reason = "Doctor" },
            new LeaveRequest { Id = Guid.NewGuid(), UserId = ramkamal.Id, LeaveType = "ANNUAL", StartDate = oct1, EndDate = oct1.AddDays(2), TotalDays = 2, Status = "APPROVED", Reason = "Conference" },
            new LeaveRequest { Id = Guid.NewGuid(), UserId = viewer.Id, LeaveType = "SICK", StartDate = oct1, EndDate = oct1.AddDays(1), TotalDays = 1, Status = "APPROVED", Reason = "Rest" }
        );

        await context.SaveChangesAsync();

        var service = new ReportService(context, mockShiftService.Object);

        // Act
        var attCsv = Encoding.UTF8.GetString(await service.ExportAttendanceReportCsvAsync(2026, 10));
        var perfCsv = Encoding.UTF8.GetString(await service.ExportPerformanceReportCsvAsync(startDate: oct1));
        var leaveCsvSummary = Encoding.UTF8.GetString(await service.ExportLeaveReportCsvAsync(startDate: oct1, viewType: "summary"));
        var leaveCsvDetailed = Encoding.UTF8.GetString(await service.ExportLeaveReportCsvAsync(startDate: oct1, viewType: "detailed"));

        // Assert Attendance
        Assert.Contains("Regular Staff", attCsv);
        Assert.DoesNotContain("Ram Kamal", attCsv);
        Assert.DoesNotContain("Site Auditor", attCsv);

        // Assert Performance
        Assert.Contains("Regular Staff", perfCsv);
        Assert.DoesNotContain("Ram Kamal", perfCsv);
        Assert.DoesNotContain("Site Auditor", perfCsv);

        // Assert Leaves
        Assert.Contains("Regular Staff", leaveCsvSummary);
        Assert.DoesNotContain("Ram Kamal", leaveCsvSummary);
        Assert.DoesNotContain("Site Auditor", leaveCsvSummary);

        Assert.Contains("Regular Staff", leaveCsvDetailed);
        Assert.DoesNotContain("Ram Kamal", leaveCsvDetailed);
        Assert.DoesNotContain("Site Auditor", leaveCsvDetailed);
    }
}
