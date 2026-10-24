using MassTransit;
using SwiftRide.Contracts.Matching;
using SwiftRide.TripService.Application.Interfaces;

namespace SwiftRide.TripService.Application.Consumers;

public sealed class NoDriverFoundForTripConsumer(ITripApplicationService trips)
    : IConsumer<NoDriverFoundForTripV1>
{
    public Task Consume(ConsumeContext<NoDriverFoundForTripV1> context) =>
        trips.HandleNoDriverFoundAsync(context.Message, context.CancellationToken);
}
