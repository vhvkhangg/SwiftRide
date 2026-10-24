using MassTransit;
using SwiftRide.Contracts.Matching;
using SwiftRide.TripService.Application.Interfaces;

namespace SwiftRide.TripService.Application.Consumers;

public sealed class DriverFoundForTripConsumer(ITripApplicationService trips)
    : IConsumer<DriverFoundForTripV1>
{
    public Task Consume(ConsumeContext<DriverFoundForTripV1> context) =>
        trips.HandleDriverFoundAsync(context.Message, context.CancellationToken);
}
