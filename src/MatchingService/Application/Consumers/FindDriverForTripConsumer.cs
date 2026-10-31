using MassTransit;
using SwiftRide.Contracts.Matching;
using SwiftRide.MatchingService.Application.Interfaces;

namespace SwiftRide.MatchingService.Application.Consumers;

public sealed class FindDriverForTripConsumer(
    IMatchingService matching,
    ILogger<FindDriverForTripConsumer> logger) : IConsumer<FindDriverForTripV1>
{
    public async Task Consume(ConsumeContext<FindDriverForTripV1> context)
    {
        var command = context.Message;
        var candidate = await matching.FindNextDriverAsync(command, context.CancellationToken);

        if (candidate is null)
        {
            await context.Publish(new NoDriverFoundForTripV1
            {
                CorrelationId = command.CorrelationId,
                TripId = command.TripId,
                Attempt = command.Attempt,
                Reason = "Không tìm thấy tài xế khả dụng chưa được thử.",
                OccurredAtUtc = DateTimeOffset.UtcNow
            }, context.CancellationToken);

            logger.LogInformation("Không tìm thấy tài xế cho Trip {TripId}, lượt {Attempt}",
                command.TripId, command.Attempt);
            return;
        }

        if (!Guid.TryParse(candidate.DriverId, out var driverId) || driverId == Guid.Empty)
        {
            await context.Publish(new NoDriverFoundForTripV1
            {
                CorrelationId = command.CorrelationId,
                TripId = command.TripId,
                Attempt = command.Attempt,
                Reason = "Ứng viên tài xế có DriverId không hợp lệ.",
                OccurredAtUtc = DateTimeOffset.UtcNow
            }, context.CancellationToken);

            logger.LogWarning(
                "Bỏ qua ứng viên có DriverId không hợp lệ cho Trip {TripId}, lượt {Attempt}",
                command.TripId,
                command.Attempt);
            return;
        }

        await context.Publish(new DriverFoundForTripV1
        {
            CorrelationId = command.CorrelationId,
            TripId = command.TripId,
            Attempt = command.Attempt,
            DriverId = driverId,
            OccurredAtUtc = DateTimeOffset.UtcNow
        }, context.CancellationToken);

        logger.LogInformation("Đã tìm thấy Driver {DriverId} cho Trip {TripId}, lượt {Attempt}",
            driverId, command.TripId, command.Attempt);
    }
}
