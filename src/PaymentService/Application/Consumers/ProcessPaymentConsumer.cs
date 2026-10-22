using MassTransit;
using SwiftRide.Contracts.Payments;
using SwiftRide.PaymentService.Application.Messaging;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Enums;

namespace SwiftRide.PaymentService.Application.Consumers;

public sealed class ProcessPaymentConsumer : IConsumer<ProcessPaymentV1>
{
    private readonly IPaymentApplicationService _payments;

    public ProcessPaymentConsumer(
        IPaymentApplicationService payments)
    {
        _payments = payments;
    }

    public async Task Consume(ConsumeContext<ProcessPaymentV1> context)
    {
        var request = ProcessPaymentV1Mapper.ToRequest(context.Message);

        var payment = await _payments.ProcessPaymentCommandAsync(request, context.CancellationToken);

        if (payment.Status != nameof(PaymentStatus.Succeeded))
        {
            throw new InvalidOperationException(
                $"Không thể phát PaymentSucceededV1 " +
                $"cho Payment có trạng thái '{payment.Status}'.");
        }

        await context.Publish(
            new PaymentSucceededV1
            {
                CorrelationId = context.Message.CorrelationId,
                TripId = payment.TripId,
                PaymentId = payment.Id,
                Amount = payment.Amount,
                Currency = context.Message.Currency,
                OccurredAtUtc = DateTimeOffset.UtcNow
            }, context.CancellationToken
        );
    }
}