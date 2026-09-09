using PHF.Core.DTOs;

namespace PHF.Core.Interfaces;

public interface ITaskService
{
    Task<List<TaskResponseDto>> GetTasksAsync(string? status = null, Guid? assignedTo = null, string? stationId = null, bool includeUnassigned = false, string? priority = null, CancellationToken cancellationToken = default);
    Task<TaskResponseDto?> GetTaskByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskResponseDto> CreateTaskAsync(CreateTaskRequest request, Guid assignedBy, CancellationToken cancellationToken = default);
    Task<TaskResponseDto?> UpdateTaskStatusAsync(Guid id, UpdateTaskStatusRequest request, Guid updatedBy, CancellationToken cancellationToken = default);
    Task<TaskAttachmentDto> AttachDeliverableAsync(AttachDeliverableRequest request, Guid uploadedBy, CancellationToken cancellationToken = default);
    Task<bool> DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default);
}
