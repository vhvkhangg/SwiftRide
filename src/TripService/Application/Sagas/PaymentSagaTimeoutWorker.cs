using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SwiftRide.Contracts.Trips;
using SwiftRide.TripService.Infrastructure;

namespace SwiftRide.TripService.Application.Sagas;

/// <summary>
/// Thay cho bộ lập lịch message (RabbitMQ chưa bật plugin delayed exchange): định kỳ tìm saga
/// còn AwaitingPayment đã quá hạn và phát TripPaymentTimedOutV1. Phát lặp lại là an toàn vì
/// saga bỏ qua timeout ở mọi trạng thái khác AwaitingPayment.
/// </summary>
public sealed class PaymentSagaTimeoutWorker(
    IServiceScopeFactory scopes,
    IBus bus,
    IOptions<TripPaymentSagaOptions> options,
    ILogger<PaymentSagaTimeoutWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.ScanIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishExpiredAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Payment saga timeout scan failed; retrying next interval.");
            }
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task PublishExpiredAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TripDbContext>();
        var now = DateTime.UtcNow;
        var expired = await db.Set<TripPaymentSagaState>().AsNoTracking()
            .Where(s => s.CurrentState == nameof(TripPaymentStateMachine.AwaitingPayment) && s.DeadlineUtc <= now)
            .OrderBy(s => s.DeadlineUtc)
            .Select(s => new { s.CorrelationId, s.DeadlineUtc })
            .Take(50)
            .ToListAsync(ct);

        foreach (var saga in expired)
        {
            logger.LogInformation("Payment saga timed out. TripId={TripId}, DeadlineUtc={DeadlineUtc}",
                saga.CorrelationId, saga.DeadlineUtc);
            await bus.Publish(new TripPaymentTimedOutV1
            {
                CorrelationId = saga.CorrelationId,
                TripId = saga.CorrelationId,
                DeadlineUtc = new DateTimeOffset(DateTime.SpecifyKind(saga.DeadlineUtc, DateTimeKind.Utc))
            }, ct);
        }
    }
}
