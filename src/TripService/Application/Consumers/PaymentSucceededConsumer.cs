using MassTransit;
using SwiftRide.Contracts.Payments;
using SwiftRide.TripService.Application.Interfaces;

namespace SwiftRide.TripService.Application.Consumers;

public sealed class PaymentSucceededConsumer(ITripApplicationService trips)
    : IConsumer<PaymentSucceededV1>
{
    public Task Consume(ConsumeContext<PaymentSucceededV1> context) =>
        trips.HandlePaymentSucceededAsync(context.Message, context.CancellationToken);
}
