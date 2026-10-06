using System.Text;
using Microsoft.EntityFrameworkCore;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Core.Enums;
using PHF.Infrastructure.Data;
using PHF.Infrastructure.Services;
using Xunit;

namespace PHF.Api.Tests;

public class ReportServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetAttendanceReportAsync_ComputesWorkingDaysAttendanceRateAndLeaves()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var emp = new User
        {
            Id = Guid.NewGuid(),
            Email = "attendance@punnya.org",
            FullName = "Attendance Tester",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-ATT"
        };
        context.Users.Add(emp);

        var oct1 = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        var oct2 = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);

        // 2 shifts worked in October 2026
        context.Shifts.AddRange(
            new Shift
            {
                Id = Guid.NewGuid(),
                UserId = emp.Id,
                StationId = "ST-ATT",
                ClockIn = oct1,
                ClockOut = oct1.AddHours(8),
                TotalActiveSeconds = 25200,
                TotalIdleSeconds = 3600,
                Status = ShiftStatus.COMPLETED.ToString()
            },
            new Shift
            {
                Id = Guid.NewGuid(),
                UserId = emp.Id,
                StationId = "ST-ATT",
                ClockIn = oct2,
                ClockOut = oct2.AddHours(8),
                TotalActiveSeconds = 25200,
                TotalIdleSeconds = 3600,
                Status = ShiftStatus.COMPLETED.ToString()
            }
        );

        // 1 approved leave day on Oct 5
        var oct5 = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        context.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(),
            UserId = emp.Id,
            LeaveType = LeaveType.CASUAL.ToString(),
            StartDate = oct5,
            EndDate = oct5,
            TotalDays = 1,
            Reason = "Personal work",
            Status = LeaveStatus.APPROVED.ToString()
        });

        await context.SaveChangesAsync();

        var service = new ReportService(context);

        // Act - Request October 2026 Attendance Report
        var report = await service.GetAttendanceReportAsync(2026, 10);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(2026, report.Year);
        Assert.Equal(10, report.Month);
        Assert.NotEmpty(report.StaffSummaries);

        var staffRow = report.StaffSummaries.First(s => s.UserId == emp.Id);
        Assert.Equal("Attendance Tester", staffRow.EmployeeName);
        Assert.Equal(2, staffRow.DaysPresent);
        Assert.Equal(1, staffRow.ApprovedLeaveDays);
        Assert.Equal(16.0, staffRow.TotalHours);
        Assert.True(staffRow.AttendanceRatePercentage > 0);
    }

    [Fact]
    public async Task GetPerformanceReportAsync_CalculatesTaskVelocityAndEfficiency()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var emp = new User
        {
            Id = Guid.NewGuid(),
            Email = "performer@punnya.org",
            FullName = "Top Performer",
            Role = UserRole.EMPLOYEE.ToString(),
            StationId = "ST-PERF"
        };
        context.Users.Add(emp);

        var oct1 = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

        // 3 tasks assigned: 2 completed, 1 in progress
        context.Tasks.AddRange(
            new TaskItem
            {
                Id = Guid.NewGuid(),
                TaskCode = "TSK-001",
                Title = "Task 1",
                AssignedTo = emp.Id,
                Status = TaskItemStatus.COMPLETED.ToString(),
                CreatedAt = oct1,
                CompletedAt = oct1.AddHours(2),
                DeliverableRequired = true,
                DeliverableFileName = "report.pdf"
            },
            new TaskItem
            {
                Id = Guid.NewGuid(),
                TaskCode = "TSK-002",
                Title = "Task 2",
                AssignedTo = emp.Id,
                Status = TaskItemStatus.COMPLETED.ToString(),
                CreatedAt = oct1,
                CompletedAt = oct1.AddHours(4),
                DeliverableRequired = false
            },
            new TaskItem
            {
                Id = Guid.NewGuid(),
                TaskCode = "TSK-003",
                Title = "Task 3",
                AssignedTo = emp.Id,
                Status = TaskItemStatus.IN_PROGRESS.ToString(),
                CreatedAt = oct1
            }
        );

        // Shift worked
        context.Shifts.Add(new Shift
        {
            Id = Guid.NewGuid(),
            UserId = emp.Id,
            StationId = "ST-PERF",
            ClockIn = oct1,
            ClockOut = oct1.AddHours(8),
            TotalActiveSeconds = 25200,
            TotalIdleSeconds = 3600,
            Status = ShiftStatus.COMPLETED.ToString()
        });

        await context.SaveChangesAsync();

        var service = new ReportService(context);

        // Act
        var report = await service.GetPerformanceReportAsync(oct1.Date, oct1.Date.AddDays(1), "DAY");

        // Assert
        Assert.NotNull(report);
        Assert.NotEmpty(report.StaffPerformance);

        var row = report.StaffPerformance.First(p => p.UserId == emp.Id);
        Assert.Equal(3, row.TasksAssigned);
        Assert.Equal(2, row.TasksCompleted);
        Assert.Equal(1, row.TasksInProgress);
        Assert.Equal(66.7, row.TaskCompletionRate);
        Assert.Equal(8.0, row.TotalHoursWorked);
        Assert.Equal(1, row.DeliverablesSubmitted);
        Assert.Equal(100.0, row.DeliverableComplianceRate);
        Assert.NotEmpty(row.PerformanceRating);
    }

    [Fact]
    public async Task GetLeaveReportAsync_AggregatesLeaveDaysByTypesAndAudits()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var emp = new User
        {
            Id = Guid.NewGuid(),
            Email = "leaveuser@punnya.org",
            FullName = "Leave User",
            Role = UserRole.EMPLOYEE.ToString()
        };
        context.Users.Add(emp);

        var oct1 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        context.LeaveRequests.AddRange(
            new LeaveRequest
            {
                Id = Guid.NewGuid(),
                UserId = emp.Id,
                LeaveType = LeaveType.CASUAL.ToString(),
                StartDate = oct1,
                EndDate = oct1.AddDays(1),
                TotalDays = 2,
                Reason = "Family event",
                Status = LeaveStatus.APPROVED.ToString(),
                CreatedAt = oct1.AddDays(-2)
            },
            new LeaveRequest
            {
                Id = Guid.NewGuid(),
                UserId = emp.Id,
                LeaveType = LeaveType.SICK.ToString(),
                StartDate = oct1.AddDays(5),
                EndDate = oct1.AddDays(5),
                TotalDays = 1,
                Reason = "Fever",
                Status = LeaveStatus.PENDING.ToString(),
                CreatedAt = oct1.AddDays(4)
            }
        );

        await context.SaveChangesAsync();

        var service = new ReportService(context);

        // Act
        var report = await service.GetLeaveReportAsync(oct1, oct1.AddDays(10), "CUSTOM");

        // Assert
        Assert.NotNull(report);
        Assert.Equal(2, report.TotalApplications);
        Assert.Equal(2, report.TotalApprovedDays);
        Assert.Equal(1, report.TotalPendingRequests);

        var summary = report.StaffSummaries.First(s => s.UserId == emp.Id);
        Assert.Equal(2, summary.TotalRequests);
        Assert.Equal(2, summary.TotalApprovedDays);
        Assert.Equal(2, summary.CasualLeaveDays);
        Assert.Equal(1, summary.PendingRequests);

        Assert.Equal(2, report.DetailedRecords.Count);
    }

    [Fact]
    public async Task ExportReportsCsv_ProducesValidBOMAndFormattedHeaders()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var emp = new User { Id = Guid.NewGuid(), Email = "csv@punnya.org", FullName = "Csv Test", Role = UserRole.EMPLOYEE.ToString(), StationId = "ST-CSV" };
        context.Users.Add(emp);
        await context.SaveChangesAsync();

        var service = new ReportService(context);

        // Act
        var attCsv = await service.ExportAttendanceReportCsvAsync(2026, 10);
        var perfCsv = await service.ExportPerformanceReportCsvAsync();
        var leaveCsv = await service.ExportLeaveReportCsvAsync();

        // Assert
        var attText = Encoding.UTF8.GetString(attCsv);
        var perfText = Encoding.UTF8.GetString(perfCsv);
        var leaveText = Encoding.UTF8.GetString(leaveCsv);

        Assert.Contains("MONTH-END ATTENDANCE REPORT", attText);
        Assert.Contains("STAFF PERFORMANCE & VELOCITY REPORT", perfText);
        Assert.Contains("MONTH-END / PERIOD LEAVE SUMMARY", leaveText);
    }
}
