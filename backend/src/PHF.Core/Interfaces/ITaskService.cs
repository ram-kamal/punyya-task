using PHF.Core.DTOs;

namespace PHF.Core.Interfaces;

public interface ITaskService
{
    Task<List<TaskResponseDto>> GetTasksAsync(string? status = null, Guid? assignedTo = null, string? stationId = null, bool includeUnassigned = false, string? priority = null, CancellationToken cancellationToken = default);
    Task<TaskResponseDto?> GetTaskByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskResponseDto> CreateTaskAsync(CreateTaskRequest request, Guid assignedBy, CancellationToken cancellationToken = default);
    Task<TaskResponseDto?> UpdateTaskStatusAsync(Guid id, UpdateTaskStatusRequest request, Guid updatedBy, CancellationToken cancellationToken = default);
    Task<TaskResponseDto?> UpdateTaskAsync(Guid id, UpdateTaskRequest request, Guid updatedBy, CancellationToken cancellationToken = default);
    Task<TaskAttachmentDto> AttachDeliverableAsync(AttachDeliverableRequest request, Guid uploadedBy, CancellationToken cancellationToken = default);
    Task<bool> DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BulkDeleteTasksResponse> BulkDeleteTasksAsync(List<Guid> taskIds, CancellationToken cancellationToken = default);
    Task<DeleteAllTasksResponse> DeleteAllTasksAsync(Guid deletedBy, string deletedByName, CancellationToken cancellationToken = default);
    Task<List<TaskBackupDto>> GetTaskBackupsAsync(CancellationToken cancellationToken = default);
    Task<RestoreBackupResponse?> RestoreTaskBackupAsync(Guid backupId, Guid restoredBy, CancellationToken cancellationToken = default);
    Task<bool> DismissBackupReminderAsync(Guid backupId, int milestoneDay, CancellationToken cancellationToken = default);
}
