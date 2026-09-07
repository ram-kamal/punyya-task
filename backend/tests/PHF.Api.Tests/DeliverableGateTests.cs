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

public class DeliverableGateTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task UpdateTaskStatus_WhenCompletingWithoutDeliverable_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockNotifier = new Mock<IRealtimeNotifier>();
        var taskService = new TaskService(context, mockNotifier.Object);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            TaskCode = "TASK-201",
            Title = "Implement Security Audit",
            Status = TaskItemStatus.IN_PROGRESS.ToString(),
            DeliverableRequired = true,
            DeliverableUrl = null,
            DeliverableFileName = null
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var request = new UpdateTaskStatusRequest
        {
            Status = TaskItemStatus.COMPLETED.ToString(),
            DeliverableUrl = null,
            DeliverableFileName = null
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            taskService.UpdateTaskStatusAsync(task.Id, request, Guid.NewGuid()));

        Assert.Contains("Deliverable proof of work", ex.Message);
    }

    [Fact]
    public async Task UpdateTaskStatus_WhenCompletingWithDeliverable_SucceedsAndSetsCompletedTimestamp()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockNotifier = new Mock<IRealtimeNotifier>();
        var taskService = new TaskService(context, mockNotifier.Object);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            TaskCode = "TASK-202",
            Title = "Database Migration Scripts",
            Status = TaskItemStatus.IN_PROGRESS.ToString(),
            DeliverableRequired = true,
            DeliverableUrl = null
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var request = new UpdateTaskStatusRequest
        {
            Status = TaskItemStatus.COMPLETED.ToString(),
            DeliverableUrl = "/storage/deliverables/migration.sql",
            DeliverableFileName = "migration.sql"
        };

        // Act
        var result = await taskService.UpdateTaskStatusAsync(task.Id, request, Guid.NewGuid());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TaskItemStatus.COMPLETED.ToString(), result.Status);
        Assert.Equal("/storage/deliverables/migration.sql", result.DeliverableUrl);
        Assert.NotNull(result.CompletedAt);
    }
}
