using MassTransit;
using SwiftRide.Contracts.Payments;
using SwiftRide.TripService.Application.Interfaces;

namespace SwiftRide.TripService.Application.Consumers;

public sealed class PaymentFailedConsumer(ITripApplicationService trips)
    : IConsumer<PaymentFailedV1>
{
    public Task Consume(ConsumeContext<PaymentFailedV1> context) =>
        trips.HandlePaymentFailedAsync(context.Message, context.CancellationToken);
}
