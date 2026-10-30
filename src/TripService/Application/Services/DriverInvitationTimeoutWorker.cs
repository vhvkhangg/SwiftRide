using SwiftRide.TripService.Application.Interfaces;

namespace SwiftRide.TripService.Application.Services;

public sealed class DriverInvitationTimeoutWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DriverInvitationTimeoutWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessExpiredInvitationsAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ProcessExpiredInvitationsAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITripWorkflowRepository>();
            var trips = scope.ServiceProvider.GetRequiredService<ITripApplicationService>();
            
            // Nếu rớt kết nối DB, lỗi sẽ xảy ra ở đây.
            var expired = await repository.GetExpiredInvitationsAsync(DateTime.UtcNow, 50, ct);

            foreach (var invitation in expired)
            {
                try
                {
                    await trips.HandleInvitationTimeoutAsync(
                        invitation.TripId, invitation.DriverId, invitation.Attempt, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Không xử lý được lời mời hết hạn cho Trip {TripId}, Driver {DriverId}, Attempt {Attempt}",
                        invitation.TripId, invitation.DriverId, invitation.Attempt);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // Bắt lỗi toàn cục của vòng lặp để bảo vệ Worker không bị chết vì lỗi DB tạm thời
            logger.LogWarning(ex, "Lỗi kết nối DB tạm thời khi quét timeout. Sẽ thử lại ở chu kỳ (tick) tiếp theo.");
        }
    }
}
