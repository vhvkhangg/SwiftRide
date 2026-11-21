using MassTransit;
using SwiftRide.Contracts.Matching;
using SwiftRide.MatchingService.Application.Interfaces;
using SwiftRide.MatchingService.Api;
using SwiftRide.MatchingService.Domain.Interfaces;

namespace SwiftRide.MatchingService.Application.Consumers;

public sealed class FindDriverForTripConsumer(
    IMatchingService matching,
    IMatchingOutboxRepository outbox,
    TripAccessClient trips,
    ILogger<FindDriverForTripConsumer> logger) : IConsumer<FindDriverForTripV1>
{
    public async Task Consume(ConsumeContext<FindDriverForTripV1> context)
    {
        var command = context.Message;
        if (command.TripId == Guid.Empty || command.CorrelationId != command.TripId || command.Attempt < 1)
            throw new ArgumentException("Invalid matching command.");
        var trip = await trips.GetAsync(command.TripId, context.CancellationToken);
        if (trip is null || trip.Status != "PriceConfirmed" || trip.MatchingAttempt != command.Attempt)
            return;
        if (await outbox.ExistsAsync(command.TripId, command.Attempt, context.CancellationToken))
            return;

        var candidate = await matching.FindNextDriverAsync(command, context.CancellationToken);

        if (candidate is null)
        {
            await outbox.EnqueueNoDriverFoundAsync(
                command.TripId,
                command.Attempt,
                "Không tìm thấy tài xế khả dụng chưa được thử.",
                context.CancellationToken);

            logger.LogInformation("Không tìm thấy tài xế cho Trip {TripId}, lượt {Attempt}",
                command.TripId, command.Attempt);
            return;
        }

        if (!Guid.TryParse(candidate.DriverId, out var driverId) || driverId == Guid.Empty)
        {
            await outbox.EnqueueNoDriverFoundAsync(
                command.TripId,
                command.Attempt,
                "Ứng viên tài xế có DriverId không hợp lệ.",
                context.CancellationToken);

            logger.LogWarning(
                "Bỏ qua ứng viên có DriverId không hợp lệ cho Trip {TripId}, lượt {Attempt}",
                command.TripId,
                command.Attempt);
            return;
        }

        logger.LogInformation("Đã tìm thấy Driver {DriverId} cho Trip {TripId}, lượt {Attempt}",
            driverId, command.TripId, command.Attempt);
    }
}
