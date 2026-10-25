using MassTransit;
using SwiftRide.Contracts.Payments;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Messaging;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Enums;
using SwiftRide.PaymentService.Application.Exceptions;

namespace SwiftRide.PaymentService.Application.Consumers;

public sealed class ProcessPaymentConsumer : IConsumer<ProcessPaymentV1>
{
    private readonly IPaymentApplicationService _payments;

    private readonly ILogger<ProcessPaymentConsumer> _logger;

    public ProcessPaymentConsumer(
        IPaymentApplicationService payments,
        ILogger<ProcessPaymentConsumer> logger)
    {
        _payments = payments;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessPaymentV1> context)
    {
        _logger.LogInformation(
            "Processing payment command. TripId={TripId}, CorrelationId={CorrelationId}",
            context.Message.TripId,
            context.Message.CorrelationId);

        var request = ProcessPaymentV1Mapper.ToRequest(context.Message);
        PaymentResponse payment;
        try
        {
            payment = await _payments.ProcessPaymentCommandAsync(request, context.CancellationToken);
        }
        catch (PaymentCommandRejectedException ex)
        {
            await context.Publish(new PaymentFailedV1
            {
                CorrelationId = context.Message.CorrelationId,
                TripId = context.Message.TripId,
                IdempotencyKey = context.Message.IdempotencyKey,
                Reason = ex.Message,
                OccurredAtUtc = DateTimeOffset.UtcNow
            }, context.CancellationToken);

            _logger.LogWarning(
                "Payment rejected. TripId={TripId}, Reason={Reason}",
                context.Message.TripId,
                ex.Message);

            return;
        }

        if (payment.Status != nameof(PaymentStatus.Succeeded))
        {
            await context.Publish(new PaymentFailedV1
            {
                CorrelationId = context.Message.CorrelationId,
                TripId = context.Message.TripId,
                IdempotencyKey = context.Message.IdempotencyKey,
                Reason = $"Payment kết thúc với trạng thái '{payment.Status}'.",
                OccurredAtUtc = DateTimeOffset.UtcNow
            }, context.CancellationToken);

            _logger.LogInformation(
                "Payment success event staged. PaymentId={PaymentId}, TripId={TripId}",
                payment.Id,
                payment.TripId);

            return;
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