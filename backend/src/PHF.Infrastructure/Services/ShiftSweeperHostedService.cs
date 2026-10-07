using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PHF.Core.Interfaces;

namespace PHF.Infrastructure.Services;

public class ShiftSweeperHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShiftSweeperHostedService> _logger;

    public ShiftSweeperHostedService(IServiceScopeFactory scopeFactory, ILogger<ShiftSweeperHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ShiftSweeperHostedService started. Monitoring and sweeping stale shifts.");

        // Initial brief delay on application boot before first sweep
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var shiftService = scope.ServiceProvider.GetRequiredService<IShiftService>();
                var closed = await shiftService.AutoCloseStaleShiftsAsync(stoppingToken);
                if (closed > 0)
                {
                    _logger.LogInformation("ShiftSweeper: Successfully auto-closed {Count} stale/abandoned shift(s).", closed);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ShiftSweeper: Exception occurred while auto-closing stale shifts.");
            }

            try
            {
                // Periodic sweep every 10 minutes
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("ShiftSweeperHostedService stopped.");
    }
}
