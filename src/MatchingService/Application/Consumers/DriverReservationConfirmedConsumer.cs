using MassTransit;
using SwiftRide.Contracts.Matching;
using SwiftRide.MatchingService.Domain.Interfaces;

namespace SwiftRide.MatchingService.Application.Consumers;

public sealed class DriverReservationConfirmedConsumer(
    IDriverRepository repository,
    ILogger<DriverReservationConfirmedConsumer> logger)
    : IConsumer<DriverReservationConfirmedV1>
{
    public async Task Consume(ConsumeContext<DriverReservationConfirmedV1> context)
    {
        var message = context.Message;
        if (message.TripId == Guid.Empty || message.DriverId == Guid.Empty || message.Attempt < 1)
            throw new ArgumentException("DriverReservationConfirmedV1 chứa dữ liệu không hợp lệ.");

        var confirmed = await repository.ConfirmReservationAsync(
            message.DriverId.ToString("D"),
            message.TripId,
            message.Attempt,
            context.CancellationToken);
        if (!confirmed)
            logger.LogWarning(
                "Không tìm thấy reservation để confirm cho Trip {TripId}, Driver {DriverId}, Attempt {Attempt}",
                message.TripId, message.DriverId, message.Attempt);
    }
}
