using PHF.Core.DTOs;

namespace PHF.Core.Interfaces;

public interface ITelemetryService
{
    Task<int> IngestBatchAsync(TelemetryBatchRequest request, CancellationToken cancellationToken = default);
    Task ProcessHeartbeatAsync(WorkstationHeartbeatDto heartbeat, CancellationToken cancellationToken = default);
    Task<List<WorkstationStatusDto>> GetWorkstationsStatusAsync(CancellationToken cancellationToken = default);
    Task<List<LiveWorkstationEventDto>> GetRecentEventsAsync(int limit = 50, CancellationToken cancellationToken = default);
}
