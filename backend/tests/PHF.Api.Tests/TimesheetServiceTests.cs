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
        Assert.Contains("Date,Station,Employee Name,Email,Clock In,Clock Out,Total Hours,Active Hours,Idle Hours,Efficiency %,Tasks Completed", csvContent);
        Assert.Contains("ST-101", csvContent);
        Assert.Contains("Aarav Sharma", csvContent);
        Assert.Contains("st101@punnyaheart.org", csvContent);
    }
}
