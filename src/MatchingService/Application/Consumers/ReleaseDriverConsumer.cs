using MassTransit;
using SwiftRide.Contracts.Matching;
using SwiftRide.MatchingService.Domain.Interfaces;

namespace SwiftRide.MatchingService.Application.Consumers;

public sealed class ReleaseDriverConsumer(IDriverRepository repository) : IConsumer<ReleaseDriverV1>
{
    public async Task Consume(ConsumeContext<ReleaseDriverV1> context)
    {
        var command = context.Message;
        if (command.TripId == Guid.Empty || command.CorrelationId != command.TripId)
            throw new ArgumentException("Invalid reservation release command.");
        await repository.ReleaseReservationAsync(command.DriverId == Guid.Empty ? "" : command.DriverId.ToString("D"),
            command.TripId, null, context.CancellationToken);
    }
}
