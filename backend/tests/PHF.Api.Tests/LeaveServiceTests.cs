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

public class LeaveServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ApplyLeaveAsync_WithValidDates_CreatesPendingLeaveRequest()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockNotifier = new Mock<IRealtimeNotifier>();
        var leaveService = new LeaveService(context, mockNotifier.Object);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Aarav Sharma",
            Email = "aarav@punnyaheart.org",
            StationId = "ST-101"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var request = new CreateLeaveRequestDto
        {
            LeaveType = "SICK",
            StartDate = DateTime.UtcNow.Date.AddDays(2),
            EndDate = DateTime.UtcNow.Date.AddDays(4),
            Reason = "Medical procedure and recovery"
        };

        // Act
        var result = await leaveService.ApplyLeaveAsync(request, user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PENDING", result.Status);
        Assert.Equal("SICK", result.LeaveType);
        Assert.Equal(3, result.TotalDays);
        Assert.Equal("Aarav Sharma", result.UserName);

        mockNotifier.Verify(n => n.BroadcastWorkstationEventAsync(It.Is<LiveWorkstationEventDto>(
            e => e.EventType == "LEAVE_SUBMITTED")), Times.Once);
    }

    [Fact]
    public async Task ApplyLeaveAsync_WhenEndDateEarlierThanStartDate_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockNotifier = new Mock<IRealtimeNotifier>();
        var leaveService = new LeaveService(context, mockNotifier.Object);

        var request = new CreateLeaveRequestDto
        {
            LeaveType = "CASUAL",
            StartDate = DateTime.UtcNow.Date.AddDays(5),
            EndDate = DateTime.UtcNow.Date.AddDays(2), // earlier than start!
            Reason = "Vacation"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            leaveService.ApplyLeaveAsync(request, Guid.NewGuid()));
    }

    [Fact]
    public async Task ReviewLeaveAsync_WhenApproved_SetsStatusApprovedAndAssignsReviewer()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockNotifier = new Mock<IRealtimeNotifier>();
        var leaveService = new LeaveService(context, mockNotifier.Object);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Priya Patel",
            Email = "priya@punnyaheart.org",
            StationId = "ST-102"
        };
        var manager = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Operations Manager",
            Email = "manager@punnyaheart.org"
        };
        context.Users.AddRange(user, manager);

        var leave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            LeaveType = "CASUAL",
            StartDate = DateTime.UtcNow.Date.AddDays(3),
            EndDate = DateTime.UtcNow.Date.AddDays(4),
            TotalDays = 2,
            Reason = "Personal work",
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };
        context.LeaveRequests.Add(leave);
        await context.SaveChangesAsync();

        // Act
        var result = await leaveService.ReviewLeaveAsync(leave.Id, "APPROVE", "Approved, take care.", manager.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("APPROVED", result.Status);
        Assert.Equal(manager.Id, result.ReviewedBy);
        Assert.Equal("Operations Manager", result.ReviewerName);
        Assert.Equal("Approved, take care.", result.ReviewComments);
        Assert.NotNull(result.ReviewedAt);

        mockNotifier.Verify(n => n.BroadcastWorkstationEventAsync(It.Is<LiveWorkstationEventDto>(
            e => e.EventType == "LEAVE_STATUS_CHANGED")), Times.Once);
    }
}
