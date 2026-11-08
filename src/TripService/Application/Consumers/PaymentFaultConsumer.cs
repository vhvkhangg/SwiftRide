using MassTransit;
using SwiftRide.Contracts.Payments;
using SwiftRide.TripService.Application.Interfaces;

namespace SwiftRide.TripService.Application.Consumers;

public sealed class PaymentFaultConsumer(ITripApplicationService trips) : IConsumer<Fault<ProcessPaymentV1>>
{
    public Task Consume(ConsumeContext<Fault<ProcessPaymentV1>> context) =>
        trips.HandlePaymentFaultAsync(context.Message.Message, context.CancellationToken);
}
