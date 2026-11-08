using MassTransit;
using SwiftRide.Contracts.Matching;
using SwiftRide.MatchingService.Domain.Interfaces;

namespace SwiftRide.MatchingService.Application.Consumers;

public sealed class DriverReservationReleasedConsumer(
    IDriverRepository repository,
    ILogger<DriverReservationReleasedConsumer> logger)
    : IConsumer<DriverReservationReleasedV1>
{
    public async Task Consume(ConsumeContext<DriverReservationReleasedV1> context)
    {
        var message = context.Message;
        if (message.TripId == Guid.Empty || message.DriverId == Guid.Empty || message.Attempt < 1)
            throw new ArgumentException("DriverReservationReleasedV1 chứa dữ liệu không hợp lệ.");

        var released = await repository.ReleaseReservationAsync(
            message.DriverId.ToString("D"),
            message.TripId,
            message.Attempt,
            context.CancellationToken);
        if (!released)
            logger.LogInformation(
                "Reservation đã được giải phóng hoặc owner không còn khớp cho Trip {TripId}, Driver {DriverId}, Attempt {Attempt}",
                message.TripId, message.DriverId, message.Attempt);
    }
}
