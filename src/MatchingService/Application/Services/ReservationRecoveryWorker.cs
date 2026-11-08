using SwiftRide.MatchingService.Api;
using SwiftRide.MatchingService.Domain.Interfaces;

namespace SwiftRide.MatchingService.Application.Services;

public sealed class ReservationRecoveryWorker(IServiceScopeFactory scopes,
    ILogger<ReservationRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
                var trips = scope.ServiceProvider.GetRequiredService<TripAccessClient>();
                foreach (var reservation in await repository.GetReservationsToReconcileAsync(stoppingToken))
                {
                    var tripId = reservation.ReservationTripId!.Value;
                    var trip = await trips.GetAsync(tripId, stoppingToken);
                    var pending = trip?.Status == "PriceConfirmed" && trip.MatchingAttempt == reservation.ReservationAttempt;
                    var active = trip?.DriverId?.ToString("D") == reservation.DriverId &&
                        trip.Status is "DriverAccepted" or "EnRoute" or "PickedUp";
                    if (pending || active)
                        await repository.DeferReconciliationAsync(reservation.DriverId, tripId, stoppingToken);
                    else
                        await repository.ReleaseReservationAsync(reservation.DriverId, tripId, reservation.ReservationAttempt, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning(exception, "Reservation reconciliation failed; retry next tick."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
